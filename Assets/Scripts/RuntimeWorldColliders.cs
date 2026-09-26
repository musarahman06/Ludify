using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Automatic CityMap set-up (no scene edits or menu steps needed):
///   1. <see cref="AddWorldColliders"/> adds colliders to buildings, trees, circuit barriers/stands and road props
///      (placed without any) and to the track surface. It is called by the editor scene processor
///      (Editor/WorldCollidersSceneProcessor.cs) before static batching, on entering Play Mode and in builds.
///   2. On scene load the grid cars are made driveable and X-to-drive is hooked up for the player.
/// Never deletes or moves scene objects; anything that already has a collider is left alone.
/// </summary>
public static class RuntimeWorldColliders
{
    static readonly string[] BuildingGroups = { "DowntownBuildings", "InnerOutskirtsBuildings", "CityOutskirtsBuildings", "SuburbHouses" };
    static readonly string[] CircuitSolidGroups = { "Barriers", "PitLane", "DebrisFence", "Grandstands" };
    static readonly string[] TrackSurfaces = { "Track_Asphalt", "PitLane_Asphalt" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // sceneLoaded isn't raised when entering Play Mode with "Reload Scene" disabled, so also set up
    // whatever is already loaded once Play starts. SetUpScene is idempotent, so running both is safe.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetUpLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) SetUpScene(SceneManager.GetSceneAt(i));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUpScene(scene);

    static void SetUpScene(Scene scene)
    {
        if (!scene.isLoaded) return;
        var circuit = FindInScene(scene, "F1Circuit");
        if (circuit == null) return;   // not CityMap
        if (FindInScene(scene, "_VehicleInteraction") != null) return;   // already set up

        int cars = SetUpCars(scene, circuit);
        Debug.Log($"[RuntimeWorldColliders] Made {cars} cars driveable.");
    }

    /// <summary>
    /// Adds world colliders. Must run before static batching (mesh bounds are lost once meshes are combined),
    /// so objects that are already batched are skipped. Returns the number of colliders added.
    /// </summary>
    public static int AddWorldColliders(Scene scene)
    {
        var circuit = FindInScene(scene, "F1Circuit");
        if (circuit == null) return 0;   // not CityMap

        int colliders = 0;
        foreach (var group in BuildingGroups) colliders += AddBoxToChildren(FindInScene(scene, group), trunk: false);
        colliders += AddBoxToChildren(FindInScene(scene, "RiverTrees"), trunk: true);
        // Traffic lights and signs overhang the road, so only their poles block.
        colliders += AddBoxToChildren(FindInScene(scene, "RoadProps"), trunk: true);

        var farm = FindInScene(scene, "FarmHouse");
        if (farm != null)
        {
            foreach (Transform child in farm.transform)
            {
                // A box would fill the fenced paddock, so fences use their real shape.
                if (child.name.Contains("fence"))
                    foreach (var mf in child.GetComponentsInChildren<MeshFilter>()) colliders += AddMeshCollider(mf.gameObject);
                else
                    colliders += AddBox(child.gameObject, trunk: child.name.Contains("tree"));
            }
        }

        foreach (var sub in CircuitSolidGroups)
        {
            var t = circuit.transform.Find(sub);
            if (t != null) colliders += AddBoxToChildren(t.gameObject, trunk: false);
        }

        foreach (var surfaceName in TrackSurfaces)
        {
            var t = circuit.transform.Find(surfaceName);
            if (t != null) colliders += AddMeshCollider(t.gameObject);
        }
        return colliders;
    }

    static int SetUpCars(Scene scene, GameObject circuit)
    {
        var grid = circuit.transform.Find("GridCars");
        int count = 0;
        if (grid != null)
        {
            // Copy the list first: making a car driveable re-parents it.
            var models = new Transform[grid.childCount];
            for (int i = 0; i < models.Length; i++) models[i] = grid.GetChild(i);
            foreach (var model in models)
            {
                if (model.GetComponent<CarController>() != null) { count++; continue; }
                if (CarController.MakeDriveable(model) != null) count++;
            }
        }

        var player = Object.FindAnyObjectByType<PlayerController>();
        var cam = Object.FindAnyObjectByType<OrbitCamera>();
        if (count > 0 && player != null && cam != null)
        {
            var go = new GameObject("_VehicleInteraction");
            SceneManager.MoveGameObjectToScene(go, scene);
            var vi = go.AddComponent<VehicleInteraction>();
            vi.player = player.gameObject;
            vi.orbitCamera = cam;

            // Getting into a car on the circuit starts the Knowledge Time Trial.
            var tt = new GameObject("_TimeTrial");
            SceneManager.MoveGameObjectToScene(tt, scene);
            tt.AddComponent<TimeTrialManager>();
        }
        return count;
    }

    static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        }
        return null;
    }

    static int AddBoxToChildren(GameObject group, bool trunk)
    {
        if (group == null) return 0;
        int n = 0;
        foreach (Transform child in group.transform) n += AddBox(child.gameObject, trunk);
        return n;
    }

    /// <summary>Box fitted to the object's meshes in its own local space, so rotation and scale are respected.</summary>
    static int AddBox(GameObject go, bool trunk)
    {
        if (go.GetComponentInChildren<Collider>() != null) return 0;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            if (r.isPartOfStaticBatch) return 0;

        bool first = true;
        var local = new Bounds();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mb.min.x : mb.max.x,
                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var p = go.transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                else local.Encapsulate(p);
            }
        }
        if (first) return 0;

        var box = go.AddComponent<BoxCollider>();
        box.center = local.center;
        if (trunk)
        {
            // Trees and poles: only the trunk blocks, so the camera isn't pushed around by canopies.
            // Kenney models pivot at the base of the trunk/pole, so centre the box there when it lies inside the model.
            float w = Mathf.Max(0.3f, Mathf.Min(local.size.x, local.size.z) * 0.25f);
            bool pivotInside = local.min.x <= 0f && local.max.x >= 0f && local.min.z <= 0f && local.max.z >= 0f;
            if (pivotInside) box.center = new Vector3(0f, local.center.y, 0f);
            box.size = new Vector3(w, local.size.y, w);
        }
        else
        {
            box.size = local.size;
        }
        return 1;
    }

    static int AddMeshCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() != null) return 0;
        var mf = go.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return 0;
        var mr = go.GetComponent<Renderer>();
        if (mr != null && mr.isPartOfStaticBatch) return 0;
        go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        return 1;
    }
}
