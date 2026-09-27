using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Installs the suburb/farm jobs when CityMap loads (no scene edits). Waits for City Life to finish setting up
/// (it provides the dialogue box and HUD), then places the givers:
/// suburbs: Paperboy Pete (newspaper route by bike), Mrs. Green (lawn mowing);
/// farm: Farmer Joe (harvest), Rosa the farmhand (raking leaves), Gardener Sam (planting).
/// </summary>
public static class JobsBootstrap
{
    const string RootName = "_Jobs";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded isn't raised when entering Play Mode without a scene reload (see RuntimeWorldColliders).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetUpLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) SetUp(SceneManager.GetSceneAt(i));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUp(scene);

    static void SetUp(Scene scene)
    {
        if (!scene.isLoaded || GameObject.Find("SuburbHouses") == null || GameObject.Find("FarmHouse") == null) return;
        if (GameObject.Find(RootName) != null) return;
        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<Installer>();
    }

    /// <summary>Waits a few frames for City Life's DialogueBox and HUD, then builds everything.</summary>
    sealed class Installer : MonoBehaviour
    {
        float giveUpAt;

        void Start() => giveUpAt = Time.realtimeSinceStartup + 15f;

        void Update()
        {
            var dialogue = FindAnyObjectByType<DialogueBox>();
            var cityHud = FindAnyObjectByType<CityHud>();
            var player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (dialogue == null || cityHud == null || player == null || player.visualRoot == null)
            {
                if (Time.realtimeSinceStartup > giveUpAt) { Debug.LogWarning("[Jobs] City Life UI not found; jobs not installed."); Destroy(this); }
                return;
            }
            Build(gameObject, player, dialogue, cityHud);
            Destroy(this);
        }
    }

    static void Build(GameObject root, PlayerController player, DialogueBox dialogue, CityHud cityHud)
    {
        JobProps.Init(player);
        var hud = JobHud.Create();
        hud.transform.SetParent(root.transform, false);
        var manager = root.AddComponent<JobManager>();
        manager.Init(player, dialogue, cityHud, hud);
        var people = new GameObject("JobGivers").transform;
        people.SetParent(root.transform, false);

        // ---- Suburbs
        var houses = JobPlaces.SuburbHouses();
        if (houses.Count > 0)
        {
            Vector3 suburbCenter = Vector3.zero;
            foreach (var h in houses) suburbCenter += h.Bounds.center;
            suburbCenter /= houses.Count;

            // Pete waits by the suburb entrance, on the southern edge nearest the main road.
            Vector3 entrance = JobPlaces.SouthEdgeNearSpine(houses);
            var pete = Giver(player, people, "Paperboy Pete", entrance, Vector3.forward);
            manager.AddGiver(pete, () => new NewspaperJob(houses));

            // Mrs. Green's house: a roomy front garden near the middle of the suburbs.
            var lawnHouse = JobPlaces.BestLawnHouse(houses, suburbCenter);
            Vector3 across = Vector3.Cross(Vector3.up, lawnHouse.FacingDir);
            Vector3 greenSpot = lawnHouse.Edge + lawnHouse.FacingDir * 1.2f + across * (Mathf.Clamp(lawnHouse.WidthAcross, 8f, 14f) * 0.5f + 1.5f);
            var green = Giver(player, people, "Mrs. Green", greenSpot, -across);
            manager.AddGiver(green, () => new LawnMowingJob(lawnHouse));
        }

        // ---- Farm
        Vector3 farmhouse = JobPlaces.Farmhouse();
        var avoid = JobProps.RendererBounds("FarmCrops");
        avoid.AddRange(JobProps.RendererBounds("FarmHouse"));
        avoid.AddRange(JobProps.RendererBounds(FarmDressing.RootName));
        Vector3 joeSpot = JobProps.ClearSpot(farmhouse + new Vector3(-8f, 0f, -8f), new Vector3(0.5f, 1f, 0.5f), avoid);
        Vector3 rosaSpot = JobProps.ClearSpot(farmhouse + new Vector3(-14f, 0f, -4f), new Vector3(0.5f, 1f, 0.5f), avoid);
        Vector3 samSpot = JobProps.ClearSpot(farmhouse + new Vector3(-4f, 0f, -14f), new Vector3(0.5f, 1f, 0.5f), avoid);
        var joe = Giver(player, people, "Farmer Joe", joeSpot, Vector3.back);
        var rosa = Giver(player, people, "Rosa the farmhand", rosaSpot, Vector3.back);
        var sam = Giver(player, people, "Gardener Sam", samSpot, Vector3.back);
        // The farm dressing's barnyard (if built) is where the leaves blow about.
        var yardMarker = GameObject.Find("FarmyardCenter");
        Vector3 yard = yardMarker != null ? yardMarker.transform.position
            : JobProps.ClearSpot(farmhouse + new Vector3(-20f, 0f, -12f), new Vector3(4f, 0.6f, 4f), JobProps.RendererBounds("FarmHouse"));
        manager.AddGiver(joe, () => new HarvestJob(farmhouse));
        manager.AddGiver(rosa, () => new RakeLeavesJob(yard));
        manager.AddGiver(sam, () => new PlantingJob(farmhouse));

        Debug.Log($"[Jobs] Suburb and farm jobs ready ({houses.Count} suburb houses).");
    }

    static Npc Giver(PlayerController player, Transform parent, string name, Vector3 spot, Vector3 facing)
    {
        var npc = NpcFactory.Create(player, JobProps.Ground(spot), parent);
        npc.DisplayName = name;
        npc.name = "JobGiver_" + name;
        npc.transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward);
        npc.MakeStationary();   // the west bank has no NavMesh
        return npc;
    }
}

/// <summary>Where things are on the west bank: suburb houses (with their road-facing side) and the farmhouse.</summary>
public static class JobPlaces
{
    public sealed class House
    {
        public Bounds Bounds;
        /// <summary>Unit direction from the house toward its nearest road (snapped to x or z).</summary>
        public Vector3 FacingDir;
        /// <summary>Ground point on the house's road-facing wall.</summary>
        public Vector3 Edge;
        /// <summary>Mailbox spot: between the wall and the road, near the road.</summary>
        public Vector3 Front;
        public float WidthAcross;
        public float RoadDistance;
    }

    public static List<House> SuburbHouses()
    {
        var list = new List<House>();
        var group = GameObject.Find("SuburbHouses");
        if (group == null) return list;
        var roads = JobProps.RendererBounds("Roads_Suburb");
        roads.AddRange(JobProps.RendererBounds("RoadConnectors"));

        foreach (Transform t in group.transform)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            var b = JobProps.BoundsOf(t.gameObject);
            Vector3 c = new Vector3(b.center.x, 0f, b.center.z);

            // Nearest road surface point.
            Vector3 best = c + Vector3.forward * 10f;
            float bestD = float.MaxValue;
            foreach (var rb in roads)
            {
                var p = rb.ClosestPoint(new Vector3(c.x, rb.center.y, c.z));
                p.y = 0f;
                float d = (p - c).sqrMagnitude;
                if (d > 0.01f && d < bestD) { bestD = d; best = p; }
            }
            Vector3 dir = best - c;
            dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dir.z));
            float halfDepth = Mathf.Abs(dir.x) > 0f ? b.extents.x : b.extents.z;
            float across = Mathf.Abs(dir.x) > 0f ? b.size.z : b.size.x;
            Vector3 edge = JobProps.Ground(c + dir * halfDepth);
            float roadDist = Mathf.Max(0f, Vector3.Dot(best - c, dir) - halfDepth);
            Vector3 front = JobProps.Ground(edge + dir * Mathf.Clamp(roadDist - 1.5f, 1.5f, 9f));
            list.Add(new House { Bounds = b, FacingDir = dir, Edge = edge, Front = front, WidthAcross = across, RoadDistance = roadDist });
        }
        return list;
    }

    /// <summary>A point just south of the suburbs, by the north–south spine road if there is one.</summary>
    public static Vector3 SouthEdgeNearSpine(List<House> houses)
    {
        float minZ = float.MaxValue, sumX = 0f;
        foreach (var h in houses) { minZ = Mathf.Min(minZ, h.Bounds.min.z); sumX += h.Bounds.center.x; }
        float x = sumX / houses.Count;
        var spine = GameObject.Find("WestSpine");
        if (spine != null) x = JobProps.BoundsOf(spine).max.x + 2.5f;
        return JobProps.ClearSpot(new Vector3(x, 0f, minZ - 4f), new Vector3(0.5f, 1f, 0.5f), null, 20f);
    }

    /// <summary>The house near the middle of the suburbs with the deepest, widest front garden.</summary>
    public static House BestLawnHouse(List<House> houses, Vector3 center)
    {
        House best = houses[0];
        float bestScore = float.MinValue;
        foreach (var h in houses)
        {
            float score = Mathf.Min(h.RoadDistance, 8f) * 3f + Mathf.Min(h.WidthAcross, 14f) - Vector3.Distance(h.Bounds.center, center) * 0.05f;
            if (score > bestScore) { bestScore = score; best = h; }
        }
        return best;
    }

    public static Vector3 Farmhouse()
    {
        var farm = GameObject.Find("FarmHouse");
        if (farm == null) return new Vector3(190f, 0f, 280f);
        foreach (Transform t in farm.transform)
            if (t.name.StartsWith("Farmhouse")) return JobProps.Ground(t.position);
        return JobProps.Ground(farm.transform.GetChild(0).position);
    }
}
