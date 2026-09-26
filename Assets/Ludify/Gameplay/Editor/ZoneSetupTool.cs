using Ludify.Gameplay.Core;
using Ludify.Gameplay.Player;
using Ludify.Gameplay.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ludify.Gameplay.Editor
{
    /// <summary>
    /// Creates the zone trigger volumes (with fast-travel waypoints) and the _LudifySystems object
    /// (GameModeManager + ZoneRegistry), saved as a prefab so later milestones extend the prefab instead
    /// of editing the scene. Run after the collider pass so waypoints can avoid buildings.
    /// </summary>
    public static class ZoneSetupTool
    {
        const string ControlsPath = "Assets/Ludify/Gameplay/Core/LudifyControls.inputactions";
        const string SystemsPrefabPath = "Assets/Ludify/Gameplay/Prefabs/_LudifySystems.prefab";

        struct ZoneSpec
        {
            public ZoneId Id; public string Name; public Vector3 Waypoint; public float Yaw;
            public ZoneSpec(ZoneId id, string name, Vector3 wp, float yaw) { Id = id; Name = name; Waypoint = wp; Yaw = yaw; }
        }

        static readonly ZoneSpec[] Specs =
        {
            new ZoneSpec(ZoneId.Racetrack, "Racetrack", new Vector3(72f, 0.2f, 40f), 90f),
            new ZoneSpec(ZoneId.Farm, "Farm", new Vector3(150f, 0.2f, 240f), 90f),
            new ZoneSpec(ZoneId.Suburbs, "Suburbs", new Vector3(115f, 0.2f, 400f), 0f),
            new ZoneSpec(ZoneId.Downtown, "Downtown", new Vector3(400f, 0.2f, 350f), 180f),
        };

        [MenuItem("Ludify/World/3. Create Zones + Systems")]
        public static void Run()
        {
            var world = GameObject.Find("World") ?? new GameObject("World");
            var oldZones = world.transform.Find("Zones");
            if (oldZones != null) Object.DestroyImmediate(oldZones.gameObject);
            var zonesRoot = new GameObject("Zones");
            zonesRoot.transform.SetParent(world.transform, false);

            foreach (var spec in Specs)
            {
                var rect = WorldLayout.ZoneRect(spec.Id);
                var zoneGo = new GameObject($"Zone_{spec.Name}");
                zoneGo.transform.SetParent(zonesRoot.transform, false);
                zoneGo.transform.position = new Vector3(rect.center.x, 30f, rect.center.y);
                var box = zoneGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(rect.width, 80f, rect.height);

                var wpGo = new GameObject($"Waypoint_{spec.Name}");
                wpGo.transform.SetParent(zoneGo.transform, false);
                wpGo.transform.SetPositionAndRotation(FindFreeSpot(spec.Waypoint), Quaternion.Euler(0f, spec.Yaw, 0f));
                var wp = wpGo.AddComponent<FastTravelWaypoint>();
                wp.Configure(spec.Id);

                var zone = zoneGo.AddComponent<ZoneVolume>();
                zone.Configure(spec.Id, spec.Name, wp);
            }

            BuildSystems();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[ZoneSetupTool] zones + _LudifySystems created.");
        }

        /// <summary>Spiral outward from the preferred point until a player-sized capsule fits.</summary>
        static Vector3 FindFreeSpot(Vector3 preferred)
        {
            Physics.SyncTransforms();
            for (int ring = 0; ring < 12; ring++)
            {
                int steps = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < steps; i++)
                {
                    float ang = i * Mathf.PI * 2f / steps;
                    var p = preferred + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (ring * 2f);
                    if (!Physics.CheckCapsule(p + Vector3.up * 0.7f, p + Vector3.up * 1.6f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                        return p;
                }
            }
            Debug.LogWarning($"[ZoneSetupTool] no free spot near {preferred}; using it anyway.");
            return preferred;
        }

        static void BuildSystems()
        {
            var existing = GameObject.Find("_LudifySystems");
            if (existing != null) Object.DestroyImmediate(existing);

            var systems = new GameObject("_LudifySystems");
            var gmm = systems.AddComponent<GameModeManager>();
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            var so = new SerializedObject(gmm);
            so.FindProperty("controls").objectReferenceValue = controls;
            so.ApplyModifiedPropertiesWithoutUndo();

            var registry = systems.AddComponent<ZoneRegistry>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var rso = new SerializedObject(registry);
            rso.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
            rso.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SystemsPrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(systems, SystemsPrefabPath, InteractionMode.AutomatedAction);
        }
    }
}
