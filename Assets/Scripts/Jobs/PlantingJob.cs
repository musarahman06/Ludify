using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Farm job: 6 soil plots. For each: plant a seed (E), fill the watering can at the well (holds 3), water it (E),
/// wait while it grows (~25 s), then harvest (E). Help: fertiliser that instantly grows every watered plot.
/// </summary>
public class PlantingJob : Job
{
    const int PlotCount = 6, CanSize = 3;
    const float GrowSeconds = 25f, Reach = 1.8f;

    enum State { Empty, Planted, Growing, Grown, Harvested }

    sealed class Plot
    {
        public Transform Root, Seed, Plant, Veggie;
        public Renderer Soil;
        public State State;
        public float Growth;
    }

    readonly Vector3 farmAnchor;
    readonly List<Plot> plots = new List<Plot>();
    Transform well;
    int water;
    Vector3 gardenCenter;

    public PlantingJob(Vector3 farmhouse) { farmAnchor = farmhouse; }

    public override string Title => "Plant the vegetable garden";
    public override string Intro =>
        "Want to grow some veggies? Plant a seed in each of my 6 plots, fill the watering can at the well, water them, " +
        "and they'll sprout. Harvest them once they're fully grown!";
    public override int Coins => 40;
    public override string HelpOffer => "Answer a question and I'll give you my special fertiliser. Every watered plot will be ready right away!";
    public override string ThanksLine => "Look at all those veggies! You've got a green thumb. Here's your pay.";

    int Count(State s) { int n = 0; foreach (var p in plots) if (p.State == s) n++; return n; }
    public override float Progress01
    {
        get
        {
            float sum = 0f;
            foreach (var p in plots)
                sum += p.State == State.Harvested ? 1f : p.State == State.Grown ? 0.85f : p.State == State.Growing ? 0.3f + 0.55f * p.Growth
                     : p.State == State.Planted ? 0.15f : 0f;
            return sum / PlotCount;
        }
    }

    public override string Objective
    {
        get
        {
            if (IsComplete) return "Garden harvested! Go back to Gardener Sam (green !).";
            string step = Count(State.Grown) > 0 ? "Some veggies are ready to harvest!"
                : Count(State.Empty) > 0 ? "Plant seeds in the empty plots."
                : Count(State.Planted) > 0 ? (water > 0 ? "Water the planted seeds." : "Fill the watering can at the well.")
                : "Wait for the plants to grow...";
            return $"Harvested: {Count(State.Harvested)}/{PlotCount}   Growing: {Count(State.Growing)}\nWatering can: {water}/{CanSize}\n{step}";
        }
    }

    protected override void Begin()
    {
        var avoid = JobProps.RendererBounds("FarmCrops");
        avoid.AddRange(JobProps.RendererBounds("FarmHouse"));
        avoid.AddRange(JobProps.RendererBounds(FarmDressing.RootName));
        // The farm dressing keeps a garden corner free in the barnyard; otherwise find open ground.
        var gardenMarker = GameObject.Find("FarmGardenCenter");
        gardenCenter = gardenMarker != null ? gardenMarker.transform.position
            : JobProps.ClearSpot(farmAnchor + new Vector3(-12f, 0f, -14f), new Vector3(3.6f, 0.6f, 2.6f), avoid);
        for (int i = 0; i < PlotCount; i++)
        {
            Vector3 p = JobProps.Ground(gardenCenter + new Vector3((i % 3 - 1) * 2.3f, 0f, (i / 3 - 0.5f) * 2.3f));
            var root = JobProps.Plot(Root, p);
            plots.Add(new Plot { Root = root, Soil = root.GetComponentInChildren<Renderer>() });
        }
        Vector3 wellSpot = gardenMarker != null ? JobProps.Ground(gardenCenter + new Vector3(5.2f, 0f, 1.5f))
            : JobProps.ClearSpot(gardenCenter + new Vector3(5.5f, 0f, 0f), new Vector3(0.9f, 1f, 0.9f), avoid, 20f);
        well = JobProps.Well(Root, wellSpot);
        AddMarker("Garden", "G", SearchColor, gardenCenter, 8f);
        AddMarker("Well", "W", new Color(0.3f, 0.55f, 0.95f), wellSpot);
    }

    Plot NearestPlot(Vector3 p)
    {
        Plot best = null;
        float bestD = Reach;
        foreach (var plot in plots)
        {
            float d = FlatDistance(p, plot.Root.position);
            if (d < bestD) { bestD = d; best = plot; }
        }
        return best;
    }

    public override Vector3? NextTarget(Vector3 from)
    {
        if (IsComplete) return Giver.transform.position;
        if (water == 0 && Count(State.Planted) > 0 && Count(State.Empty) == 0 && Count(State.Grown) == 0) return well.position;
        return FlatDistance(from, gardenCenter) > 6f ? gardenCenter : (Vector3?)null;
    }

    public override string Prompt(Vector3 actor)
    {
        if (IsComplete) return null;
        if (water < CanSize && FlatDistance(actor, well.position) < 2.5f) return "[E]  Fill the watering can";
        var plot = NearestPlot(actor);
        if (plot == null) return null;
        switch (plot.State)
        {
            case State.Empty: return "[E]  Plant a seed";
            case State.Planted: return water > 0 ? "[E]  Water the seed" : "Fill the watering can at the well first";
            case State.Growing: return $"Growing... {Mathf.FloorToInt(plot.Growth * 100f)}%";
            case State.Grown: return "[E]  Harvest";
            default: return null;
        }
    }

    public override bool Interact(Vector3 actor)
    {
        if (IsComplete) return false;
        if (water < CanSize && FlatDistance(actor, well.position) < 2.5f)
        {
            water = CanSize;
            Manager.Toast("Watering can filled!");
            return true;
        }
        var plot = NearestPlot(actor);
        if (plot == null) return false;
        switch (plot.State)
        {
            case State.Empty:
                plot.State = State.Planted;
                plot.Seed = JobProps.Part(plot.Root, "Seed", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.15f, 0.5f), JobProps.WetSoil);
                return true;
            case State.Planted when water > 0:
                water--;
                plot.State = State.Growing;
                plot.Soil.sharedMaterial = JobProps.Mat(JobProps.WetSoil);
                plot.Plant = new GameObject("Plant").transform;
                plot.Plant.SetParent(plot.Root, false);
                plot.Plant.localPosition = new Vector3(0f, 0.16f, 0f);
                JobProps.Part(plot.Plant, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.08f, 0.3f, 0.08f), JobProps.Leaf);
                JobProps.Part(plot.Plant, "LeafA", PrimitiveType.Cube, new Vector3(0.2f, 0.45f, 0f), new Vector3(0.4f, 0.04f, 0.18f), JobProps.Leaf, new Vector3(0f, 0f, 20f));
                JobProps.Part(plot.Plant, "LeafB", PrimitiveType.Cube, new Vector3(-0.2f, 0.55f, 0f), new Vector3(0.4f, 0.04f, 0.18f), JobProps.Leaf, new Vector3(0f, 0f, -20f));
                plot.Plant.localScale = Vector3.one * 0.15f;
                return true;
            case State.Grown:
                plot.State = State.Harvested;
                if (plot.Plant != null) Object.Destroy(plot.Plant.gameObject);
                if (plot.Seed != null) Object.Destroy(plot.Seed.gameObject);
                plot.Soil.sharedMaterial = JobProps.Mat(JobProps.Soil);
                int done = Count(State.Harvested);
                if (done >= PlotCount) { IsComplete = true; Manager.Toast("Everything's harvested! Go back to Gardener Sam."); }
                else Manager.Toast($"Harvested! {done}/{PlotCount}");
                return true;
        }
        return false;
    }

    public override void Tick(Vector3 actor, float dt)
    {
        foreach (var p in plots)
            if (p.State == State.Growing) Grow(p, p.Growth + dt / GrowSeconds);
    }

    void Grow(Plot p, float growth)
    {
        p.Growth = Mathf.Clamp01(growth);
        if (p.Plant != null) p.Plant.localScale = Vector3.one * Mathf.Lerp(0.15f, 1.3f, p.Growth);
        if (p.Growth < 1f) return;
        p.State = State.Grown;
        // A ripe pumpkin at the base shows it's ready.
        p.Veggie = JobProps.Part(p.Root, "Pumpkin", PrimitiveType.Sphere, new Vector3(0.25f, 0.35f, 0.1f), new Vector3(0.5f, 0.38f, 0.5f), new Color(0.95f, 0.55f, 0.1f));
        if (p.Plant != null) p.Veggie.SetParent(p.Plant, true);
    }

    protected override void ApplyHelp()
    {
        foreach (var p in plots)
            if (p.State == State.Growing) Grow(p, 1f);
    }
}
