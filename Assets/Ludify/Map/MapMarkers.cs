using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludify.Map
{
    /// <summary>
    /// Extra map markers added by gameplay at runtime (e.g. NPCs with a quest). They show on the minimap and
    /// the full map like the fixed fast-travel points, can be clicked to fast travel, and follow
    /// <see cref="FastTravelPoint.Follow"/> if it's set (you land just in front of it, facing it).
    /// </summary>
    public static class MapMarkers
    {
        const float LandInFront = 2.5f;

        static readonly List<FastTravelPoint> markers = new List<FastTravelPoint>();

        public static IReadOnlyList<FastTravelPoint> All => markers;

        /// <summary>Raised when a marker is added or removed, so the map views can rebuild their icons.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            markers.Clear();
            Changed = null;
        }

        public static void Add(FastTravelPoint point)
        {
            if (point == null || markers.Contains(point)) return;
            UpdatePosition(point);
            markers.Add(point);
            Changed?.Invoke();
        }

        public static void Remove(FastTravelPoint point)
        {
            if (markers.Remove(point)) Changed?.Invoke();
        }

        /// <summary>Moves markers that follow something (called by the map views every frame).</summary>
        public static void UpdatePositions()
        {
            foreach (FastTravelPoint point in markers) UpdatePosition(point);
        }

        static void UpdatePosition(FastTravelPoint point)
        {
            Transform follow = point.Follow;
            if (follow == null) return;
            point.Position = follow.position + follow.forward * LandInFront;
            point.LookAt = follow.position;
        }
    }
}
