using UnityEngine;

namespace Ludify.Map
{
    /// <summary>Moves the on-foot player safely to a destination (onto the ground, not inside geometry).</summary>
    public static class FastTravel
    {
        const float SearchStep = 2.5f;
        const int SearchRings = 10;
        /// <summary>Preferred free space around the landing spot, so the camera has room behind the player.</summary>
        const float RoomyRadius = 3f;

        /// <summary>Null if travel is allowed, otherwise a reason to show the player.</summary>
        public static string BlockedReason(PlayerController player)
        {
            if (player == null) return "No player found.";
            if (!player.gameObject.activeInHierarchy) return "Get out of the car to fast travel (press X).";
            return null;
        }

        public static bool TryTravel(PlayerController player, FastTravelPoint point)
        {
            var controller = player.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius : 0.5f;
            float height = controller != null ? controller.height : 2f;

            if (!FindStandingSpot(point.Position, radius, height, player.transform, out Vector3 spot))
            {
                Debug.LogWarning($"[Ludify.Map] No free ground near \"{point.Name}\" ({point.Position}).");
                return false;
            }

            Quaternion facing = point.Facing;
            if (point.LookAt.HasValue)
            {
                Vector3 look = point.LookAt.Value - spot;
                look.y = 0;
                if (look.sqrMagnitude > 0.01f) facing = Quaternion.LookRotation(look);
            }
            point.ArrivalFacing = facing;

            // A CharacterController overrides transform changes while enabled.
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(spot + Vector3.up * 0.05f, facing);
            Physics.SyncTransforms();
            if (controller != null) controller.enabled = wasEnabled;
            return true;
        }

        /// <summary>Ground at or near <paramref name="target"/> with room for a standing capsule.</summary>
        static bool FindStandingSpot(Vector3 target, float radius, float height, Transform ignore, out Vector3 spot)
        {
            // Prefer open ground (roads, plazas); settle for any spot the player fits in.
            return Search(target, RoomyRadius, height, ignore, out spot)
                || Search(target, radius, height, ignore, out spot);
        }

        static bool Search(Vector3 target, float radius, float height, Transform ignore, out Vector3 spot)
        {
            for (int ring = 0; ring <= SearchRings; ring++)
            {
                int samples = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2 / samples;
                    Vector3 probe = target + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * ring * SearchStep;
                    if (GroundAt(probe, ignore, out spot) && IsClear(spot, radius, height, ignore))
                        return true;
                }
            }
            spot = default;
            return false;
        }

        static bool GroundAt(Vector3 probe, Transform ignore, out Vector3 ground)
        {
            var origin = new Vector3(probe.x, probe.y + 200f, probe.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance)); // highest surface first
            foreach (RaycastHit hit in hits)
            {
                if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
                if (hit.point.y > probe.y + 3f) continue; // roofs/bridges above the target: look underneath
                if (hit.normal.y < 0.7f) continue;       // too steep to stand on
                ground = hit.point;
                return true;
            }
            ground = default;
            return false;
        }

        static bool IsClear(Vector3 feet, float radius, float height, Transform ignore)
        {
            Vector3 bottom = feet + Vector3.up * (radius + 0.1f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + 0.1f, height - radius); // wide checks become a sphere
            foreach (Collider c in Physics.OverlapCapsule(bottom, top, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                if (ignore == null || !c.transform.IsChildOf(ignore)) return false;
            return true;
        }
    }
}
