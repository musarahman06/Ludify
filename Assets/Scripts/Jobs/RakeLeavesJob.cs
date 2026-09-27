using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Farm job: rake up the leaves scattered around the farmyard. Walk over them with the rake; the bag holds 12,
/// so empty it in the compost bin (E). Help: a leaf blower that pulls in leaves within 6 m for 15 s.
/// </summary>
public class RakeLeavesJob : Job
{
    const int ClusterCount = 40, BagSize = 12;
    const float RakeRadius = 1.3f, BlowerRadius = 6f, BlowerSeconds = 15f;

    readonly Vector3 yardCenter;
    readonly List<Transform> leaves = new List<Transform>();
    Transform rake, bin;
    int inBag, binned;
    float blowerUntil;

    public RakeLeavesJob(Vector3 farmyardCenter) { yardCenter = farmyardCenter; }

    public override string Title => "Rake the farmyard";
    public override string Intro =>
        "The wind blew leaves all over the farmyard! Grab the rake and sweep them up. Your bag holds 12 leaves, " +
        "so empty it in the green compost bin whenever it's full.";
    public override int Coins => 30;
    public override string HelpOffer => "Answer a question and you can borrow my leaf blower. It sucks up leaves from 6 m away for 15 seconds.";
    public override string ThanksLine => "Spotless! Thanks for the help. Here you go.";

    public override float Progress01 => binned / (float)ClusterCount;
    public override string Objective =>
        IsComplete ? "Farmyard clean! Go back to Rosa (green !)."
        : $"Leaves in the bin: {binned}/{ClusterCount}\nBag: {inBag}/{BagSize}" +
          (inBag >= BagSize ? "  <color=#FFB347><b>Full! Empty it in the compost bin.</b></color>" : "") +
          (Time.time < blowerUntil ? $"\n<color=#FFD24D><b>Leaf blower: {Mathf.CeilToInt(blowerUntil - Time.time)} s</b></color>" : "");

    protected override void Begin()
    {
        var rng = new System.Random();
        for (int i = 0; i < ClusterCount; i++)
        {
            Vector3 p = yardCenter;
            for (int tries = 0; tries < 10; tries++)
            {
                p = JobProps.Ground(yardCenter + new Vector3(((float)rng.NextDouble() - 0.5f) * 34f, 0f, ((float)rng.NextDouble() - 0.5f) * 24f));
                if (!Physics.CheckSphere(p + Vector3.up * 0.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) break;
            }
            leaves.Add(JobProps.LeafCluster(Root, p, rng));
        }
        Vector3 binSpot = JobProps.ClearSpot(Giver.transform.position + Giver.transform.forward * 3f, new Vector3(0.7f, 0.6f, 0.7f), null, 15f);
        bin = JobProps.CompostBin(Root, binSpot);
        rake = JobProps.Rake(Manager.Player.transform);
        AddMarker("Leaves", "L", SearchColor, yardCenter, 20f);
        AddMarker("Compost bin", "B", new Color(0.3f, 0.75f, 0.35f), binSpot);
    }

    public override Vector3? NextTarget(Vector3 from)
    {
        if (IsComplete) return Giver.transform.position;
        if (inBag >= BagSize || (inBag > 0 && leaves.Count == 0)) return bin.position;
        Transform best = null;
        float bestD = float.MaxValue;
        foreach (var l in leaves)
        {
            float d = FlatDistance(from, l.position);
            if (d < bestD) { bestD = d; best = l; }
        }
        return best != null ? best.position : (Vector3?)null;
    }

    public override void Tick(Vector3 actor, float dt)
    {
        if (IsComplete || rake == null || !rake.gameObject.activeInHierarchy) return;
        Vector3 head = rake.TransformPoint(new Vector3(0f, 0f, 0.95f));
        float r = Time.time < blowerUntil ? BlowerRadius : RakeRadius;
        for (int i = leaves.Count - 1; i >= 0 && inBag < BagSize; i--)
        {
            if (FlatDistance(leaves[i].position, head) > r) continue;
            Object.Destroy(leaves[i].gameObject);
            leaves.RemoveAt(i);
            inBag++;
            if (inBag == BagSize) Manager.Toast("Bag full! Empty it in the green compost bin.");
        }
    }

    public override string Prompt(Vector3 actor) =>
        !IsComplete && inBag > 0 && FlatDistance(actor, bin.position) < 2.5f ? $"[E]  Empty the bag ({inBag} leaves)" : null;

    public override bool Interact(Vector3 actor)
    {
        if (Prompt(actor) == null) return false;
        binned += inBag;
        inBag = 0;
        if (binned >= ClusterCount)
        {
            IsComplete = true;
            if (rake != null) Object.Destroy(rake.gameObject);
            Manager.Toast("All the leaves are composted! Go back to Rosa.");
        }
        else Manager.Toast($"Bag emptied. {ClusterCount - binned} leaves to go.");
        return true;
    }

    protected override void ApplyHelp() => blowerUntil = Time.time + BlowerSeconds;

    public override void Cleanup()
    {
        if (rake != null) Object.Destroy(rake.gameObject);
        base.Cleanup();
    }
}
