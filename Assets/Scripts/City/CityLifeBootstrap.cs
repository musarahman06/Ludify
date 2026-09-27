using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Brings the city to life when CityMap loads (no scene edits): recolors the city buildings, builds a walkable
/// NavMesh over the east bank, spawns residents who wander the streets, and installs the quest system + HUD.
/// </summary>
public static class CityLifeBootstrap
{
    const int ResidentCount = 16;
    const string RootName = "_CityLife";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // See RuntimeWorldColliders: sceneLoaded isn't raised when entering Play Mode without a scene reload.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SetUpLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++) SetUp(SceneManager.GetSceneAt(i));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUp(scene);

    static void SetUp(Scene scene)
    {
        if (!scene.isLoaded || GameObject.Find("DowntownBuildings") == null) return;   // not CityMap
        if (GameObject.Find(RootName) != null) return;                                 // already set up
        var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null || player.visualRoot == null) return;

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        CityColorizer.Apply();
        BuildNavMesh(root);

        QuestTargets.SetTemplate(player.visualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial);

        var residents = new List<Npc>();
        var npcParent = new GameObject("Residents").transform;
        npcParent.SetParent(root.transform, false);
        for (int attempt = 0; attempt < ResidentCount * 10 && residents.Count < ResidentCount; attempt++)
        {
            Vector3 p = CityArea.RandomPoint();
            if (!NavMesh.SamplePosition(new Vector3(p.x, 0.5f, p.z), out var hit, 3f, NavMesh.AllAreas)) continue;
            if (!CityArea.Contains(hit.position)) continue;
            residents.Add(NpcFactory.Create(player, hit.position, npcParent));
        }

        var dialogue = DialogueBox.Create();
        dialogue.transform.SetParent(root.transform, false);
        var hud = CityHud.Create();
        hud.transform.SetParent(root.transform, false);
        root.AddComponent<QuestManager>().Init(residents, dialogue, hud, player);

        Debug.Log($"[CityLife] {residents.Count} residents walking around the city.");
    }

    static void BuildNavMesh(GameObject root)
    {
        float started = Time.realtimeSinceStartup;
        var surface = root.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        // East bank only (the river curves, so leave a margin; CityArea.Contains trims the rest).
        var min = new Vector3(CityArea.MinX - 20f, -5f, CityArea.MinZ - 5f);
        var max = new Vector3(CityArea.MaxX + 5f, 15f, CityArea.MaxZ + 5f);
        surface.center = (min + max) * 0.5f;
        surface.size = max - min;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.3f;
        surface.BuildNavMesh();
        Debug.Log($"[CityLife] City NavMesh built in {(Time.realtimeSinceStartup - started) * 1000f:0} ms.");
    }
}
