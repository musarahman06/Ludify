using System.Collections.Generic;
using UnityEngine;
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
        CityNav.Build(root);

        QuestTargets.SetTemplate(player.visualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial);

        var residents = new List<Npc>();
        var npcParent = new GameObject("Residents").transform;
        npcParent.SetParent(root.transform, false);
        for (int attempt = 0; attempt < ResidentCount * 10 && residents.Count < ResidentCount; attempt++)
        {
            Vector3? p = CityNav.RandomPoint();
            if (p.HasValue) residents.Add(NpcFactory.Create(player, p.Value, npcParent));
        }

        // After the residents are cloned, so they don't copy the player's outfit.
        ClothingShop.Build(player, root.transform);
        Wardrobe.ApplyEquipped(player.visualRoot);
        StoreView.Create(player).transform.SetParent(root.transform, false);

        var dialogue = DialogueBox.Create();
        dialogue.transform.SetParent(root.transform, false);
        var hud = CityHud.Create();
        hud.transform.SetParent(root.transform, false);
        root.AddComponent<QuestManager>().Init(residents, dialogue, hud, player);

        Debug.Log($"[CityLife] {residents.Count} residents walking around the city.");
    }
}
