using Ludify.Gameplay.Core;
using UnityEngine;

namespace Ludify.Gameplay.World
{
    /// <summary>
    /// Marks a zone's extent with a trigger box. <see cref="ZoneRegistry"/> checks the player's
    /// position against these bounds; the collider is a trigger so nothing physically collides with it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ZoneVolume : MonoBehaviour
    {
        [SerializeField] ZoneId zone;
        [SerializeField] string displayName;
        [SerializeField] FastTravelWaypoint waypoint;

        BoxCollider box;

        public ZoneId Zone => zone;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? zone.ToString() : displayName;
        public FastTravelWaypoint Waypoint => waypoint;
        public Bounds Bounds => box != null ? box.bounds : GetComponent<BoxCollider>().bounds;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        public bool Contains(Vector3 worldPosition)
        {
            var b = Bounds;
            return worldPosition.x >= b.min.x && worldPosition.x <= b.max.x
                && worldPosition.z >= b.min.z && worldPosition.z <= b.max.z;
        }

        public void Configure(ZoneId id, string name, FastTravelWaypoint travelPoint)
        {
            zone = id;
            displayName = name;
            waypoint = travelPoint;
        }

        void OnDrawGizmos()
        {
            var c = GetComponent<BoxCollider>();
            if (c == null) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(c.bounds.center, c.bounds.size);
        }
    }
}
