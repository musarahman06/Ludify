using System.Collections.Generic;
using Ludify.Map;
using UnityEngine;

/// <summary>
/// Farm job: 12 of the farm's crop rows are ripe (golden markers). Harvest each with E (basket holds 6) and drop
/// the basket at the crate. Help: an exact map marker over every ripe crop left.
/// </summary>
public class HarvestJob : Job
{
    const int Ripe = 12, BasketSize = 6;

    sealed class Crop
    {
        public GameObject Go;
        public Bounds Bounds;
        public Transform Marker;
        public FastTravelPoint MapMarker;
        public bool Picked;
        public Renderer[] HiddenRenderers;
    }

    readonly Vector3 farmAnchor;
    readonly List<Crop> crops = new List<Crop>();
    Transform crate;
    FastTravelPoint fieldMarker;
    int inBasket, delivered;

    public HarvestJob(Vector3 farmhouse) { farmAnchor = farmhouse; }

    public override string Title => "Harvest the ripe crops";
    public override string Intro =>
        "Harvest time! 12 of my crop rows are ripe. Look for the golden markers, pick them with E, and bring them to the " +
        "crate by the farmhouse. The basket only holds 6, so you'll need two trips.";
    public override int Coins => 35;
    public override string HelpOffer => "Answer a question and I'll mark every ripe crop exactly on your map.";
    public override string ThanksLine => "What a harvest! Thank you. Here's your share.";

    public override float Progress01 => delivered / (float)Ripe;
    public override string Objective =>
        IsComplete ? "Harvest delivered! Go back to Farmer Joe (green !)."
        : $"Crops in the crate: {delivered}/{Ripe}\nBasket: {inBasket}/{BasketSize}" +
          (inBasket >= BasketSize ? "  <color=#FFB347><b>Full! Take it to the crate.</b></color>" : "");

    protected override void Begin()
    {
        var farm = GameObject.Find("FarmCrops");
        var candidates = new List<GameObject>();
        if (farm != null) foreach (Transform c in farm.transform) if (c.gameObject.activeInHierarchy) candidates.Add(c.gameObject);
        candidates.Sort((a, b) => FlatDistance(a.transform.position, farmAnchor).CompareTo(FlatDistance(b.transform.position, farmAnchor)));
        var near = candidates.GetRange(0, Mathf.Min(candidates.Count, 60));   // the rows closest to the farmhouse
        var rng = new System.Random();
        while (crops.Count < Ripe && near.Count > 0)
        {
            var go = near[rng.Next(near.Count)];
            near.Remove(go);
            var b = JobProps.BoundsOf(go);
            if (crops.Exists(c => FlatDistance(c.Bounds.center, b.center) < 5f)) continue;
            var crop = new Crop { Go = go, Bounds = b };
            crop.Marker = JobProps.Marker(Root, new Vector3(b.center.x, b.max.y, b.center.z), JobProps.Gold, 1.2f);
            crops.Add(crop);
        }

        Vector3 center = Vector3.zero;
        foreach (var c in crops) center += c.Bounds.center;
        center /= Mathf.Max(1, crops.Count);
        float radius = 15f;
        foreach (var c in crops) radius = Mathf.Max(radius, FlatDistance(center, c.Bounds.center) + 6f);
        fieldMarker = AddMarker("Ripe crops", "*", SearchColor, center, radius);

        Vector3 crateSpot = JobProps.ClearSpot(Giver.transform.position + Giver.transform.forward * 3f, new Vector3(0.8f, 0.5f, 0.6f), null, 15f);
        crate = JobProps.Crate(Root, crateSpot);
        AddMarker("Harvest crate", "C", new Color(0.8f, 0.55f, 0.25f), crateSpot);
    }

    Crop NearestRipe(Vector3 p, float within)
    {
        Crop best = null;
        float bestD = within;
        foreach (var c in crops)
        {
            if (c.Picked) continue;
            Vector3 closest = c.Bounds.ClosestPoint(new Vector3(p.x, c.Bounds.center.y, p.z));
            float d = FlatDistance(p, closest);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public override Vector3? NextTarget(Vector3 from)
    {
        if (IsComplete) return Giver.transform.position;
        bool anyLeft = crops.Exists(c => !c.Picked);
        if (inBasket >= BasketSize || (!anyLeft && inBasket > 0)) return crate.position;
        return NearestRipe(from, float.MaxValue)?.Bounds.center;
    }

    public override string Prompt(Vector3 actor)
    {
        if (IsComplete) return null;
        if (inBasket > 0 && FlatDistance(actor, crate.position) < 2.5f) return $"[E]  Put {inBasket} crops in the crate";
        if (inBasket < BasketSize && NearestRipe(actor, 2.5f) != null) return "[E]  Harvest";
        return null;
    }

    public override bool Interact(Vector3 actor)
    {
        if (IsComplete) return false;
        if (inBasket > 0 && FlatDistance(actor, crate.position) < 2.5f)
        {
            delivered += inBasket;
            inBasket = 0;
            if (delivered >= Ripe) { IsComplete = true; Manager.Toast("All crops harvested! Go back to Farmer Joe."); }
            else Manager.Toast($"Crate: {delivered}/{Ripe}. Back to the field!");
            return true;
        }
        var crop = inBasket < BasketSize ? NearestRipe(actor, 2.5f) : null;
        if (crop == null) return false;
        crop.Picked = true;
        // Crops are static-batched, so hide them via their renderers (restored on cleanup).
        crop.HiddenRenderers = crop.Go.GetComponentsInChildren<Renderer>();
        foreach (var r in crop.HiddenRenderers) r.enabled = false;
        if (crop.Marker != null) Object.Destroy(crop.Marker.gameObject);
        RemoveMarker(crop.MapMarker);
        inBasket++;
        Manager.Toast(inBasket >= BasketSize ? "Basket full! Take it to the crate." : $"Picked! Basket {inBasket}/{BasketSize}");
        return true;
    }

    protected override void ApplyHelp()
    {
        RemoveMarker(fieldMarker);
        foreach (var c in crops)
            if (!c.Picked) c.MapMarker = AddMarker("Ripe crop", "*", TargetColor, c.Bounds.center);
    }

    public override void Cleanup()
    {
        foreach (var c in crops)
            if (c.HiddenRenderers != null)
                foreach (var r in c.HiddenRenderers) if (r != null) r.enabled = true;
        base.Cleanup();
    }
}
