using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds CityMap's world colliders and surroundings (arched bridges, hills with trees, map boundary) to the
/// in-memory scene when entering Play Mode and when building, before static batching combines the meshes.
/// The saved scene file is never changed.
/// </summary>
class WorldCollidersSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        int added = RuntimeWorldColliders.AddWorldColliders(scene);
        if (added > 0) Debug.Log($"[WorldCollidersSceneProcessor] Added {added} colliders to '{scene.name}'.");

        var assets = LoadDressingAssets();
        if (assets != null) WorldDressing.Build(scene, assets);

        var farm = LoadFarmAssets();
        if (farm != null) FarmDressing.Build(scene, farm);
    }

    static readonly string[] KenneyNature =
    {
        "crops_cornStageA", "crops_cornStageC", "crops_wheatStageA", "crops_wheatStageB",
        "plant_bush", "plant_bushLarge", "flower_yellowA", "flower_redA", "flower_purpleA",
        "tree_default", "tree_oak", "rock_smallA", "rock_smallB", "rock_smallC", "rock_largeA",
    };
    static readonly string[] QuaterniusFarm = { "BigBarn", "Barn", "Silo", "TowerWindmill", "ChickenCoop", "Fence2" };

    static FarmDressing.Assets LoadFarmAssets()
    {
        var a = new FarmDressing.Assets
        {
            LitTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Terrain/Mat_Bridge_Concrete.mat"),
            Green = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layers/Z_Green.terrainlayer"),
            Dirt = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layers/Z_Farm.terrainlayer"),
        };
        foreach (var n in KenneyNature)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Kenney/NatureKit/Models/{n}.fbx");
            if (go != null) a.Models[n] = go;
        }
        foreach (var n in QuaterniusFarm)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Quaternius/FarmBuildings/{n}.obj");
            if (go != null) a.Models[n] = go;
        }
        if (a.LitTemplate == null) { Debug.LogWarning("[WorldCollidersSceneProcessor] Mat_Bridge_Concrete missing; skipping farm dressing."); return null; }
        if (!a.Models.ContainsKey("BigBarn")) Debug.LogWarning("[WorldCollidersSceneProcessor] Quaternius farm buildings not imported yet; the farm gets greenery but no barn.");
        return a;
    }

    static WorldDressing.Assets LoadDressingAssets()
    {
        Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Terrain/{name}.mat");
        var assets = new WorldDressing.Assets
        {
            road = Mat("Mat_Road"),
            fallbackBridge = Mat("Mat_Bridge_Concrete"),
            hillLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Terrain/Layers/Z_Green.terrainlayer"),
        };
        if (assets.road == null || assets.fallbackBridge == null)
        {
            Debug.LogWarning("[WorldCollidersSceneProcessor] Bridge materials missing in Assets/Terrain; skipping world dressing.");
            return null;
        }
        assets.bridgeByDeckName["S1_Bridge_RoadDeck"] = Mat("Mat_Bridge_Concrete");
        assets.bridgeByDeckName["B2_TrussBridge_RoadDeck"] = Mat("Mat_Bridge_Truss");
        assets.bridgeByDeckName["B3_ArchBridge_RoadDeck"] = Mat("Mat_Bridge_Stone");
        assets.bridgeByDeckName["SuspensionBridge_RoadDeck"] = Mat("Mat_Bridge_Modern");
        return assets;
    }
}
