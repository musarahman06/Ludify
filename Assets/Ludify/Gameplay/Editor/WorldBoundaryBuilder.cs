using Ludify.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Ludify.Gameplay.Editor
{
    /// <summary>
    /// Builds the invisible walls that make bridge gating work:
    ///   • river bank walls following <see cref="WorldLayout.RiverCenterX"/>, open only where a bridge deck crosses,
    ///   • side walls along each bridge deck so nobody can step off into the river,
    ///   • perimeter walls around the 500 × 500 m map.
    /// Also removes the water plane's collider (which let the player walk on the river).
    /// Re-running replaces the previously generated walls.
    /// </summary>
    public static class WorldBoundaryBuilder
    {
        const float BankWallOffset = 24f;     // from river center; just inside the sloped bank
        const float SegmentLength = 4f;
        const float WallHeight = 12f;
        const float WallBottom = -4f;
        const float BridgeGapHalf = WorldLayout.BridgeDeckWidth * 0.5f + 0.5f;

        [MenuItem("Ludify/World/2. Build Boundaries")]
        public static void Run()
        {
            var water = GameObject.Find("RiverWater");
            if (water != null)
            {
                var mc = water.GetComponent<MeshCollider>();
                if (mc != null) Object.DestroyImmediate(mc);
            }

            var world = GetOrCreate("World", null);
            var old = world.transform.Find("Boundaries");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = GetOrCreate("Boundaries", world.transform);

            int river = BuildRiverWalls(GetOrCreate("RiverWalls", root.transform).transform);
            int decks = BuildDeckSideWalls(GetOrCreate("BridgeSideWalls", root.transform).transform);
            int edges = BuildMapBounds(GetOrCreate("MapBounds", root.transform).transform);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[WorldBoundaryBuilder] river wall segments: {river}, bridge side walls: {decks}, map edge walls: {edges}.");
        }

        static int BuildRiverWalls(Transform parent)
        {
            int count = 0;
            foreach (int side in new[] { -1, 1 })
            {
                for (float z = 0f; z < WorldLayout.MapSize; z += SegmentLength)
                {
                    float z0 = z, z1 = Mathf.Min(z + SegmentLength, WorldLayout.MapSize);
                    if (OverlapsBridge(z0, z1)) continue;

                    var a = new Vector3(WorldLayout.RiverCenterX(z0) + side * BankWallOffset, 0f, z0);
                    var b = new Vector3(WorldLayout.RiverCenterX(z1) + side * BankWallOffset, 0f, z1);
                    AddWall(parent, $"Bank{(side < 0 ? "W" : "E")}_{count}", a, b, 1f);
                    count++;
                }
            }
            return count;
        }

        static bool OverlapsBridge(float z0, float z1)
        {
            foreach (var bridge in WorldLayout.Bridges)
                if (z1 > bridge.Z - BridgeGapHalf && z0 < bridge.Z + BridgeGapHalf) return true;
            return false;
        }

        static int BuildDeckSideWalls(Transform parent)
        {
            int count = 0;
            foreach (var bridge in WorldLayout.Bridges)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float z = bridge.Z + side * (WorldLayout.BridgeDeckWidth * 0.5f + 0.25f);
                    var a = new Vector3(bridge.WestEndX, 0f, z);
                    var b = new Vector3(bridge.EastEndX, 0f, z);
                    AddWall(parent, $"{bridge.Id}_Side{(side < 0 ? "S" : "N")}", a, b, 0.5f);
                    count++;
                }
            }
            return count;
        }

        static int BuildMapBounds(Transform parent)
        {
            float s = WorldLayout.MapSize;
            AddWall(parent, "West", new Vector3(-0.5f, 0, 0), new Vector3(-0.5f, 0, s), 1f);
            AddWall(parent, "East", new Vector3(s + 0.5f, 0, 0), new Vector3(s + 0.5f, 0, s), 1f);
            AddWall(parent, "South", new Vector3(0, 0, -0.5f), new Vector3(s, 0, -0.5f), 1f);
            AddWall(parent, "North", new Vector3(0, 0, s + 0.5f), new Vector3(s, 0, s + 0.5f), 1f);
            return 4;
        }

        /// <summary>Invisible box wall from a to b (on the ground plane), extending vertically from WallBottom.</summary>
        static void AddWall(Transform parent, string name, Vector3 a, Vector3 b, float thickness)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var dir = b - a;
            float len = dir.magnitude;
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, WallBottom + WallHeight * 0.5f, 0f);
            box.size = new Vector3(thickness, WallHeight, len + 0.3f);
            go.isStatic = true;
        }

        static GameObject GetOrCreate(string name, Transform parent)
        {
            var existing = parent == null ? GameObject.Find(name) : parent.Find(name)?.gameObject;
            if (existing != null) return existing;
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
