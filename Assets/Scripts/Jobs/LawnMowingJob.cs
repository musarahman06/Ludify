using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Suburb job: push a mower over Mrs. Green's overgrown front lawn until 95% of the tall grass is cut.
/// The old mower stalls at 25%, 50% and 75%; answer a practice question to restart it (skipped if no lecture).
/// Help: a turbo blade that doubles the cutting width for 20 s.
/// </summary>
public class LawnMowingJob : Job
{
    const float CutRadius = 0.9f, TurboSeconds = 20f, DoneAt = 0.95f;
    static readonly float[] StallAt = { 0.25f, 0.5f, 0.75f };

    readonly JobPlaces.House house;
    readonly List<Transform> tufts = new List<Transform>();
    readonly List<bool> cut = new List<bool>();
    int cutCount;
    Transform mower;
    Material cutMaterial;
    float turboUntil;
    int stallsDone;
    bool stalled, restarting;
    Vector3 lawnCenter;

    public LawnMowingJob(JobPlaces.House lawnHouse) { house = lawnHouse; }

    public override string Title => "Mow Mrs. Green's lawn";
    public override string Intro =>
        "Oh dear, look at my lawn, it's a jungle! Could you mow it for me? Just push the mower over the tall grass. " +
        "It's a grumpy old thing though. When it stalls, answer a question and it'll start right back up.";
    public override int Coins => 30;
    public override string HelpOffer => "Answer a question and I'll switch on the turbo blade. It cuts twice as wide for 20 seconds.";
    public override string ThanksLine => "It looks wonderful! Thank you, dear. Here's a little something.";

    public override float Progress01 => tufts.Count == 0 ? 0f : cutCount / (float)tufts.Count;
    public override string Objective =>
        IsComplete ? "Lawn mowed! Tell Mrs. Green (green !)."
        : stalled ? $"Lawn mowed: {Mathf.FloorToInt(Progress01 * 100f)}%\n<color=#FF8C66><b>The mower stalled!</b></color> Press E to restart it (answer a question)."
        : $"Lawn mowed: {Mathf.FloorToInt(Progress01 * 100f)}% (need {Mathf.RoundToInt(DoneAt * 100f)}%)\nWalk the mower over the tall grass." +
          (Time.time < turboUntil ? $"\n<color=#FFD24D><b>Turbo blade: {Mathf.CeilToInt(turboUntil - Time.time)} s</b></color>" : "");

    protected override void Begin()
    {
        cutMaterial = JobProps.Mat(JobProps.CutGrass);

        // Lawn: a strip in front of the house, between its road-facing wall and the street.
        Vector3 dir = house.FacingDir, across = Vector3.Cross(Vector3.up, dir);
        float width = Mathf.Clamp(house.WidthAcross, 8f, 14f);
        float depth = Mathf.Clamp(house.RoadDistance - 2f, 4f, 8f);
        Vector3 start = house.Edge + dir * 0.6f;
        lawnCenter = start + dir * (depth * 0.5f);
        int nx = Mathf.RoundToInt(width), nz = Mathf.RoundToInt(depth);
        var rng = new System.Random();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                Vector3 p = start + across * (i - (nx - 1) * 0.5f) + dir * (j + 0.5f);
                p = JobProps.Ground(p);
                if (Physics.CheckBox(p + Vector3.up * 0.6f, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    continue;   // a wall or prop is here
                tufts.Add(JobProps.GrassTuft(Root, p, 0.35f + (float)rng.NextDouble() * 0.25f));
                cut.Add(false);
            }

        mower = JobProps.Mower(Manager.Player.transform);
        AddMarker("Lawn", "L", SearchColor, lawnCenter, Mathf.Max(width, depth) * 0.75f);
    }

    public override Vector3? NextTarget(Vector3 from)
    {
        if (IsComplete) return Giver.transform.position;
        return FlatDistance(from, lawnCenter) > 10f ? lawnCenter : (Vector3?)null;
    }

    public override void Tick(Vector3 actor, float dt)
    {
        if (IsComplete || stalled || mower == null || !mower.gameObject.activeInHierarchy) return;
        float r = Time.time < turboUntil ? CutRadius * 2f : CutRadius;
        Vector3 blade = mower.position;
        for (int i = 0; i < tufts.Count; i++)
        {
            if (cut[i] || FlatDistance(tufts[i].position, blade) > r) continue;
            cut[i] = true;
            cutCount++;
            // Short stubble sitting on the ground where the tall tuft stood.
            var t = tufts[i];
            float groundY = t.position.y - t.localScale.y * 0.5f;
            t.localScale = new Vector3(t.localScale.x, 0.06f, t.localScale.z);
            t.position = new Vector3(t.position.x, groundY + 0.03f, t.position.z);
            t.GetComponent<MeshRenderer>().sharedMaterial = cutMaterial;
        }
        if (stallsDone < StallAt.Length && Progress01 >= StallAt[stallsDone] && Progress01 < DoneAt)
        {
            stalled = true;
            stallsDone++;
            Puff.Spawn(Root, mower.position + Vector3.up * 0.6f);
            Manager.Toast("Sputter... the mower stalled! Press E and answer a question to restart it.");
            return;
        }
        if (Progress01 >= DoneAt)
        {
            IsComplete = true;
            if (mower != null) Object.Destroy(mower.gameObject);
            Manager.Toast("The lawn looks great! Go tell Mrs. Green.");
        }
    }

    public override string Prompt(Vector3 actor) => stalled && !restarting ? "[E]  Restart the mower (answer a question)" : null;

    public override bool Interact(Vector3 actor)
    {
        if (!stalled || restarting) return false;
        Restart();
        return true;
    }

    async void Restart()
    {
        restarting = true;
        bool ok = await Manager.AskAsync("Answer correctly to restart the mower", "Restart");
        restarting = false;
        if (Ended) return;
        if (!ok) { Manager.Toast("The mower's still stalled. Press E to try again."); return; }
        stalled = false;
        Manager.Toast("Vroom! The mower's running again.");
    }

    protected override void ApplyHelp() => turboUntil = Time.time + TurboSeconds;

    public override void Cleanup()
    {
        if (mower != null) Object.Destroy(mower.gameObject);
        base.Cleanup();
    }
}

/// <summary>A little puff of dark smoke that rises and fades (stalled mower).</summary>
public class Puff : MonoBehaviour
{
    float age;

    public static void Spawn(Transform parent, Vector3 position)
    {
        for (int i = 0; i < 4; i++)
        {
            var p = JobProps.Part(parent, "Smoke", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.3f, new Color(0.25f, 0.25f, 0.27f));
            p.position = position + Random.insideUnitSphere * 0.25f;
            p.gameObject.AddComponent<Puff>().age = -i * 0.12f;
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age < 0f) return;
        transform.position += Vector3.up * (0.8f * Time.deltaTime);
        transform.localScale = Vector3.one * (0.3f + age * 0.9f);
        if (age > 1.4f) Destroy(gameObject);
    }
}
