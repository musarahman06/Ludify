using UnityEngine;

/// <summary>
/// Sends the player (or a car) back to where it started if it ends up below the terrain, e.g. after clipping
/// through the ground or falling out of the world. Added automatically by RuntimeWorldColliders.
/// </summary>
public class FallGuard : MonoBehaviour
{
    [Tooltip("How far below the terrain surface counts as fallen through.")]
    public float marginBelowTerrain = 3f;
    [Tooltip("Absolute safety floor, for anywhere the terrain can't be sampled.")]
    public float absoluteMinY = -40f;

    Vector3 spawnPosition;
    Quaternion spawnRotation;
    Terrain terrain;
    CharacterController controller;
    CarController car;

    void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        terrain = Terrain.activeTerrain;
        controller = GetComponent<CharacterController>();
        car = GetComponent<CarController>();
    }

    void LateUpdate()
    {
        Vector3 p = transform.position;
        bool fell = p.y < absoluteMinY;
        if (!fell && terrain != null)
        {
            float ground = terrain.SampleHeight(p) + terrain.GetPosition().y;
            fell = p.y < ground - marginBelowTerrain;
        }
        if (fell) ResetToSpawn();
    }

    public void ResetToSpawn()
    {
        if (car != null)
        {
            car.PlaceAt(spawnPosition, spawnRotation);
            return;
        }
        // A CharacterController overrides direct position changes unless it's briefly disabled.
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (wasEnabled) controller.enabled = true;
        Debug.Log($"[FallGuard] '{name}' fell below the terrain; reset to spawn.");
    }
}
