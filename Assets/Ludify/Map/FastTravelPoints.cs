using System.Collections.Generic;
using UnityEngine;

namespace Ludify.Map
{
    public sealed class FastTravelPoint
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Facing = Quaternion.identity;
        /// <summary>If set, the player turns to face this on arrival (e.g. the building).</summary>
        public Vector3? LookAt;
        /// <summary>Facing actually used by the last arrival (set by <see cref="FastTravel"/>).</summary>
        public Quaternion ArrivalFacing = Quaternion.identity;
        public Color Color;
        /// <summary>Text on the map icon. Defaults to the first letter of <see cref="Name"/>.</summary>
        public string Glyph;
        /// <summary>If set, the point moves with this object (see <see cref="MapMarkers"/>).</summary>
        public Transform Follow;
        /// <summary>If above 0, the maps also draw a translucent circle of this radius (metres), e.g. a quest search area.</summary>
        public float Radius;

        public string IconText => string.IsNullOrEmpty(Glyph) ? Name.Substring(0, 1) : Glyph;
    }

    /// <summary>
    /// The fast-travel destinations. Resolved from the live scene at startup, so they follow
    /// the world as it changes. To add one, append to <see cref="Resolve"/>, e.g.
    /// <c>AddAtObject(points, "Farm", "FarmHouse", Color.yellow);</c>
    /// </summary>
    public static class FastTravelPoints
    {
        public static List<FastTravelPoint> Resolve(Vector3 spawnPosition, Quaternion spawnRotation, Rect mapArea)
        {
            var points = new List<FastTravelPoint>
            {
                new FastTravelPoint
                {
                    Name = "Racetrack",
                    Position = spawnPosition,
                    Facing = spawnRotation,
                    Color = new Color(0.9f, 0.25f, 0.25f),
                },
            };

            FastTravelPoint skyscraper = TallestBuilding("Skyscraper", "DowntownBuildings", mapArea.center, new Color(0.3f, 0.6f, 1f));
            if (skyscraper != null) points.Add(skyscraper);

            return points;
        }

        /// <summary>A point in front of the named scene object (any root or child name).</summary>
        public static void AddAtObject(List<FastTravelPoint> points, string name, string objectName, Color color)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogWarning($"[Ludify.Map] Fast-travel point \"{name}\": no object named \"{objectName}\" in the scene.");
                return;
            }
            points.Add(new FastTravelPoint { Name = name, Position = go.transform.position, Color = color });
        }

        /// <summary>
        /// The tallest building under <paramref name="parentName"/> (or in the whole scene if missing).
        /// The landing spot is on the ground just outside the building, on the side facing the map centre.
        /// </summary>
        static FastTravelPoint TallestBuilding(string name, string parentName, Vector2 mapCenter, Color color)
        {
            GameObject parent = GameObject.Find(parentName);
            Renderer[] renderers = parent != null
                ? parent.GetComponentsInChildren<MeshRenderer>()
                : Object.FindObjectsByType<MeshRenderer>();

            Renderer tallest = null;
            foreach (Renderer r in renderers)
                if (tallest == null || r.bounds.max.y > tallest.bounds.max.y) tallest = r;
            if (tallest == null)
            {
                Debug.LogWarning($"[Ludify.Map] Fast-travel point \"{name}\": no buildings found.");
                return null;
            }

            // Whole building footprint (all renderers of its top-level object under the parent).
            Transform root = tallest.transform;
            while (root.parent != null && root.parent.gameObject != parent) root = root.parent;
            Bounds footprint = tallest.bounds;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) footprint.Encapsulate(r.bounds);

            Vector2 center = new Vector2(footprint.center.x, footprint.center.z);
            Vector2 toMapCenter = (mapCenter - center).sqrMagnitude > 1f ? (mapCenter - center).normalized : Vector2.down;
            float outside = Mathf.Max(footprint.extents.x, footprint.extents.z) + 4f;
            Vector2 spot = center + toMapCenter * outside;

            return new FastTravelPoint
            {
                Name = name,
                Position = new Vector3(spot.x, footprint.min.y, spot.y),
                Facing = Quaternion.LookRotation(new Vector3(-toMapCenter.x, 0, -toMapCenter.y)),
                LookAt = footprint.center, // face the building from wherever the player lands
                Color = color,
            };
        }
    }
}
