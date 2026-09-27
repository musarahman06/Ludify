using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the on-foot player get in and out of <see cref="CarController"/> cars with X.
/// Lives on its own GameObject because the player object is deactivated while driving.
/// </summary>
public class VehicleInteraction : MonoBehaviour
{
    public GameObject player;
    public OrbitCamera orbitCamera;
    public float enterRange = 4f;
    public float maxExitSpeed = 3f;       // m/s
    public float carCameraDistance = 11f;

    /// <summary>Raised when the player gets into / out of a car (the time trial listens).</summary>
    public static event Action<CarController> CarEntered, CarExited;

    /// <summary>While true, X can't take the player out of the car (e.g. mid-question).</summary>
    public static bool BlockExit;

    CarController[] cars;
    CarController nearestCar;
    CarController drivenCar;
    float playerCameraDistance;
    GUIStyle hintStyle;

    void Start()
    {
        cars = FindObjectsByType<CarController>();
    }

    void Update()
    {
        if (player == null || orbitCamera == null || LoadingScreen.IsLoading) return;
        var kb = Keyboard.current;
        bool interact = kb != null && kb.xKey.wasPressedThisFrame;

        if (drivenCar != null)
        {
            if (interact && !BlockExit && Mathf.Abs(drivenCar.ForwardSpeed) <= maxExitSpeed) Exit();
            return;
        }

        // Player hidden but not in a car = riding the paper-route bike (or another mode): no car switching.
        if (!player.activeInHierarchy) { nearestCar = null; return; }

        nearestCar = FindNearestCar();
        if (interact && nearestCar != null) Enter(nearestCar);
    }

    CarController FindNearestCar()
    {
        CarController best = null;
        float bestDist = enterRange;
        Vector3 p = player.transform.position + Vector3.up;
        foreach (var car in cars)
        {
            if (car == null || car.BodyCollider == null) continue;
            float d = Vector3.Distance(p, car.BodyCollider.ClosestPoint(p));
            if (d < bestDist) { bestDist = d; best = car; }
        }
        return best;
    }

    void Enter(CarController car)
    {
        drivenCar = car;
        car.IsDriven = true;
        player.SetActive(false);
        playerCameraDistance = orbitCamera.distance;
        orbitCamera.SetTarget(car.transform, carCameraDistance);
        SetOtherCarsVisible(car, false);   // an empty track while you race
        CarEntered?.Invoke(car);
    }

    void Exit()
    {
        var car = drivenCar;
        car.IsDriven = false;
        drivenCar = null;

        // Step out on the driver's (left) side, onto whatever ground is there.
        Vector3 side = -car.transform.right * (car.BodyCollider.bounds.extents.magnitude * 0.6f + 1f);
        Vector3 spot = car.transform.position + side;
        if (Physics.Raycast(spot + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            spot = hit.point;
        player.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(car.transform.forward, Vector3.up));
        player.SetActive(true);
        orbitCamera.SetTarget(player.transform, playerCameraDistance);
        SetOtherCarsVisible(car, true);    // the other cars come back, parked where they were
        CarExited?.Invoke(car);
    }

    /// <summary>Hides (or restores) every car except the one being driven.</summary>
    void SetOtherCarsVisible(CarController driven, bool visible)
    {
        foreach (var c in cars)
            if (c != null && c != driven) c.gameObject.SetActive(visible);
    }

    void OnGUI()
    {
        string hint = null;
        if (drivenCar != null)
        {
            Speedometer.Draw(drivenCar);
            // Only mention getting out once the car is slow enough to do it.
            if (!BlockExit && Mathf.Abs(drivenCar.ForwardSpeed) <= maxExitSpeed) hint = "Press X to get out";
        }
        else if (nearestCar != null)
        {
            hint = "Press X to drive";
        }
        if (hint == null) return;

        hintStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(16, Screen.height / 40),
            alignment = TextAnchor.LowerCenter,
            normal = { textColor = Color.white },
        };
        var rect = new Rect(0, Screen.height - Screen.height * 0.2f, Screen.width, Screen.height * 0.15f);
        var shadow = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
        var prev = hintStyle.normal.textColor;
        hintStyle.normal.textColor = new Color(0, 0, 0, 0.7f);
        GUI.Label(shadow, hint, hintStyle);
        hintStyle.normal.textColor = prev;
        GUI.Label(rect, hint, hintStyle);
    }
}
