using System.Collections.Generic;
using Ludify.Gameplay.Core;
using UnityEngine;

namespace Ludify.Gameplay.World
{
    /// <summary>
    /// Tracks which zone the player is in by checking their position against every <see cref="ZoneVolume"/>
    /// a few times per second (works for walking, driving and teleports alike). Raises
    /// <see cref="GameEvents.ZoneEntered"/> on every change and <see cref="GameEvents.ZoneDiscovered"/> the first time.
    /// </summary>
    public sealed class ZoneRegistry : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] float checkInterval = 0.25f;

        readonly List<ZoneVolume> zones = new List<ZoneVolume>();
        readonly HashSet<ZoneId> discovered = new HashSet<ZoneId>();
        float nextCheck;

        public ZoneId? CurrentZone { get; private set; }
        public IReadOnlyList<ZoneVolume> Zones => zones;
        public bool IsDiscovered(ZoneId zone) => discovered.Contains(zone);

        public ZoneVolume Get(ZoneId zone)
        {
            foreach (var z in zones) if (z.Zone == zone) return z;
            return null;
        }

        void Awake()
        {
            zones.AddRange(FindObjectsByType<ZoneVolume>());
            // The prefab can't hold a scene reference, so fall back to finding the player.
            if (player == null)
            {
                var pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null) player = pc.transform;
            }
        }

        void Update()
        {
            if (player == null || Time.time < nextCheck) return;
            nextCheck = Time.time + checkInterval;

            ZoneId? found = null;
            foreach (var z in zones)
            {
                if (z.Contains(player.position)) { found = z.Zone; break; }
            }
            if (found == CurrentZone || found == null) return;

            CurrentZone = found;
            GameEvents.RaiseZoneEntered(found.Value);
            if (discovered.Add(found.Value)) GameEvents.RaiseZoneDiscovered(found.Value);
        }

        /// <summary>Restore discovery state from a save without re-raising events.</summary>
        public void RestoreDiscovered(IEnumerable<ZoneId> ids)
        {
            foreach (var id in ids) discovered.Add(id);
        }
    }
}
