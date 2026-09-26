using Ludify.Gameplay.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ludify.Gameplay.Player
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
        public float distance = 8f;
        public float minDistance = 2f;
        public float maxDistance = 20f;
        public float yaw = 0f;
        public float pitch = 20f;
        public float minPitch = -10f;
        public float maxPitch = 70f;
        public float mouseSensitivity = 0.15f;
        public float scrollSensitivity = 2f;

        void LateUpdate()
        {
            if (target == null) return;

            var input = GameModeManager.Instance != null ? GameModeManager.Instance.Input : null;
            if (input != null && input.OnFoot.enabled)
            {
                Vector2 delta = input.Look.ReadValue<Vector2>();
                // Mouse orbits only while the orbit button is held; a gamepad stick always orbits.
                bool fromGamepad = input.Look.activeControl != null && input.Look.activeControl.device is Gamepad;
                if (fromGamepad || input.OrbitHold.IsPressed())
                {
                    yaw += delta.x * mouseSensitivity;
                    pitch -= delta.y * mouseSensitivity;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }

                float scroll = input.Zoom.ReadValue<float>();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    distance -= scroll * scrollSensitivity * 0.01f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 desiredPos = pivot - rot * Vector3.forward * distance;

            if (Physics.Linecast(pivot, desiredPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                desiredPos = hit.point + (pivot - desiredPos).normalized * 0.3f;
            }

            transform.position = desiredPos;
            transform.rotation = rot;
        }
    }
}
