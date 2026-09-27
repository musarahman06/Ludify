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
