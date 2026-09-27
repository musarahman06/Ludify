using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rideable bicycle for the newspaper route. Arcade physics on a CharacterController (collides with buildings,
/// walls and kerbs; gravity and slopes): W pedals, S brakes / rolls back, A/D steer with a speed-dependent turn
/// rate. Leans into turns, wheels and cranks turn. While riding, the player object is hidden (like in a car) and
/// a copy of the player's look sits on the saddle, pedalling.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BikeController : MonoBehaviour
{
    public float maxSpeed = 13f, accel = 5f, brake = 9f, coast = 1.2f, reverseSpeed = 2f;
    public float slowTurnRate = 130f, fastTurnRate = 55f, gravity = -20f;

    public bool IsRidden { get; private set; }
    public float Speed => speed;
    public Transform Basket { get; private set; }

    CharacterController controller;
    Transform lean, frontWheel, rearWheel, fork, cranks, rider, leftLeg, rightLeg, leftArm, rightArm;
    float speed, heading, verticalVelocity, steer, wheelAngle, crankAngle;
    GameObject player;
    OrbitCamera cam;
    float playerCamDistance;

    public static BikeController Create(Transform parent, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject("Bike");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.6f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.85f, 0f);
        cc.stepOffset = 0.35f; cc.slopeLimit = 45f;
        var bike = go.AddComponent<BikeController>();
        bike.BuildVisual();
        go.AddComponent<FallGuard>();
        return bike;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        heading = transform.eulerAngles.y;
    }

    void BuildVisual()
    {
        Color frame = new Color(0.1f, 0.55f, 0.85f), tire = new Color(0.08f, 0.08f, 0.08f);
        lean = new GameObject("Lean").transform;
        lean.SetParent(transform, false);

        rearWheel = Wheel("RearWheel", new Vector3(0f, 0.36f, -0.55f), tire);
        fork = new GameObject("Fork").transform;
        fork.SetParent(lean, false);
        fork.localPosition = new Vector3(0f, 0f, 0.55f);
        frontWheel = Wheel("FrontWheel", new Vector3(0f, 0.36f, 0f), tire, fork);

        JobProps.Part(lean, "TopTube", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.05f), new Vector3(0.06f, 0.06f, 0.85f), frame, new Vector3(-8f, 0f, 0f));
        JobProps.Part(lean, "DownTube", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.15f), new Vector3(0.06f, 0.06f, 0.8f), frame, new Vector3(-38f, 0f, 0f));
        JobProps.Part(lean, "SeatTube", PrimitiveType.Cube, new Vector3(0f, 0.65f, -0.22f), new Vector3(0.06f, 0.7f, 0.06f), frame, new Vector3(-15f, 0f, 0f));
        JobProps.Part(lean, "ChainStay", PrimitiveType.Cube, new Vector3(0f, 0.37f, -0.35f), new Vector3(0.05f, 0.05f, 0.45f), frame);
        JobProps.Part(lean, "Seat", PrimitiveType.Cube, new Vector3(0f, 1.02f, -0.3f), new Vector3(0.16f, 0.06f, 0.3f), tire);
        JobProps.Part(fork, "ForkTube", PrimitiveType.Cube, new Vector3(0f, 0.65f, -0.05f), new Vector3(0.05f, 0.65f, 0.05f), frame, new Vector3(-12f, 0f, 0f));
        JobProps.Part(fork, "Bars", PrimitiveType.Cube, new Vector3(0f, 1.0f, -0.12f), new Vector3(0.6f, 0.05f, 0.05f), tire);

        cranks = new GameObject("Cranks").transform;
        cranks.SetParent(lean, false);
        cranks.localPosition = new Vector3(0f, 0.36f, -0.05f);
        JobProps.Part(cranks, "Arm", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.04f, 0.04f), JobProps.Metal, new Vector3(0f, 90f, 0f));

        Basket = new GameObject("Basket").transform;
        Basket.SetParent(fork, false);
        Basket.localPosition = new Vector3(0f, 0.85f, 0.2f);
        JobProps.Part(Basket, "Basket", PrimitiveType.Cube, Vector3.zero, new Vector3(0.4f, 0.25f, 0.3f), JobProps.Wood);
    }

    Transform Wheel(string name, Vector3 pos, Color color, Transform parent = null)
    {
        var w = new GameObject(name).transform;
        w.SetParent(parent != null ? parent : lean, false);
        w.localPosition = pos;
        JobProps.Part(w, "Tire", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.72f, 0.03f, 0.72f), color, new Vector3(0f, 0f, 90f));
        JobProps.Part(w, "Spoke", PrimitiveType.Cube, Vector3.zero, new Vector3(0.02f, 0.66f, 0.03f), JobProps.Metal);
        JobProps.Part(w, "Spoke2", PrimitiveType.Cube, Vector3.zero, new Vector3(0.02f, 0.03f, 0.66f), JobProps.Metal);
        return w;
    }

    // ------------------------------------------------------------------ mounting

    public void Mount(PlayerController p, OrbitCamera camera)
    {
        player = p.gameObject;
        cam = camera;
        IsRidden = true;
        speed = 0f;
        heading = transform.eulerAngles.y;

        // A copy of the player's look (outfit included) sits on the saddle.
        rider = Instantiate(p.visualRoot.gameObject, lean, false).transform;
        rider.name = "Rider";
        foreach (var c in rider.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in rider.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;
        rider.localPosition = new Vector3(0f, 0.12f, -0.28f);
        rider.localRotation = Quaternion.identity;
        leftLeg = Find(rider, "LeftLegPivot"); rightLeg = Find(rider, "RightLegPivot");
        leftArm = Find(rider, "LeftArmPivot"); rightArm = Find(rider, "RightArmPivot");

        player.SetActive(false);
        if (cam != null)
        {
            playerCamDistance = cam.distance;
            cam.SetTarget(transform, 7f);
        }
    }

    /// <summary>Brake to a standstill instantly (e.g. while a question is open).</summary>
    public void Stop() => speed = 0f;

    public void Dismount()
    {
        if (!IsRidden) return;
        IsRidden = false;
        speed = 0f;
        if (rider != null) Destroy(rider.gameObject);

        // Step off on the left, onto whatever ground is there.
        Vector3 spot = JobProps.Ground(transform.position - transform.right * 1.3f);
        player.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, heading, 0f));
        player.SetActive(true);
        if (cam != null) cam.SetTarget(player.transform, playerCamDistance);
    }

    static Transform Find(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    // ------------------------------------------------------------------ riding

    void Update()
    {
        float dt = Time.deltaTime;
        float throttle = 0f, steerInput = 0f;
        var kb = Keyboard.current;
        bool controls = IsRidden && kb != null && !Ludify.Import.QuestionPrompt.IsOpen && Time.timeScale > 0f;
        if (controls)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerInput += 1f;
        }

        if (throttle > 0f) speed = Mathf.MoveTowards(speed, maxSpeed, accel * dt);
        else if (throttle < 0f) speed = speed > 0.1f ? Mathf.MoveTowards(speed, 0f, brake * dt) : Mathf.MoveTowards(speed, -reverseSpeed, accel * dt);
        else speed = Mathf.MoveTowards(speed, 0f, coast * dt);

        steer = Mathf.MoveTowards(steer, steerInput, 5f * dt);
        float speed01 = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
        float turnRate = Mathf.Lerp(slowTurnRate, fastTurnRate, speed01) * Mathf.Clamp01(Mathf.Abs(speed) / 1.5f + 0.15f);
        heading += steer * turnRate * Mathf.Sign(speed == 0f ? 1f : speed) * dt;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;
        Vector3 forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
        var flags = controller.Move((forward * speed + Vector3.up * verticalVelocity) * dt);
        if ((flags & CollisionFlags.Sides) != 0) speed *= 0.5f;   // bumping into something scrubs speed
        transform.rotation = Quaternion.Euler(0f, heading, 0f);

        // Visuals: lean into the turn, turn the front wheel, roll the wheels, pedal.
        lean.localRotation = Quaternion.Slerp(lean.localRotation, Quaternion.Euler(0f, 0f, -steer * speed01 * 22f), 8f * dt);
        fork.localRotation = Quaternion.Euler(0f, steer * Mathf.Lerp(25f, 6f, speed01), 0f);
        wheelAngle += speed / 0.36f * Mathf.Rad2Deg * dt;
        frontWheel.localRotation = rearWheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f);
        if (throttle > 0f || Mathf.Abs(speed) > 0.5f) crankAngle += Mathf.Abs(speed) * 60f * dt + (throttle > 0f ? 120f * dt : 0f);
        cranks.localRotation = Quaternion.Euler(crankAngle, 0f, 0f);

        if (rider != null)
        {
            float s = Mathf.Sin(crankAngle * Mathf.Deg2Rad);
            if (leftLeg) leftLeg.localRotation = Quaternion.Euler(-55f + s * 30f, 0f, 0f);
            if (rightLeg) rightLeg.localRotation = Quaternion.Euler(-55f - s * 30f, 0f, 0f);
            if (leftArm) leftArm.localRotation = Quaternion.Euler(-65f, 0f, 0f);
            if (rightArm) rightArm.localRotation = Quaternion.Euler(-65f, 0f, 0f);
        }
    }
}
