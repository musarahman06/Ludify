using System.Collections.Generic;
using Ludify.Map;
using UnityEngine;

/// <summary>
/// Suburb job: ride Pete's bike around the suburbs and throw a paper at 6 glowing mailboxes (E within ~10 m while
/// riding). Each household asks a practice question first: answer it to deliver (skipped if no lecture is imported).
/// 150 s for a +10 coin bonus; the clock pauses while a question is open. Help: exact markers + 30 extra seconds.
/// </summary>
public class NewspaperJob : Job
{
    const int Papers = 6;
    const float ThrowRange = 10f, TimeLimit = 150f, SearchRadius = 25f;

    static readonly string[] Families = { "the Parkers", "the Nguyens", "the Garcias", "the Smiths", "the Patels", "the Kims", "the Johnsons", "the Rossis" };

    sealed class Stop
    {
        public string Family;
        public Vector3 Front;
        public Transform Mailbox, Flag;
        public FastTravelPoint Marker;
        public bool Delivered, InFlight, Asking;
    }

    sealed class Flight
    {
        public Transform Paper;
        public Vector3 From;
        public Stop Target;
        public float T;
    }

    readonly List<JobPlaces.House> houses;
    readonly List<Stop> stops = new List<Stop>();
    readonly List<Flight> flights = new List<Flight>();
    readonly List<Transform> basketPapers = new List<Transform>();
    BikeController bike;
    float startTime = -1f, extraTime, pausedTotal, pauseStart = -1f;
    bool finishedInTime;

    public NewspaperJob(List<JobPlaces.House> suburbHouses) { houses = suburbHouses; }

    public override string Title => "Newspaper route";
    public override string Intro =>
        "Hey! I've got 6 papers to deliver and my legs are done for. Take my bike? Ride up to each glowing mailbox and " +
        "press E. Every family asks a quick question first, answer it right and the paper's theirs! " +
        "Do it in 2½ minutes and there's a bonus (the clock stops while you answer).";
    public override int Coins => 35;
    public override int Bonus => finishedInTime ? 10 : 0;
    public override string HelpOffer => "Answer a question and I'll mark every house exactly on your map, plus give you 30 more seconds.";
    public override string ThanksLine => finishedInTime ? "Wow, that was fast! Here's your pay, plus a bonus." : "All delivered, thanks! Here's your pay.";

    int Delivered { get { int n = 0; foreach (var s in stops) if (s.Delivered) n++; return n; } }
    public override float Progress01 => Delivered / (float)Papers;
    float Elapsed => startTime < 0f ? 0f : Time.time - startTime - pausedTotal - (pauseStart >= 0f ? Time.time - pauseStart : 0f);
    public override float? TimeLeft => IsComplete ? (float?)null : Mathf.Max(0f, TimeLimit + extraTime - Elapsed);

    public override string Objective =>
        IsComplete ? "All papers delivered! Ride back to Pete (green !)."
        : startTime < 0f ? "Hop on Pete's bike (E), then deliver 6 papers."
        : $"Papers delivered: {Delivered}/{Papers}\n" +
          (bike != null && bike.IsRidden ? "Ride near a glowing mailbox and press E. Answer the family's question to deliver." : "Get back on the bike (E).");

    protected override void Begin()
    {
        var rng = new System.Random();
        var pool = new List<JobPlaces.House>(houses);
        while (stops.Count < Papers && pool.Count > 0)
        {
            var h = pool[rng.Next(pool.Count)];
            pool.Remove(h);
            if (stops.Exists(s => FlatDistance(s.Front, h.Front) < 18f)) continue;   // spread them out
            var stop = new Stop { Front = h.Front, Family = Families[stops.Count % Families.Length] };
            stop.Mailbox = JobProps.Mailbox(Root, h.Front, Quaternion.LookRotation(-h.FacingDir), out stop.Flag);
            Vector2 off = Random.insideUnitCircle * SearchRadius * 0.6f;
            stop.Marker = AddMarker("Mailbox", "P", SearchColor, h.Front + new Vector3(off.x, 0f, off.y), SearchRadius);
            stops.Add(stop);
        }

        Vector3 spot = JobProps.ClearSpot(Giver.transform.position + Giver.transform.right * 2.5f, new Vector3(0.5f, 0.8f, 1f), null, 12f);
        bike = BikeController.Create(Root, spot + Vector3.up * 0.05f, Giver.transform.rotation);
        for (int i = 0; i < Papers; i++)
        {
            var p = JobProps.Part(bike.Basket, "BasketPaper", PrimitiveType.Cylinder, new Vector3(-0.12f + (i % 3) * 0.12f, 0.12f, i < 3 ? -0.06f : 0.06f),
                                  new Vector3(0.1f, 0.15f, 0.1f), new Color(0.93f, 0.93f, 0.88f), new Vector3(0f, 0f, 90f));
            basketPapers.Add(p);
        }
        Manager.Bike = bike;
    }

    public override Vector3? NextTarget(Vector3 from)
    {
        if (IsComplete) return Giver.transform.position;
        Stop best = null;
        float bestD = float.MaxValue;
        foreach (var s in stops)
        {
            if (s.Delivered || s.InFlight || s.Asking) continue;
            float d = FlatDistance(from, s.Front);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best?.Front;
    }

    Stop ThrowTarget(Vector3 actor)
    {
        Stop best = null;
        float bestD = ThrowRange;
        foreach (var s in stops)
        {
            if (s.Delivered || s.InFlight || s.Asking) continue;
            float d = FlatDistance(actor, s.Front);
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    public override string Prompt(Vector3 actor)
    {
        if (bike == null) return null;
        if (!bike.IsRidden)
            return FlatDistance(actor, bike.transform.position) < 2.8f ? "[E]  Ride the bike" : null;
        var t = IsComplete ? null : ThrowTarget(actor);
        if (t != null) return $"[E]  Deliver to {t.Family} (answer their question)";
        return Mathf.Abs(bike.Speed) < 0.6f ? "[E]  Get off the bike" : null;
    }

    public override bool Interact(Vector3 actor)
    {
        if (bike == null) return false;
        if (!bike.IsRidden)
        {
            if (FlatDistance(actor, bike.transform.position) >= 2.8f) return false;
            bike.Mount(Manager.Player, Manager.Camera);
            if (startTime < 0f) { startTime = Time.time; Manager.Toast("Go! Deliver 6 papers. Throw with E near a glowing mailbox."); }
            return true;
        }
        var target = IsComplete ? null : ThrowTarget(actor);
        if (target != null) { AskThenThrow(target); return true; }
        if (Mathf.Abs(bike.Speed) < 0.6f) { bike.Dismount(); return true; }
        return false;
    }

    /// <summary>The household's question gates the delivery. The bonus clock pauses while it's open.</summary>
    async void AskThenThrow(Stop target)
    {
        target.Asking = true;
        pauseStart = Time.time;
        bool ok = await Manager.AskAsync($"A question from {target.Family}: answer it to deliver their paper", "Deliver");
        pausedTotal += Time.time - pauseStart;
        pauseStart = -1f;
        target.Asking = false;
        if (Ended) return;
        if (!ok) { Manager.Toast($"No paper for {target.Family} yet. Ride back and try again!"); return; }
        Throw(target);
    }

    void Throw(Stop target)
    {
        target.InFlight = true;
        Vector3 from = bike.Basket.position + Vector3.up * 0.3f;
        if (basketPapers.Count > 0) { Object.Destroy(basketPapers[basketPapers.Count - 1].gameObject); basketPapers.RemoveAt(basketPapers.Count - 1); }
        flights.Add(new Flight { Paper = JobProps.Paper(Root, from), From = from, Target = target });
    }

    public override void Tick(Vector3 actor, float dt)
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i];
            f.T += dt / 0.6f;
            Vector3 to = f.Target.Mailbox.position + Vector3.up * 1.2f;
            Vector3 p = Vector3.Lerp(f.From, to, f.T) + Vector3.up * (Mathf.Sin(Mathf.Clamp01(f.T) * Mathf.PI) * 2.5f);
            f.Paper.position = p;
            f.Paper.Rotate(720f * dt, 0f, 0f, Space.Self);
            if (f.T < 1f) continue;
            Object.Destroy(f.Paper.gameObject);
            flights.RemoveAt(i);
            Land(f.Target);
        }
    }

    void Land(Stop s)
    {
        s.InFlight = false;
        s.Delivered = true;
        s.Flag.localRotation = Quaternion.identity;   // flag up
        var glow = s.Mailbox.Find("Glow");
        if (glow != null) Object.Destroy(glow.gameObject);
        RemoveMarker(s.Marker);
        s.Marker = null;

        int n = Delivered;
        if (n >= Papers)
        {
            finishedInTime = TimeLeft.GetValueOrDefault() > 0f;
            IsComplete = true;
            Manager.Toast(finishedInTime ? "All papers delivered in time! Ride back to Pete for your pay + bonus." : "All papers delivered! Ride back to Pete.");
        }
        else Manager.Toast($"Delivered to {s.Family}! {n}/{Papers}");
    }

    protected override void ApplyHelp()
    {
        extraTime += 30f;
        foreach (var s in stops)
        {
            if (s.Delivered) continue;
            RemoveMarker(s.Marker);
            s.Marker = AddMarker("Mailbox", "P", TargetColor, s.Front);
        }
    }

    public override void Cleanup()
    {
        if (bike != null && bike.IsRidden) bike.Dismount();
        if (Manager.Bike == bike) Manager.Bike = null;
        base.Cleanup();
    }
}
