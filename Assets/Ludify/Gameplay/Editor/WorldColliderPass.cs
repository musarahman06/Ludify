using System.Collections.Generic;
using System.Linq;
using Ludify.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Ludify.Gameplay.Editor
{
    /// <summary>
    /// One-off world preparation for CityMap: the Kenney props were imported without colliders and some
    /// buildings sit on top of the road grid. This pass
    ///   1. removes buildings that block a road,
    ///   2. adds colliders to buildings, circuit props, trees and road furniture,
    ///   3. drops the bridge decks so their road surface sits flush with the streets, and makes them walkable/drivable.
    /// Safe to re-run: objects that already have a collider are skipped.
    /// </summary>
    public static class WorldColliderPass
    {
        static readonly string[] BuildingGroups = { "DowntownBuildings", "InnerOutskirtsBuildings", "CityOutskirtsBuildings", "SuburbHouses" };
        static readonly string[] RoadGroups = { "RoadNetwork", "RoadConnectors" };

        /// <summary>Height of the road-bridge tile's driving surface above its pivot (measured).</summary>
        const float BridgeSurfaceHeight = 4.08f;
        /// <summary>Street surface height the bridge decks are lowered to meet.</summary>
        const float StreetSurfaceY = 0.2f;

        [MenuItem("Ludify/World/1. Add World Colliders")]
        public static void Run()
        {
            int removed = RemoveBuildingsOnRoads();
            int colliders = 0;

            foreach (var group in BuildingGroups) colliders += AddBoxColliders(group, trunkOnly: false);
            colliders += AddBoxColliders("RiverTrees", trunkOnly: true);

            var farm = GameObject.Find("FarmHouse");
            if (farm != null)
            {
                foreach (Transform child in farm.transform)
                {
                    if (child.name.Contains("fence")) colliders += AddMeshColliders(child.gameObject);
                    else colliders += AddBox(child.gameObject, child.name.Contains("tree"));
                }
            }

            var circuit = GameObject.Find("F1Circuit");
            if (circuit != null)
            {
                foreach (var sub in new[] { "Barriers", "PitLane", "DebrisFence", "Grandstands", "GridCars" })
                {
                    var t = circuit.transform.Find(sub);
                    if (t != null) colliders += AddBoxColliders(t.gameObject, trunkOnly: false);
                }
                // The start gantry spans the track: a box would block cars, so use its real mesh.
                foreach (Transform child in circuit.transform)
                {
                    if (child.name.StartsWith("overheadLights")) colliders += AddMeshColliders(child.gameObject);
                    if (child.name.StartsWith("flagCheckers")) colliders += AddBox(child.gameObject, trunk: true);
                }
            }

            var props = GameObject.Find("RoadProps");
            if (props != null) foreach (Transform child in props.transform) colliders += AddMeshColliders(child.gameObject);

            int bridges = PrepareBridgeDecks();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[WorldColliderPass] removed {removed} road-blocking buildings, added {colliders} colliders, prepared {bridges} bridge decks.");
        }

        // ---------------------------------------------------------------- buildings on roads

        public static int RemoveBuildingsOnRoads(bool dryRun = false)
        {
            var roads = new List<Rect>();
            foreach (var groupName in RoadGroups)
            {
                var group = GameObject.Find(groupName);
                if (group == null) continue;
                foreach (var r in group.GetComponentsInChildren<Renderer>())
                {
                    var b = r.bounds;
                    roads.Add(Rect.MinMaxRect(b.min.x + 0.5f, b.min.z + 0.5f, b.max.x - 0.5f, b.max.z - 0.5f));
                }
            }

            int removed = 0;
            foreach (var groupName in BuildingGroups)
            {
                var group = GameObject.Find(groupName);
                if (group == null) continue;
                var doomed = new List<GameObject>();
                foreach (Transform building in group.transform)
                {
                    var foot = FootprintXZ(building.gameObject, inset: 1f);
                    if (roads.Any(r => r.Overlaps(foot))) doomed.Add(building.gameObject);
                }
                removed += doomed.Count;
                if (!dryRun) foreach (var go in doomed) Object.DestroyImmediate(go);
            }
            return removed;
        }

        static Rect FootprintXZ(GameObject go, float inset)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return Rect.zero;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            return Rect.MinMaxRect(b.min.x + inset, b.min.z + inset, b.max.x - inset, b.max.z - inset);
        }

        // ---------------------------------------------------------------- colliders

        static int AddBoxColliders(string groupName, bool trunkOnly)
        {
            var group = GameObject.Find(groupName);
            return group == null ? 0 : AddBoxColliders(group, trunkOnly);
        }

        static int AddBoxColliders(GameObject group, bool trunkOnly)
        {
            int n = 0;
            foreach (Transform child in group.transform) n += AddBox(child.gameObject, trunkOnly);
            return n;
        }

        /// <summary>Box fitted to the object's meshes in its own local space (so rotation and scale are respected).</summary>
        static int AddBox(GameObject go, bool trunk)
        {
            if (go.GetComponentInChildren<Collider>() != null) return 0;
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return 0;

            bool first = true;
            var local = new Bounds();
            foreach (var mf in filters)
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

            var box = go.AddComponent<BoxCollider>();
            if (trunk)
            {
                // Trees and poles: only the trunk blocks, so players and cars can pass under canopies.
                float w = Mathf.Max(0.3f, Mathf.Min(local.size.x, local.size.z) * 0.25f);
                box.center = new Vector3(local.center.x, local.center.y, local.center.z);
                box.size = new Vector3(w, local.size.y, w);
            }
            else
            {
                box.center = local.center;
                box.size = local.size;
            }
            return 1;
        }

        static int AddMeshColliders(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return 0;
            int n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- bridges

        static int PrepareBridgeDecks()
        {
            int n = 0;
            foreach (var bridge in WorldLayout.Bridges)
            {
                var deck = GameObject.Find(bridge.DeckObjectName);
                if (deck == null) { Debug.LogWarning($"[WorldColliderPass] bridge deck '{bridge.DeckObjectName}' not found"); continue; }

                // Lower the deck once so its driving surface meets the street surface.
                float targetY = StreetSurfaceY - BridgeSurfaceHeight;
                if (Mathf.Abs(deck.transform.position.y - targetY) > 0.01f)
                {
                    var p = deck.transform.position;
                    deck.transform.position = new Vector3(p.x, targetY, p.z);
                }
                n += AddMeshColliders(deck) > 0 ? 1 : 0;
            }
            return n;
        }
    }
}
