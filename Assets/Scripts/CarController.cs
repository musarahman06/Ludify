using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WheelCollider-based race car: rear-wheel drive, power-limited engine, aero drag and downforce,
/// anti-roll bars and speed-sensitive steering. Create with <see cref="MakeDriveable"/>.
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("Body")]
    public float mass = 750f;

    [Header("Engine")]
    public float enginePower = 420000f;     // watts at the rear wheels
    public float maxDriveForce = 7000f;     // traction-limited launch force (N)
    public float reverseMaxSpeed = 10f;     // m/s
    public float topSpeed = 75f;            // m/s, used for steering

    [Header("Gearbox (automatic)")]
    public float[] gearTopSpeeds = { 16f, 27f, 38f, 50f, 63f, 77f };   // m/s at redline in each gear
    public float idleRpm = 950f;
    public float redlineRpm = 7400f;
    public float upshiftRpm = 7100f;
    public float downshiftRpm = 3600f;

    [Header("Brakes")]
    public float maxBrakeForce = 22000f;    // N, whole car
    [Range(0f, 1f)] public float frontBrakeBias = 0.6f;
    public float handbrakeTorque = 6000f;

    [Header("Aero")]
    public float dragCoefficient = 0.9f;    // F = c * v^2
    public float downforceCoefficient = 1.4f;

    [Header("Steering")]
    public float lowSpeedSteerAngle = 40f;
    public float highSpeedSteerAngle = 20f;
    public float steerSpeed = 7f;

    [Header("Arcade turning")]
    [Tooltip("Turn rate at full lock (degrees/second). Stays high at speed, unlike real tyres.")]
    public float maxYawRate = 130f;
    [Tooltip("Tightest turning circle radius at low speed (m).")]
    public float minTurnRadius = 5f;
    public float yawResponse = 8f;
    [Tooltip("How strongly the car's motion is pulled toward where it points (higher = less sliding).")]
    public float gripAssist = 6f;
    public float handbrakeGripAssist = 0.8f;

    [Header("Suspension")]
    public float suspensionDistance = 0.15f;
    public float spring = 35000f;
    public float damper = 4500f;
    public float antiRoll = 11000f;

    [Header("Grip")]
    public float frontSideStiffness = 2.2f;
    public float rearSideStiffness = 2.2f;
    public float handbrakeRearSideStiffness = 0.6f;

    /// <summary>True while the player is driving this car.</summary>
    public bool IsDriven { get; set; }
    public float SpeedKmh => ForwardSpeed * 3.6f;
    public float SpeedMph => ForwardSpeed * 2.23694f;
    /// <summary>Current gear: -1 reverse, 1..n forward.</summary>
    public int Gear { get; private set; } = 1;
    /// <summary>Simulated engine speed, for the rev counter and engine sound.</summary>
    public float EngineRpm { get; private set; }
    /// <summary>Throttle pedal 0..1 (reversing counts as throttle).</summary>
    public float Throttle { get; private set; }
    public Transform Root => transform;
    public Rigidbody Body => body;
    public Collider BodyCollider => bodyCollider;
    public float ForwardSpeed => body ? Vector3.Dot(body.linearVelocity, transform.forward) : 0f;

    Rigidbody body;
    BoxCollider bodyCollider;
    WheelCollider frontLeft, frontRight, rearLeft, rearRight;
    Transform visFrontLeft, visFrontRight, visRearLeft, visRearRight;
    Quaternion[] wheelVisualOffset = new Quaternion[4];
    float steer;
    bool initialized;


    /// <summary>
    /// Wraps a Kenney race car model (children wheelFrontLeft / wheelFrontRight / wheelBackLeft / wheelBackRight)
    /// in an unscaled physics root, so collider sizes and wheel radii are true to life, and returns its controller.
    /// </summary>
    public static CarController MakeDriveable(Transform model)
    {
        var fl = FindDeep(model, "wheelFrontLeft");
        var fr = FindDeep(model, "wheelFrontRight");
        var rl = FindDeep(model, "wheelBackLeft");
        var rr = FindDeep(model, "wheelBackRight");
        if (!fl || !fr || !rl || !rr)
        {
            Debug.LogWarning($"[CarController] '{model.name}' has no wheelFrontLeft/Right + wheelBackLeft/Right children; not driveable.");
            return null;
        }

        // Forward = rear axle -> front axle, whatever way the model was authored.
        Vector3 frontAxle = (fl.position + fr.position) * 0.5f;
        Vector3 rearAxle = (rl.position + rr.position) * 0.5f;
        Vector3 forward = Vector3.ProjectOnPlane(frontAxle - rearAxle, Vector3.up).normalized;

        var root = new GameObject(model.name + "_Driveable");
        root.transform.SetPositionAndRotation(model.position, Quaternion.LookRotation(forward, Vector3.up));
        root.transform.SetParent(model.parent, true);
        model.SetParent(root.transform, true);

        var car = root.AddComponent<CarController>();
        car.BuildPhysics(fl, fr, rl, rr);
        root.AddComponent<CarEngineAudio>();
        return car;
    }

    void BuildPhysics(Transform fl, Transform fr, Transform rl, Transform rr)
    {
        initialized = true;
        visFrontLeft = fl; visFrontRight = fr; visRearLeft = rl; visRearRight = rr;

        // Lift slightly so the wheels settle onto the ground instead of starting inside it.
        transform.position += Vector3.up * 0.3f;

        body = gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = 0f;
        body.angularDamping = 0.5f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        float wheelRadius = WheelRadius(fl);
        float wheelY = transform.InverseTransformPoint(fl.position).y;

        // Body box: the model's renderers, with its floor raised to the wheel centres for ground clearance.
        var bounds = LocalBounds(transform);
        float bottom = Mathf.Max(bounds.min.y, wheelY);
        var min = new Vector3(bounds.min.x, bottom, bounds.min.z);
        var size = bounds.max - min;
        bodyCollider = gameObject.AddComponent<BoxCollider>();
        bodyCollider.center = min + size * 0.5f;
        bodyCollider.size = size;

        Vector3 frontLocal = transform.InverseTransformPoint((fl.position + fr.position) * 0.5f);
        Vector3 rearLocal = transform.InverseTransformPoint((rl.position + rr.position) * 0.5f);
        // Low centre of mass, slightly rear-biased like a mid-engined race car.
        body.centerOfMass = new Vector3(0f, wheelY - wheelRadius * 0.3f, Mathf.Lerp(rearLocal.z, frontLocal.z, 0.45f));

        frontLeft = CreateWheel("WC_FrontLeft", fl, wheelRadius, frontSideStiffness);
        frontRight = CreateWheel("WC_FrontRight", fr, wheelRadius, frontSideStiffness);
        rearLeft = CreateWheel("WC_RearLeft", rl, wheelRadius, rearSideStiffness);
        rearRight = CreateWheel("WC_RearRight", rr, wheelRadius, rearSideStiffness);
        frontLeft.ConfigureVehicleSubsteps(5f, 12, 15);

        // Remember how each visual wheel is rotated relative to the collider's pose.
        var wheels = new[] { frontLeft, frontRight, rearLeft, rearRight };
        var vis = new[] { fl, fr, rl, rr };
        for (int i = 0; i < 4; i++)
        {
            wheels[i].GetWorldPose(out _, out var q);
            wheelVisualOffset[i] = Quaternion.Inverse(q) * vis[i].rotation;
        }
    }

    WheelCollider CreateWheel(string wheelName, Transform visual, float radius, float sideStiffness)
    {
        var go = new GameObject(wheelName);
        go.transform.SetParent(transform, false);
        // Mount point sits above the wheel centre so the wheel rests at its modelled height.
        Vector3 local = transform.InverseTransformPoint(visual.position);
        go.transform.localPosition = local + Vector3.up * suspensionDistance * 0.5f;
        go.transform.localRotation = Quaternion.identity;

        var wc = go.AddComponent<WheelCollider>();
        wc.mass = 20f;
        wc.radius = radius;
        wc.wheelDampingRate = 0.5f;
        wc.suspensionDistance = suspensionDistance;
        wc.forceAppPointDistance = 0.1f;
        wc.suspensionSpring = new JointSpring { spring = spring, damper = damper, targetPosition = 0.5f };

        var fwd = wc.forwardFriction;
        fwd.extremumSlip = 0.4f; fwd.extremumValue = 1.2f;
        fwd.asymptoteSlip = 0.8f; fwd.asymptoteValue = 0.8f;
        fwd.stiffness = 1.5f;
        wc.forwardFriction = fwd;

        var side = wc.sidewaysFriction;
        side.extremumSlip = 0.25f; side.extremumValue = 1.1f;
        side.asymptoteSlip = 0.6f; side.asymptoteValue = 0.8f;
        side.stiffness = sideStiffness;
        wc.sidewaysFriction = side;
        return wc;
    }

    void Update()
    {
        if (!initialized || body == null) return;
        SyncVisual(frontLeft, visFrontLeft, 0);
        SyncVisual(frontRight, visFrontRight, 1);
        SyncVisual(rearLeft, visRearLeft, 2);
        SyncVisual(rearRight, visRearRight, 3);
    }

    void FixedUpdate()
    {
        if (!initialized || body == null) return;

        float throttle = 0f, brake = 0f, steerInput = 0f;
        bool handbrake = !IsDriven;
        var kb = Keyboard.current;
        if (IsDriven && kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerInput += 1f;
            handbrake = kb.spaceKey.isPressed;
        }

        float speed = ForwardSpeed;
        float absSpeed = Mathf.Abs(speed);

        // Speed-sensitive steering, eased so the wheels don't snap.
        float maxSteer = Mathf.Lerp(lowSpeedSteerAngle, highSpeedSteerAngle, Mathf.Clamp01(absSpeed / topSpeed));
        steer = Mathf.MoveTowards(steer, steerInput, steerSpeed * Time.fixedDeltaTime);
        frontLeft.steerAngle = frontRight.steerAngle = steer * maxSteer;

        // S brakes while rolling forward, and reverses once (almost) stopped.
        float driveForce = 0f;
        float brakeForce = 0f;
        if (throttle > 0f)
        {
            if (speed < -0.5f) brakeForce = maxBrakeForce * throttle;
            else driveForce = Mathf.Min(maxDriveForce, enginePower / Mathf.Max(absSpeed, 1f)) * throttle;
        }
        if (brake > 0f)
        {
            if (speed > 1f) brakeForce = maxBrakeForce * brake;
            else if (-speed < reverseMaxSpeed) driveForce = -maxDriveForce * 0.4f * brake;
        }

        float rearDriveTorque = driveForce * rearLeft.radius * 0.5f;
        rearLeft.motorTorque = rearRight.motorTorque = rearDriveTorque;
        frontLeft.motorTorque = frontRight.motorTorque = 0f;

        float frontBrake = brakeForce * frontBrakeBias * 0.5f * frontLeft.radius;
        float rearBrake = brakeForce * (1f - frontBrakeBias) * 0.5f * rearLeft.radius;
        if (!IsDriven && throttle == 0f && brake == 0f) frontBrake = Mathf.Max(frontBrake, handbrakeTorque);
        frontLeft.brakeTorque = frontRight.brakeTorque = frontBrake;
        rearLeft.brakeTorque = rearRight.brakeTorque = handbrake ? Mathf.Max(rearBrake, handbrakeTorque) : rearBrake;

        SetRearSideStiffness(handbrake && IsDriven ? handbrakeRearSideStiffness : rearSideStiffness);

        // Aerodynamics: drag against motion and downforce for high-speed grip, both growing with speed squared.
        Vector3 v = body.linearVelocity;
        body.AddForce(-v * v.magnitude * dragCoefficient);
        if (frontLeft.isGrounded || rearLeft.isGrounded)
            body.AddForce(-transform.up * downforceCoefficient * speed * speed);

        ApplyAntiRoll(frontLeft, frontRight);
        ApplyAntiRoll(rearLeft, rearRight);

        if (IsDriven) ApplyArcadeTurning(speed, handbrake);

        Throttle = IsDriven ? Mathf.Max(throttle, speed <= 1f ? brake : 0f) : 0f;
        UpdateGearbox(speed);
    }

    /// <summary>Automatic gearbox: engine rpm follows the driven wheels, with a slipping clutch near standstill.</summary>
    void UpdateGearbox(float speed)
    {
        // Driven-wheel surface speed (includes wheelspin, so a burnout revs the engine).
        float wheelSpeed = Mathf.Abs((rearLeft.rpm + rearRight.rpm) * 0.5f * 2f * Mathf.PI * rearLeft.radius / 60f);

        if (speed < -0.5f) Gear = -1;
        else if (Gear < 1) Gear = 1;

        int g = Mathf.Clamp(Gear, 1, gearTopSpeeds.Length);
        float rpmInGear = redlineRpm * wheelSpeed / (Gear < 0 ? gearTopSpeeds[0] : gearTopSpeeds[g - 1]);
        if (Gear > 0)
        {
            if (rpmInGear > upshiftRpm && g < gearTopSpeeds.Length) Gear = g + 1;
            else if (rpmInGear < downshiftRpm && g > 1) Gear = g - 1;
        }

        // Clutch: pulling away, the engine sits at launch revs instead of stalling at wheel speed.
        float clutchRpm = idleRpm + Throttle * 2800f;
        float target = Mathf.Max(rpmInGear, Gear == 1 || Gear < 0 ? Mathf.Lerp(idleRpm, clutchRpm, Throttle) : idleRpm);
        if (!IsDriven) target = 0f;
        target = Mathf.Min(target, redlineRpm);

        // Flywheel inertia: revs rise quickly and fall a bit slower.
        float rate = target > EngineRpm ? 16000f : 9000f;
        EngineRpm = Mathf.MoveTowards(EngineRpm, target, rate * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Arcade assist so the car can corner hard even at 150+ mph: steer drives a target yaw rate directly,
    /// and the velocity is pulled toward the car's heading. Applied to the body's velocity (not at the tyres),
    /// so it adds no rolling moment and can't flip the car. The handbrake loosens the pull, allowing drifts.
    /// </summary>
    void ApplyArcadeTurning(float speed, bool handbrake)
    {
        if (!(frontLeft.isGrounded || frontRight.isGrounded || rearLeft.isGrounded || rearRight.isGrounded)) return;
        float dt = Time.fixedDeltaTime;
        float absSpeed = Mathf.Abs(speed);

        // Yaw: limited by a minimum turning circle at low speed, a fixed max rate at high speed.
        float yawLimit = Mathf.Min(maxYawRate * Mathf.Deg2Rad, absSpeed / minTurnRadius);
        float targetYaw = steer * yawLimit * Mathf.Sign(speed);
        Vector3 up = transform.up;
        Vector3 av = body.angularVelocity;
        float yawNow = Vector3.Dot(av, up);
        float yawNew = Mathf.Lerp(yawNow, targetYaw, 1f - Mathf.Exp(-yawResponse * dt));
        body.angularVelocity = av + up * (yawNew - yawNow);

        // Grip: rotate the horizontal velocity toward the heading, keeping its speed.
        Vector3 v = body.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(v, up);
        if (planar.sqrMagnitude < 0.25f) return;
        Vector3 heading = transform.forward * (Vector3.Dot(planar, transform.forward) >= 0f ? 1f : -1f);
        Vector3 desired = Vector3.ProjectOnPlane(heading, up).normalized * planar.magnitude;
        float assist = handbrake ? handbrakeGripAssist : gripAssist;
        Vector3 newPlanar = Vector3.Lerp(planar, desired, 1f - Mathf.Exp(-assist * dt));
        body.linearVelocity = v - planar + newPlanar;
    }

    void SetRearSideStiffness(float stiffness)
    {
        foreach (var wc in new[] { rearLeft, rearRight })
        {
            var f = wc.sidewaysFriction;
            if (Mathf.Approximately(f.stiffness, stiffness)) continue;
            f.stiffness = stiffness;
            wc.sidewaysFriction = f;
        }
    }

    void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        float travelL = 1f, travelR = 1f;
        bool groundedL = left.GetGroundHit(out var hitL);
        bool groundedR = right.GetGroundHit(out var hitR);
        if (groundedL) travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance;
        if (groundedR) travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance;

        float force = (travelL - travelR) * antiRoll;
        if (groundedL) body.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        if (groundedR) body.AddForceAtPosition(right.transform.up * force, right.transform.position);
    }

    void SyncVisual(WheelCollider wc, Transform visual, int index)
    {
        // GetWorldPose is in un-interpolated physics space; re-express it relative to the interpolated body so wheels don't jitter.
        wc.GetWorldPose(out var pos, out var rot);
        Quaternion invBody = Quaternion.Inverse(body.rotation);
        Vector3 localPos = invBody * (pos - body.position);
        Quaternion localRot = invBody * rot;
        visual.SetPositionAndRotation(transform.TransformPoint(localPos), transform.rotation * localRot * wheelVisualOffset[index]);
    }

    static float WheelRadius(Transform wheel)
    {
        var r = wheel.GetComponentInChildren<Renderer>();
        return r ? r.bounds.extents.y : 0.5f;
    }

    static Bounds LocalBounds(Transform root)
    {
        bool first = true;
        var b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mb.min.x : mb.max.x,
                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    static Transform FindDeep(Transform t, string childName)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == childName) return c;
        return null;
    }
}
