using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds CityMap's world colliders to the in-memory scene when entering Play Mode and when building,
/// before static batching combines the meshes. The saved scene file is never changed.
/// </summary>
class WorldCollidersSceneProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        int added = RuntimeWorldColliders.AddWorldColliders(scene);
        if (added > 0) Debug.Log($"[WorldCollidersSceneProcessor] Added {added} colliders to '{scene.name}'.");
    }
}
