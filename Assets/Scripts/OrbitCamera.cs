using UnityEngine;
using UnityEngine.InputSystem;

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

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch -= delta.y * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
            float scroll = mouse.scroll.ReadValue().y;
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
