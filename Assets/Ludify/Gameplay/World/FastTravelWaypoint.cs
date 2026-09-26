using Ludify.Gameplay.Core;
using UnityEngine;

namespace Ludify.Gameplay.World
{
    /// <summary>Where the player lands when fast-travelling to a zone. Forward (+Z) is the facing direction on arrival.</summary>
    public sealed class FastTravelWaypoint : MonoBehaviour
    {
        [SerializeField] ZoneId zone;

        public ZoneId Zone => zone;

        public void Configure(ZoneId id) => zone = id;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward * 2.5f);
        }
    }
}
