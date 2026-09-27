using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds CityMap's surroundings into the in-memory scene (called by Editor/WorldCollidersSceneProcessor.cs on
/// entering Play Mode and in builds, before static batching; the saved scene file is never changed):
///   1. arched, walkable bridges that start and end at road level and rise over the river,
///   2. rolling hills with trees all around the playable terrain, fading into the fog,
///   3. invisible walls around the playable terrain so nobody can walk, jump or drive off it.
/// Idempotent: does nothing if "_WorldDressing" already exists.
/// </summary>
public static class WorldDressing
{
    public class Assets
    {
        public Material road;                 // bridge deck surface
        public Material fallbackBridge;       // bridge sides/walls when a bridge has no specific material
        public Dictionary<string, Material> bridgeByDeckName = new Dictionary<string, Material>();
        public TerrainLayer hillLayer;        // texture + tiling for the hills
    }

    const string RootName = "_WorldDressing";
    static readonly string[] BridgeDecks = { "S1_Bridge_RoadDeck", "B2_TrussBridge_RoadDeck", "B3_ArchBridge_RoadDeck", "SuspensionBridge_RoadDeck" };

    public static void Build(Scene scene, Assets assets)
    {
        if (Find(scene, RootName) != null) return;
        var terrain = FindComponent<Terrain>(scene);
        if (terrain == null || Find(scene, "F1Circuit") == null) return;   // not CityMap

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        int bridges = 0;
        foreach (var deckName in BridgeDecks)
            if (BuildArchedBridge(scene, terrain, deckName, assets, root.transform)) bridges++;

        var area = PlayableArea(terrain);
        BuildHills(scene, terrain, area, assets, root.transform);
        BuildBoundary(area, root.transform);
        Debug.Log($"[WorldDressing] Built {bridges} arched bridges, surrounding hills and map boundary.");
    }

    // ------------------------------------------------------------------ helpers

    static Rect PlayableArea(Terrain t)
    {
        var p = t.GetPosition();
        var s = t.terrainData.size;
        return new Rect(p.x, p.z, s.x, s.z);
    }

    static float TerrainY(Terrain t, float x, float z)
    {
        var area = PlayableArea(t);
        var p = new Vector3(Mathf.Clamp(x, area.xMin, area.xMax), 0f, Mathf.Clamp(z, area.yMin, area.yMax));
        return t.SampleHeight(p) + t.GetPosition().y;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var r in scene.GetRootGameObjects())
            foreach (var tr in r.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        return null;
    }

    static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (var r in scene.GetRootGameObjects())
        {
            var c = r.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    static Material MakeLit(Material template, Color color)
    {
        var m = new Material(template) { color = color };
        return m;
    }

    // ------------------------------------------------------------------ bridges

    /// <summary>
    /// Replaces a floating road-bridge tile with an arched deck: each end sits on the road/terrain at the bank,
    /// the middle rises over the river. Deck, side walls (so you can't fall off) and a mesh collider.
    /// </summary>
    static bool BuildArchedBridge(Scene scene, Terrain terrain, string deckName, Assets assets, Transform parent)
    {
        var deck = Find(scene, deckName);
        if (deck == null) { Debug.LogWarning($"[WorldDressing] Bridge '{deckName}' not found."); return false; }
        var renderers = deck.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        // Span along the long horizontal axis of the old tile.
        bool alongX = b.size.x >= b.size.z;
        Vector3 axis = alongX ? Vector3.right : Vector3.forward;
        // side = axis x up keeps (side, up, axis) consistently handed, so clockwise profiles face outward.
        Vector3 side = Vector3.Cross(axis, Vector3.up);
        float length = alongX ? b.size.x : b.size.z;
        float width = Mathf.Clamp((alongX ? b.size.z : b.size.x) * 0.9f, 7f, 10f);
        Vector3 center = new Vector3(b.center.x, 0f, b.center.z);

        // Ends sit at road level at each bank (sampled just past the old tile's ends).
        Vector3 startXZ = center - axis * (length * 0.5f);
        Vector3 endXZ = center + axis * (length * 0.5f);
        float y0 = Mathf.Max(TerrainY(terrain, startXZ.x - axis.x * 3f, startXZ.z - axis.z * 3f), 0.06f);
        float y1 = Mathf.Max(TerrainY(terrain, endXZ.x + axis.x * 3f, endXZ.z + axis.z * 3f), 0.06f);
        float archHeight = Mathf.Clamp(length * 0.07f, 4f, 8f);

        var sideMat = assets.bridgeByDeckName.TryGetValue(deckName, out var bm) && bm != null ? bm : assets.fallbackBridge;
        var go = new GameObject(deckName.Replace("_RoadDeck", "_Arched"));
        go.transform.SetParent(parent, false);
        go.isStatic = true;

        var mesh = BuildBridgeMesh(startXZ, axis, side, length, width, y0, y1, archHeight);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { assets.road, sideMat };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        deck.SetActive(false);
        return true;
    }

    static Mesh BuildBridgeMesh(Vector3 start, Vector3 axis, Vector3 side, float length, float width,
                                float y0, float y1, float archHeight)
    {
        const int segments = 48;
        const float deckThickness = 0.9f, wallHeight = 1.1f, wallThickness = 0.4f;
        float half = width * 0.5f;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var roadTris = new List<int>();
        var sideTris = new List<int>();

        Vector3 Point(int i, float s, float up)
        {
            float t = i / (float)segments;
            float y = Mathf.Lerp(y0, y1, t) + archHeight * Mathf.Sin(Mathf.PI * t);
            return start + axis * (t * length) + side * s + Vector3.up * (y + up);
        }

        // Sweep a closed 2D profile (side offset, height offset), listed clockwise, along the arch; faces then
        // point outward. Each edge gets its own strip of vertices so faces stay flat-shaded.
        void Sweep(Vector2[] profile, List<int> tris, bool roadTop)
        {
            for (int e = 0; e < profile.Length; e++)
            {
                var a = profile[e];
                var c = profile[(e + 1) % profile.Length];
                bool isTop = roadTop && e == 0;
                var target = isTop ? roadTris : tris;
                int baseIndex = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    verts.Add(Point(i, a.x, a.y));
                    verts.Add(Point(i, c.x, c.y));
                    float u = i / (float)segments * length / 4f;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, Vector2.Distance(a, c) / 4f));
                }
                for (int i = 0; i < segments; i++)
                {
                    int v = baseIndex + i * 2;
                    target.Add(v); target.Add(v + 1); target.Add(v + 2);
                    target.Add(v + 1); target.Add(v + 3); target.Add(v + 2);
                }
            }
            // End caps (flat quads at both ends).
            foreach (int i in new[] { 0, segments })
            {
                int cap = verts.Count;
                foreach (var p in profile) { verts.Add(Point(i, p.x, p.y)); uvs.Add(new Vector2(p.x, p.y) / 4f); }
                for (int k = 1; k < profile.Length - 1; k++)
                {
                    // Clockwise fan faces +axis: keep it at the far end, reverse it at the start.
                    if (i == 0) { tris.Add(cap); tris.Add(cap + k + 1); tris.Add(cap + k); }
                    else { tris.Add(cap); tris.Add(cap + k); tris.Add(cap + k + 1); }
                }
            }
        }

        // Clockwise profiles; the deck's first edge is its top surface (road material).
        Sweep(new[]
        {
            new Vector2(-half, 0f), new Vector2(half, 0f),
            new Vector2(half, -deckThickness), new Vector2(-half, -deckThickness),
        }, sideTris, roadTop: true);
        float outerL = -half - wallThickness, outerR = half + wallThickness;
        Sweep(new[] { new Vector2(outerL, wallHeight), new Vector2(-half, wallHeight), new Vector2(-half, -deckThickness), new Vector2(outerL, -deckThickness) },
              sideTris, roadTop: false);
        Sweep(new[] { new Vector2(half, wallHeight), new Vector2(outerR, wallHeight), new Vector2(outerR, -deckThickness), new Vector2(half, -deckThickness) },
              sideTris, roadTop: false);

        var mesh = new Mesh { name = "ArchedBridge", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(roadTris, 0);
        mesh.SetTriangles(sideTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ hills

    const float HillRing = 220f;     // how far the hills extend beyond the playable area
    const float HillCell = 10f;

    static void BuildHills(Scene scene, Terrain terrain, Rect area, Assets assets, Transform parent)
    {
        // Where the river leaves the map on the south and north edges: keep a valley there.
        float riverSouthX = LowestAlongEdge(terrain, area, area.yMin);
        float riverNorthX = LowestAlongEdge(terrain, area, area.yMax);

        float HillHeight(float x, float z)
        {
            float dx = Mathf.Max(area.xMin - x, 0f, x - area.xMax);
            float dz = Mathf.Max(area.yMin - z, 0f, z - area.yMax);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float edge = TerrainY(terrain, x, z);
            if (d <= 0f) return edge;

            float hill = edge + 6f
                + 34f * Mathf.PerlinNoise(x * 0.007f + 13.1f, z * 0.007f + 7.7f)
                + 12f * Mathf.PerlinNoise(x * 0.022f + 3.3f, z * 0.022f + 91.2f)
                + d * 0.12f;
            float rise = Mathf.SmoothStep(0f, 1f, d / 55f);

            float valley = 1f;
            if (z < area.yMin) valley = Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x - riverSouthX) - 35f) / 55f);
            else if (z > area.yMax) valley = Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x - riverNorthX) - 35f) / 55f);
            return edge + (hill - edge) * rise * valley;
        }

        // Grid over the outer square, aligned to the playable edges so no cell straddles them.
        float x0 = area.xMin - HillRing, z0 = area.yMin - HillRing;
        int nx = Mathf.CeilToInt((area.width + 2f * HillRing) / HillCell);
        int nz = Mathf.CeilToInt((area.height + 2f * HillRing) / HillCell);
        var verts = new Vector3[(nx + 1) * (nz + 1)];
        var uvs = new Vector2[verts.Length];
        float tile = assets.hillLayer != null ? Mathf.Max(1f, assets.hillLayer.tileSize.x) : 50f;
        for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = x0 + i * HillCell, z = z0 + j * HillCell;
                verts[j * (nx + 1) + i] = new Vector3(x, HillHeight(x, z), z);
                uvs[j * (nx + 1) + i] = new Vector2(x / tile, z / tile);
            }

        var tris = new List<int>();
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float cx = x0 + (i + 0.5f) * HillCell, cz = z0 + (j + 0.5f) * HillCell;
                if (area.Contains(new Vector2(cx, cz))) continue;   // playable terrain covers this cell
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

        var mesh = new Mesh { name = "SurroundingHills", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var hills = new GameObject("SurroundingHills") { isStatic = true };
        hills.transform.SetParent(parent, false);
        hills.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mat = MakeLit(assets.fallbackBridge, new Color(0.72f, 0.82f, 0.6f));
        if (assets.hillLayer != null && assets.hillLayer.diffuseTexture != null)
        {
            mat.mainTexture = assets.hillLayer.diffuseTexture;
            mat.color = Color.white;
        }
        mat.name = "Mat_SurroundingHills";
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
        hills.AddComponent<MeshRenderer>().sharedMaterial = mat;

        ExtendRiver(scene, area, riverSouthX, riverNorthX, parent);
        PlantTrees(scene, area, HillHeight, riverSouthX, riverNorthX, parent);
    }

    static float LowestAlongEdge(Terrain terrain, Rect area, float z)
    {
        float bestX = area.center.x, bestY = float.MaxValue;
        for (float x = area.xMin; x <= area.xMax; x += 2f)
        {
            float y = TerrainY(terrain, x, z);
            if (y < bestY) { bestY = y; bestX = x; }
        }
        return bestX;
    }

    /// <summary>Continues the river's water surface out through the valleys so it doesn't stop at the map edge.</summary>
    static void ExtendRiver(Scene scene, Rect area, float southX, float northX, Transform parent)
    {
        var water = Find(scene, "RiverWater");
        var wr = water != null ? water.GetComponent<Renderer>() : null;
        if (wr == null) return;
        float y = water.transform.position.y;
        foreach (var (x, zMin, zMax) in new[] { (southX, area.yMin - HillRing, area.yMin + 1f), (northX, area.yMax - 1f, area.yMax + HillRing) })
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "RiverWaterExtension";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.SetPositionAndRotation(new Vector3(x, y, (zMin + zMax) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            q.transform.localScale = new Vector3(110f, zMax - zMin, 1f);
            q.GetComponent<Renderer>().sharedMaterial = wr.sharedMaterial;
            q.isStatic = true;
        }
    }

    static void PlantTrees(Scene scene, Rect area, System.Func<float, float, float> height,
                           float riverSouthX, float riverNorthX, Transform parent)
    {
        var source = Find(scene, "RiverTrees");
        if (source == null || source.transform.childCount == 0) return;
        var prototypes = new List<GameObject>();
        var seenNames = new HashSet<string>();
        foreach (Transform c in source.transform)
        {
            string key = c.name.Split(' ')[0];
            if (seenNames.Add(key)) prototypes.Add(c.gameObject);
        }

        var group = new GameObject("SurroundingTrees");
        group.transform.SetParent(parent, false);
        var rng = new System.Random(4242);
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        int placed = 0, attempts = 0;
        const int target = 260;
        while (placed < target && attempts++ < target * 20)
        {
            float x = Rand(area.xMin - HillRing * 0.85f, area.xMax + HillRing * 0.85f);
            float z = Rand(area.yMin - HillRing * 0.85f, area.yMax + HillRing * 0.85f);
            float dx = Mathf.Max(area.xMin - x, 0f, x - area.xMax);
            float dz = Mathf.Max(area.yMin - z, 0f, z - area.yMax);
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 12f) continue;                                       // keep the boundary clear
            if (z < area.yMin && Mathf.Abs(x - riverSouthX) < 60f) continue;   // not in the river valley
            if (z > area.yMax && Mathf.Abs(x - riverNorthX) < 60f) continue;
            // Denser near the map (foreground), thinning out towards the horizon.
            if (rng.NextDouble() > Mathf.Lerp(1f, 0.35f, d / HillRing)) continue;

            var proto = prototypes[rng.Next(prototypes.Count)];
            var tree = Object.Instantiate(proto, group.transform);
            tree.name = proto.name;
            tree.transform.SetPositionAndRotation(new Vector3(x, height(x, z) - 0.2f, z), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
            tree.transform.localScale = proto.transform.lossyScale * Rand(0.9f, 1.8f);
            foreach (var col in tree.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            foreach (var t in tree.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            placed++;
        }
    }

    // ------------------------------------------------------------------ boundary

    static void BuildBoundary(Rect area, Transform parent)
    {
        var boundary = new GameObject("MapBoundary");
        boundary.transform.SetParent(parent, false);
        const float thickness = 4f, bottom = -60f, top = 80f;
        float h = top - bottom, cy = (top + bottom) * 0.5f;

        void Wall(string name, Vector3 center, Vector3 size)
        {
            var w = new GameObject(name) { isStatic = true };
            w.transform.SetParent(boundary.transform, false);
            w.transform.position = center;
            w.AddComponent<BoxCollider>().size = size;
        }

        float spanX = area.width + thickness * 2f, spanZ = area.height + thickness * 2f;
        Wall("West", new Vector3(area.xMin - thickness * 0.5f, cy, area.center.y), new Vector3(thickness, h, spanZ));
        Wall("East", new Vector3(area.xMax + thickness * 0.5f, cy, area.center.y), new Vector3(thickness, h, spanZ));
        Wall("South", new Vector3(area.center.x, cy, area.yMin - thickness * 0.5f), new Vector3(spanX, h, thickness));
        Wall("North", new Vector3(area.center.x, cy, area.yMax + thickness * 0.5f), new Vector3(spanX, h, thickness));
    }
}
