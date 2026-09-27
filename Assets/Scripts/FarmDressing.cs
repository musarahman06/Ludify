using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the farm lush (called by Editor/WorldCollidersSceneProcessor.cs on entering Play Mode and in builds,
/// before static batching; the saved scene is never changed):
///   - a farmyard west of the farmhouse with a red barn, silo, windmill, chicken coop, hay bales and a fence,
///   - every sparse crop plot filled out into rows of the same crop (a green corn / golden wheat patchwork),
///   - hedgerows, flower meadows, an orchard and rocks,
///   - green grass painted between the crop rows (<see cref="FarmGround"/>, on an in-memory copy of the terrain).
/// Idempotent: does nothing if "_FarmDressing" already exists.
/// </summary>
public static class FarmDressing
{
    public class Assets
    {
        public readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        public Material LitTemplate;
        public TerrainLayer Green, Dirt;
    }

    public const string RootName = "_FarmDressing";
    const float CellX = 8f, CellZ = 14f;
    /// <summary>Crops stay below the player (who is 2 m tall) so you can see across the fields.</summary>
    const float MaxCropHeight = 1.4f;

    // Quaternius MTL colours (linear), converted to sRGB when applied.
    static readonly Dictionary<string, Color> QuaterniusColors = new Dictionary<string, Color>
    {
        { "DarkRed", new Color(0.202f, 0.0425f, 0.0321f) }, { "LightRed", new Color(0.274f, 0.056f, 0.042f) },
        { "RoofBlack", new Color(0.0788f, 0.0788f, 0.0788f) }, { "White", new Color(0.64f, 0.64f, 0.64f) },
        { "Brown", new Color(0.225f, 0.0767f, 0.0374f) }, { "Grey", new Color(0.351f, 0.351f, 0.351f) },
        { "Black", new Color(0.031f, 0.029f, 0.032f) }, { "DarkBrown", new Color(0.143f, 0.0956f, 0.049f) },
        { "DarkGrey", new Color(0.176f, 0.176f, 0.176f) }, { "LightBrown", new Color(0.329f, 0.216f, 0.107f) },
    };

    static Transform root;
    static System.Random rng;
    static readonly List<Bounds> blocked = new List<Bounds>();
    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    static Assets assets;

    public static void Build(Scene scene, Assets a)
    {
        var crops = FindIn(scene, "FarmCrops");
        var house = FindIn(scene, "FarmHouse");
        if (crops == null || house == null || FindIn(scene, RootName) != null) return;
        assets = a;
        rng = new System.Random(777);
        blocked.Clear();
        matCache.Clear();

        var go = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(go, scene);
        root = go.transform;

        // Keep clear of roads and the farmhouse.
        foreach (var name in new[] { "RoadConnectors", "Roads_Suburb" })
        {
            var g = FindIn(scene, name);
            if (g == null) continue;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { var b = r.bounds; b.Expand(new Vector3(3f, 0f, 3f)); blocked.Add(b); }
        }
        foreach (var r in house.GetComponentsInChildren<Renderer>()) { var b = r.bounds; b.Expand(new Vector3(4f, 0f, 4f)); blocked.Add(b); }

        // Nothing from the farm may spill onto the race track (asphalt, kerbs, barriers, stands, pit lane).
        float trackMaxZ = float.NegativeInfinity;
        var circuit = FindIn(scene, "F1Circuit");
        if (circuit != null)
            foreach (var r in circuit.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                trackMaxZ = Mathf.Max(trackMaxZ, b.max.z);
                b.Expand(new Vector3(8f, 0f, 8f));
                blocked.Add(b);
            }

        Vector3 farmhouse = house.transform.childCount > 0 ? house.transform.GetChild(0).position : house.transform.position;
        foreach (Transform t in house.transform) if (t.name.StartsWith("Farmhouse")) farmhouse = t.position;

        // Keep off the extended race track (and the old west farm it replaced).
        var track = FindIn(scene, TrackExtension.RootName);
        if (track != null)
            foreach (var r in track.GetComponentsInChildren<Renderer>()) { var b = r.bounds; b.Expand(new Vector3(8f, 0f, 8f)); blocked.Add(b); }
        foreach (var g in TrackExtension.GreenRects)
            blocked.Add(new Bounds(new Vector3(g.center.x, 0f, g.center.y), new Vector3(g.width, 200f, g.height)));

        // Farm extent from the (remaining) crop grid.
        Bounds farm = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        foreach (Transform c in crops.transform)
        {
            if (!c.gameObject.activeSelf) continue;
            if (!any) { farm = new Bounds(c.position, Vector3.zero); any = true; } else farm.Encapsulate(c.position);
        }
        if (!any) return;
        farm.Expand(new Vector3(CellX, 0f, CellZ));
        if (!float.IsNegativeInfinity(trackMaxZ) && farm.min.z < trackMaxZ + 4f)
            farm.SetMinMax(new Vector3(farm.min.x, farm.min.y, trackMaxZ + 4f), farm.max);   // stop short of the track

        // The original crops are imported far taller than a person; bring them down to size too.
        foreach (Transform c in crops.transform)
            if (c.gameObject.activeSelf) FitHeight(c, MaxCropHeight * (0.8f + 0.2f * (float)rng.NextDouble()));

        var yard = BuildFarmyard(crops, farmhouse, farm);
        var dirtStrips = FillFields(crops, yard);
        Hedgerows(farm, yard);
        Meadows(farmhouse, yard);
        Orchard(farm, yard);

        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

        var ground = go.AddComponent<FarmGround>();
        ground.Setup(new Rect(farm.min.x, farm.min.z, farm.size.x, farm.size.z), dirtStrips, a.Green, a.Dirt, TrackExtension.GreenRects);
        Debug.Log($"[FarmDressing] Built farm dressing: {root.GetComponentsInChildren<Renderer>().Length} props.");
    }

    // ------------------------------------------------------------------ farmyard

    /// <summary>A 40 x 30 m yard west of the farmhouse: barn, silos, windmill, coop, hay and a fence, with an empty
    /// garden corner (south-west) kept free for the planting job. The single crops that stood there are switched off.
    /// Children "FarmyardCenter" and "FarmGardenCenter" tell the jobs where these are.</summary>
    static Bounds BuildFarmyard(GameObject crops, Vector3 farmhouse, Bounds farm)
    {
        float maxX = Mathf.Min(farmhouse.x - 12f, farm.max.x - 2f), maxZ = Mathf.Min(farmhouse.z + 7f, farm.max.z);
        var yard = new Bounds(new Vector3(maxX - 20f, farmhouse.y, maxZ - 15f), new Vector3(40f, 20f, 30f));
        foreach (Transform c in crops.transform)
            if (yard.Contains(new Vector3(c.position.x, yard.center.y, c.position.z))) c.gameObject.SetActive(false);

        Vector3 Y(float x, float z) => Ground(new Vector3(x, 0f, z));
        float x0 = yard.min.x, z0 = yard.min.z;

        Place("BigBarn", Y(x0 + 14f, z0 + 20f), 180f, 1.5f, collider: true);
        Place("Silo", Y(x0 + 25f, z0 + 23f), 0f, 1.2f, collider: true);
        Place("Silo", Y(x0 + 28.5f, z0 + 20f), 30f, 1.0f, collider: true);
        Place("TowerWindmill", Y(x0 + 35f, z0 + 25f), 150f, 1.1f, collider: true);
        Place("ChickenCoop", Y(x0 + 31f, z0 + 7f), 200f, 1.3f, collider: true);

        // Hay: round bales lying around and a little stack of square bales by the barn.
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = Y(x0 + 19f + (float)rng.NextDouble() * 16f, z0 + 10f + (float)rng.NextDouble() * 6f);
            RoundBale(p, (float)rng.NextDouble() * 180f);
        }
        for (int i = 0; i < 6; i++)
            SquareBale(Y(x0 + 20.5f + (i % 3) * 1.25f, z0 + 13.5f) + Vector3.up * (i / 3 * 0.6f), 0f);

        Marker("FarmyardCenter", Y(x0 + 20f, z0 + 9f));
        Marker("FarmGardenCenter", Y(x0 + 8f, z0 + 6.5f));   // left empty for the planting job

        // Fence around the yard, leaving a gate on the south side.
        FenceLine(Y(x0, z0), Y(x0, yard.max.z));
        FenceLine(Y(x0, yard.max.z), Y(yard.max.x, yard.max.z));
        FenceLine(Y(yard.max.x, yard.max.z), Y(yard.max.x, z0));
        FenceLine(Y(yard.max.x, z0), Y(x0 + 23f, z0));
        FenceLine(Y(x0 + 17f, z0), Y(x0, z0));

        yard.Expand(new Vector3(2f, 0f, 2f));
        blocked.Add(yard);
        return yard;
    }

    static void Marker(string name, Vector3 p)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        t.position = p;
    }

    static void RoundBale(Vector3 p, float yaw)
    {
        var g = new GameObject("HayBale").transform;
        g.SetParent(root, false);
        g.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        Prim(g, PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 0.65f, 1.4f), new Color(0.93f, 0.78f, 0.35f), new Vector3(0f, 0f, 90f));
        Prim(g, PrimitiveType.Cylinder, new Vector3(0f, 0.7f, 0f), new Vector3(1.1f, 0.66f, 1.1f), new Color(0.82f, 0.64f, 0.25f), new Vector3(0f, 0f, 90f));
        g.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.3f, 1.4f, 1.4f);
        g.GetComponent<BoxCollider>().center = new Vector3(0f, 0.7f, 0f);
    }

    static void SquareBale(Vector3 p, float yaw)
    {
        var t = Prim(root, PrimitiveType.Cube, Vector3.zero, new Vector3(1.2f, 0.6f, 0.8f), new Color(0.95f, 0.82f, 0.4f), new Vector3(0f, yaw, 0f));
        t.position = p + Vector3.up * 0.3f;
        t.gameObject.AddComponent<BoxCollider>();
    }

    static void FenceLine(Vector3 a, Vector3 b)
    {
        if (!assets.Models.ContainsKey("Fence2")) return;
        Vector3 d = b - a; d.y = 0f;
        float len = d.magnitude;
        const float seg = 5.8f;
        int n = Mathf.Max(1, Mathf.RoundToInt(len / seg));
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = Ground(a + d * ((i + 0.5f) / n));
            var f = Place("Fence2", p, yaw, 1f, collider: true, check: false);
            if (f != null) f.localScale = new Vector3(len / n / seg, 1f, 1f);
        }
    }

    // ------------------------------------------------------------------ fields

    /// <summary>Fills each sparse crop plot (one plant every 8 x 14 m) with rows of the same crop, and returns the
    /// dirt strips under the rows for the ground painter. Plants stay under <see cref="MaxCropHeight"/>.</summary>
    static List<Rect> FillFields(GameObject crops, Bounds yard)
    {
        var strips = new List<Rect>();
        float[] rowZ = { -4f, 0f, 4f };
        float[] plantX = { -2.6f, 0f, 2.6f };
        foreach (Transform c in crops.transform)
        {
            if (!c.gameObject.activeSelf) continue;
            string model = c.name.Split(' ')[0];
            if (!assets.Models.TryGetValue(model, out var proto)) continue;
            // Green, young corn plots grow up a bit so the patchwork reads green and gold.
            if (model == "crops_cornStageA" && rng.NextDouble() < 0.5 && assets.Models.ContainsKey("crops_cornStageC")) proto = assets.Models["crops_cornStageC"];

            Vector3 cp = c.position;
            foreach (float dz in rowZ)
            {
                float rowMin = float.MaxValue, rowMax = float.MinValue;
                foreach (float dx in plantX)
                {
                    if (dx == 0f && dz == 0f) continue;   // the original plant stands here
                    float jx = ((float)rng.NextDouble() - 0.5f) * 0.4f;
                    Vector3 at = cp + new Vector3(dx + jx, 0f, dz);
                    if (IsBlocked(new Bounds(at, new Vector3(1.5f, 4f, 1.5f)))) continue;   // road, yard, track...
                    var t = Object.Instantiate(proto, root).transform;
                    t.name = proto.name;
                    t.SetPositionAndRotation(Ground(at), c.rotation * Quaternion.Euler(0f, rng.Next(4) * 90f, 0f));
                    FitHeight(t, MaxCropHeight * (0.8f + 0.2f * (float)rng.NextDouble()));
                    rowMin = Mathf.Min(rowMin, at.x);
                    rowMax = Mathf.Max(rowMax, at.x);
                }
                if (dz == 0f) { rowMin = Mathf.Min(rowMin, cp.x); rowMax = Mathf.Max(rowMax, cp.x); }
                if (rowMax >= rowMin)
                    strips.Add(new Rect(rowMin - 1f, cp.z + dz - 0.75f, rowMax - rowMin + 2f, 1.5f));
            }
        }
        return strips;
    }

    // ------------------------------------------------------------------ greenery

    static void Hedgerows(Bounds farm, Bounds yard)
    {
        string[] bushes = { "plant_bush", "plant_bushLarge", "plant_bush" };
        // Around the whole farm, every ~3.5 m, skipping roads, the yard and anything else in the way.
        var corners = new[]
        {
            new Vector3(farm.min.x - 3f, 0f, farm.min.z - 3f), new Vector3(farm.max.x + 3f, 0f, farm.min.z - 3f),
            new Vector3(farm.max.x + 3f, 0f, farm.max.z + 3f), new Vector3(farm.min.x - 3f, 0f, farm.max.z + 3f),
        };
        for (int s = 0; s < 4; s++)
        {
            Vector3 a = corners[s], b = corners[(s + 1) % 4];
            int n = Mathf.FloorToInt(Vector3.Distance(a, b) / 3.5f);
            for (int i = 0; i <= n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)n) + new Vector3(((float)rng.NextDouble() - 0.5f), 0f, ((float)rng.NextDouble() - 0.5f));
                if (IsBlocked(new Bounds(p, new Vector3(2f, 4f, 2f)))) continue;
                Place(bushes[rng.Next(bushes.Length)], Ground(p), (float)rng.NextDouble() * 360f, 1.4f + (float)rng.NextDouble() * 0.8f, check: false);
            }
        }
    }

    static void Meadows(Vector3 farmhouse, Bounds yard)
    {
        string[] flowers = { "flower_yellowA", "flower_yellowA", "flower_yellowA", "flower_redA", "flower_purpleA" };
        // Clusters of flowers along the yard fence and around the farmhouse.
        var centers = new List<Vector3>();
        for (int i = 0; i < 10; i++)
        {
            float t = i / 10f * Mathf.PI * 2f;
            centers.Add(new Vector3(yard.center.x + Mathf.Cos(t) * (yard.extents.x + 3f), 0f, yard.center.z + Mathf.Sin(t) * (yard.extents.z + 3f)));
        }
        for (int i = 0; i < 6; i++)
        {
            float t = i / 6f * Mathf.PI * 2f;
            centers.Add(farmhouse + new Vector3(Mathf.Cos(t) * 9f, 0f, Mathf.Sin(t) * 9f));
        }
        foreach (var c in centers)
            for (int k = 0; k < 16; k++)
            {
                Vector2 o = RandomInCircle(3.5f);
                Vector3 p = c + new Vector3(o.x, 0f, o.y);
                if (IsBlocked(new Bounds(p, new Vector3(0.8f, 2f, 0.8f)))) continue;
                Place(flowers[rng.Next(flowers.Length)], Ground(p), (float)rng.NextDouble() * 360f, 1.6f + (float)rng.NextDouble() * 0.8f, check: false);
            }
        // A few rocks.
        string[] rocks = { "rock_smallA", "rock_smallB", "rock_smallC", "rock_largeA" };
        for (int i = 0; i < 14; i++)
        {
            Vector3 p = new Vector3(yard.center.x + ((float)rng.NextDouble() - 0.5f) * 90f, 0f, yard.center.z + ((float)rng.NextDouble() - 0.5f) * 60f);
            if (IsBlocked(new Bounds(p, new Vector3(2f, 3f, 2f)))) continue;
            Place(rocks[rng.Next(rocks.Length)], Ground(p), (float)rng.NextDouble() * 360f, 1.2f + (float)rng.NextDouble(), check: false);
        }
    }

    static void Orchard(Bounds farm, Bounds yard)
    {
        string[] trees = { "tree_default", "tree_oak", "tree_default" };
        // A 3 x 5 orchard just outside the fields on the side away from the river, plus trees along the edges.
        Vector3 start = new Vector3(farm.min.x - 10f, 0f, farm.center.z - 14f);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 5; j++)
            {
                Vector3 p = start + new Vector3(-i * 6f, 0f, j * 7f);
                if (IsBlocked(new Bounds(p, new Vector3(3f, 6f, 3f)))) continue;
                Place(trees[rng.Next(trees.Length)], Ground(p), (float)rng.NextDouble() * 360f, 2.4f + (float)rng.NextDouble() * 0.6f, check: false);
            }
        for (int i = 0; i < 16; i++)
        {
            bool south = i % 2 == 0;
            Vector3 p = new Vector3(Mathf.Lerp(farm.min.x, farm.max.x, (float)rng.NextDouble()), 0f, south ? farm.min.z - 7f : farm.max.z + 6f);
            if (IsBlocked(new Bounds(p, new Vector3(3f, 6f, 3f)))) continue;
            Place(trees[rng.Next(trees.Length)], Ground(p), (float)rng.NextDouble() * 360f, 2.2f + (float)rng.NextDouble() * 0.8f, check: false);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Uniformly scales an object so its rendered height is at most <paramref name="maxHeight"/>.</summary>
    static void FitHeight(Transform t, float maxHeight)
    {
        float h = BoundsOf(t).size.y;
        if (h > maxHeight && h > 0.001f) t.localScale *= maxHeight / h;
    }

    static Transform Place(string model, Vector3 position, float yaw, float scale, bool collider = false, bool check = true)
    {
        if (!assets.Models.TryGetValue(model, out var proto) || proto == null) return null;
        var t = Object.Instantiate(proto, root).transform;
        t.name = model;
        t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        t.localScale = Vector3.one * scale;
        Recolor(t);
        foreach (var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var b = BoundsOf(t);
        if (collider)
        {
            var box = t.gameObject.AddComponent<BoxCollider>();
            var local = new Bounds(t.InverseTransformPoint(b.center), Vector3.zero);
            box.center = local.center;
            box.size = new Vector3(b.size.x / scale, b.size.y / scale, b.size.z / scale);
            if (Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.7f) box.size = new Vector3(box.size.z, box.size.y, box.size.x);
            blocked.Add(b);
        }
        return t;
    }

    /// <summary>OBJ imports get legacy materials; swap them for URP Lit in the MTL colours (converted to sRGB).</summary>
    static void Recolor(Transform t)
    {
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.Replace(" (Instance)", "");
                if (!QuaterniusColors.TryGetValue(n, out var lin)) continue;
                if (!matCache.TryGetValue(n, out var m))
                {
                    var c = new Color(Mathf.LinearToGammaSpace(lin.r), Mathf.LinearToGammaSpace(lin.g), Mathf.LinearToGammaSpace(lin.b));
                    m = new Material(assets.LitTemplate) { name = "Farm_" + n, color = c };
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
                    matCache[n] = m;
                }
                mats[i] = m;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static Transform Prim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        string key = "Prim_" + ColorUtility.ToHtmlStringRGB(color);
        if (!matCache.TryGetValue(key, out var m)) { m = new Material(assets.LitTemplate) { name = key, color = color }; matCache[key] = m; }
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    static bool IsBlocked(Bounds b)
    {
        foreach (var x in blocked)
            if (x.min.x < b.max.x && x.max.x > b.min.x && x.min.z < b.max.z && x.max.z > b.min.z) return true;
        return false;
    }

    static Vector2 RandomInCircle(float r)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * r;
        return new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
    }

    static Vector3 Ground(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        if (t == null) return p;
        return new Vector3(p.x, t.SampleHeight(p) + t.GetPosition().y, p.z);
    }

    static Bounds BoundsOf(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static GameObject FindIn(Scene scene, string name)
    {
        foreach (var r in scene.GetRootGameObjects())
            foreach (var tr in r.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        return null;
    }
}

/// <summary>
/// Paints the farm green with brown dirt only under the crop rows. Works on an in-memory copy of the TerrainData,
/// so the saved terrain asset is never modified.
/// </summary>
public class FarmGround : MonoBehaviour
{
    [SerializeField] Rect farmRect;
    [SerializeField] List<Rect> dirtStrips = new List<Rect>();
    [SerializeField] TerrainLayer green, dirt;
    [SerializeField] List<Rect> grassRects = new List<Rect>();

    /// <summary>True once the farm ground has been painted this session (the loading screen waits for it).</summary>
    public static bool Painted { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Painted = false;

    public void Setup(Rect farm, List<Rect> strips, TerrainLayer greenLayer, TerrainLayer dirtLayer, List<Rect> extraGrass = null)
    {
        farmRect = farm;
        dirtStrips = strips;
        green = greenLayer;
        dirt = dirtLayer;
        grassRects = extraGrass != null ? new List<Rect>(extraGrass) : new List<Rect>();
    }

    // Start, not Awake: in the Editor the scene processor adds this component to the already-loaded scene, so Awake
    // ran before Setup() had passed in the layers and the farm (and the ground under the extended track) stayed brown.
    void Start()
    {
        // Terrain.activeTerrain is only set once the terrain is enabled; fall back to finding it.
        var terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : FindAnyObjectByType<Terrain>();
        if (terrain == null || green == null || dirt == null) { Painted = true; return; }

        var data = Instantiate(terrain.terrainData);
        data.name = terrain.terrainData.name + " (farm painted)";
        int gi = LayerIndex(data, green), di = LayerIndex(data, dirt);
        terrain.terrainData = data;
        var col = terrain.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = data;

        Paint(data, terrain.GetPosition(), farmRect, dirtStrips, gi, di);
        foreach (var r in grassRects) Paint(data, terrain.GetPosition(), r, null, gi, di);   // e.g. the old west farm, now grass
        Painted = true;
    }

    static void Paint(TerrainData data, Vector3 origin, Rect farmRect, List<Rect> dirtStrips, int gi, int di)
    {
        Vector3 size = data.size;
        int res = data.alphamapResolution, layers = data.alphamapLayers;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((farmRect.xMin - origin.x) / size.x * res), 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((farmRect.yMin - origin.z) / size.z * res), 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((farmRect.xMax - origin.x) / size.x * res), 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((farmRect.yMax - origin.z) / size.z * res), 0, res - 1);
        int w = x1 - x0 + 1, h = z1 - z0 + 1;
        var map = data.GetAlphamaps(x0, z0, w, h);
        const float border = 4f;   // soft blend into the surroundings

        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float wx = origin.x + (x0 + i + 0.5f) / res * size.x;
                float wz = origin.z + (z0 + j + 0.5f) / res * size.z;
                float edge = Mathf.Min(wx - farmRect.xMin, farmRect.xMax - wx, wz - farmRect.yMin, farmRect.yMax - wz);
                float blend = Mathf.Clamp01(edge / border);
                if (blend <= 0f) continue;
                bool isDirt = false;
                if (dirtStrips != null)
                    foreach (var s in dirtStrips) if (s.Contains(new Vector2(wx, wz))) { isDirt = true; break; }
                int target = isDirt ? di : gi;
                for (int l = 0; l < layers; l++)
                    map[j, i, l] = Mathf.Lerp(map[j, i, l], l == target ? 1f : 0f, blend);
            }
        data.SetAlphamaps(x0, z0, map);
    }

    static int LayerIndex(TerrainData data, TerrainLayer layer)
    {
        var list = new List<TerrainLayer>(data.terrainLayers);
        int i = list.IndexOf(layer);
        if (i >= 0) return i;
        list.Add(layer);   // only on the in-memory copy
        data.terrainLayers = list.ToArray();
        return list.Count - 1;
    }
}
