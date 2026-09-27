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

    [Header("Look input")]
    public float mouseSensitivity = 0.15f;      // click-drag (mouse or trackpad)
    public float scrollLookSensitivity = 10f;   // two-finger trackpad swipe (degrees per scroll step)
    public bool invertScrollLook = false;
    public float keyLookSpeed = 90f;            // Q/E turn, R/F tilt (degrees per second)

    [Header("Zoom")]
    public float scrollSensitivity = 1f;        // Ctrl/Cmd + scroll (units per scroll step)
    public float keyZoomSpeed = 10f;            // +/- keys (units per second)

    [Header("Smoothing")]
    public float followSmoothTime = 0.05f;
    public float verticalSmoothTime = 0.12f;
    public float collisionPullInSpeed = 30f;
    public float collisionEaseOutSpeed = 4f;
    public LayerMask collisionMask = ~0;

    [Header("Chase (vehicles)")]
    public float chaseDelay = 1f;           // seconds without look input before swinging behind a vehicle
    public float chaseSpeed = 3f;

    private Rigidbody targetBody;
    private bool chaseTarget;
    private Vector3 lastTargetPosition;
    private Vector3 targetVelocity;
    private float lastLookInputTime = -999f;
    private Vector3 smoothedPivot;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float currentDistance;
    private bool initialized;

    /// <summary>Follow a new target (e.g. a car) at the given distance.</summary>
    public void SetTarget(Transform newTarget, float newDistance)
    {
        target = newTarget;
        targetBody = newTarget ? newTarget.GetComponent<Rigidbody>() : null;
        // Vehicles without a Rigidbody (e.g. the bike) also get the chase camera.
        chaseTarget = targetBody != null || (newTarget != null && newTarget.GetComponent<CharacterController>() != null
                                             && newTarget.GetComponent<PlayerController>() == null);
        lastTargetPosition = newTarget ? newTarget.position : Vector3.zero;
        distance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleInput();

        // Vehicles: after a moment without look input, swing round behind the direction of travel.
        if (Time.deltaTime > 0f) targetVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
        lastTargetPosition = target.position;
        Vector3 vehicleVelocity = targetBody != null ? targetBody.linearVelocity : targetVelocity;
        if (chaseTarget && Time.time - lastLookInputTime > chaseDelay
            && Vector3.Dot(vehicleVelocity, target.forward) > 2f)
        {
            float heading = Mathf.Atan2(target.forward.x, target.forward.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, heading, 1f - Mathf.Exp(-chaseSpeed * Time.deltaTime));
        }

        Vector3 rawPivot = target.position + targetOffset;
        if (!initialized)
        {
            smoothedPivot = rawPivot;
            currentDistance = distance;
            initialized = true;
        }
        else
        {
            // Separate horizontal/vertical smoothing: follows movement tightly but ignores small vertical steps.
            Vector3 horizontal = Vector3.SmoothDamp(
                new Vector3(smoothedPivot.x, 0f, smoothedPivot.z),
                new Vector3(rawPivot.x, 0f, rawPivot.z),
                ref horizontalVelocity, followSmoothTime);
            float y = Mathf.SmoothDamp(smoothedPivot.y, rawPivot.y, ref verticalVelocity, verticalSmoothTime);
            smoothedPivot = new Vector3(horizontal.x, y, horizontal.z);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rot * Vector3.back;

        // Nearest obstacle behind the pivot, ignoring the target itself (e.g. the car body).
        float targetDistance = distance;
        foreach (var hit in Physics.RaycastAll(smoothedPivot, back, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(target)) continue;
            targetDistance = Mathf.Min(targetDistance, Mathf.Max(hit.distance - 0.3f, 0.3f));
        }

        // Pull in quickly to avoid clipping, ease back out slowly so bumps don't make the camera pump.
        float speed = targetDistance < currentDistance ? collisionPullInSpeed : collisionEaseOutSpeed;
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-speed * Time.deltaTime));

        transform.position = smoothedPivot + back * currentDistance;
        transform.rotation = rot;
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        bool zoomModifier = keyboard != null &&
            (keyboard.ctrlKey.isPressed || keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed);

        if (mouse != null)
        {
            // Click-drag with either button (trackpad click-drag works too).
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity;
                pitch -= delta.y * mouseSensitivity;
                if (delta.sqrMagnitude > 0.01f) lastLookInputTime = Time.time;
            }

            // Normalise to "scroll steps" regardless of the Input System's scroll delta setting.
            Vector2 scroll = mouse.scroll.ReadValue();
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)
                scroll /= 120f;
            if (zoomModifier)
            {
                if (Mathf.Abs(scroll.y) > 0.01f)
                    distance -= scroll.y * scrollSensitivity;
            }
            else if (scroll.sqrMagnitude > 0.0001f)
            {
                // Two-finger swipe: sideways turns, up/down tilts.
                float sign = invertScrollLook ? -1f : 1f;
                yaw += scroll.x * scrollLookSensitivity * sign;
                pitch += scroll.y * scrollLookSensitivity * sign;
                lastLookInputTime = Time.time;
            }
        }

        if (keyboard != null)
        {
            float dt = Time.deltaTime;
            if (keyboard.qKey.isPressed) yaw -= keyLookSpeed * dt;
            if (keyboard.eKey.isPressed) yaw += keyLookSpeed * dt;
            if (keyboard.rKey.isPressed) pitch -= keyLookSpeed * dt;
            if (keyboard.fKey.isPressed) pitch += keyLookSpeed * dt;
            if (keyboard.qKey.isPressed || keyboard.eKey.isPressed || keyboard.rKey.isPressed || keyboard.fKey.isPressed)
                lastLookInputTime = Time.time;

            if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) distance -= keyZoomSpeed * dt;
            if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) distance += keyZoomSpeed * dt;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
}
