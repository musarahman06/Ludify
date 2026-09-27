using System.Collections.Generic;
using Ludify.Map;
using UnityEngine;

/// <summary>
/// One suburb/farm minigame, run by <see cref="JobManager"/> with the same flow as the city quests:
/// yellow "!" giver → accept → do the task (tracker + map circles + direction) → green "!" → coins + a star.
/// Talking to the giver mid-job offers a question-gated "help" (see <see cref="ApplyHelp"/>).
/// </summary>
public abstract class Job
{
    public JobManager Manager { get; private set; }
    public Npc Giver { get; private set; }
    /// <summary>Everything this job spawns lives under here and is destroyed on cleanup.</summary>
    protected Transform Root { get; private set; }

    public abstract string Title { get; }
    public abstract string Intro { get; }
    public abstract int Coins { get; }
    /// <summary>What the giver offers when you ask for help, e.g. "I'll mark every house on your map."</summary>
    public abstract string HelpOffer { get; }
    public virtual string ThanksLine => "Great work, thank you! Here's your pay.";
    public virtual int Bonus => 0;

    public bool IsComplete { get; protected set; }
    public bool HelpUsed { get; private set; }
    /// <summary>Practice questions answered correctly during this job.</summary>
    public int QuestionsAnswered { get; set; }
    /// <summary>True once the job has been cleaned up (finished or cancelled), for async callbacks.</summary>
    public bool Ended { get; private set; }

    public abstract string Objective { get; }
    public abstract float Progress01 { get; }
    public virtual float? TimeLeft => null;
    /// <summary>Where to head next (for the "north-west of you, about 40 m" line), or null.</summary>
    public virtual Vector3? NextTarget(Vector3 from) => null;

    readonly List<FastTravelPoint> markers = new List<FastTravelPoint>();

    public void Start(JobManager manager, Npc giver)
    {
        Manager = manager;
        Giver = giver;
        Root = new GameObject("Job_" + GetType().Name).transform;
        Root.SetParent(manager.transform, false);
        Begin();
    }

    protected abstract void Begin();
    public virtual void Tick(Vector3 actor, float dt) { }
    /// <summary>"[E] ..." text if there's something to do right here, else null.</summary>
    public virtual string Prompt(Vector3 actor) => null;
    /// <summary>Handles E. Return true if the key was used.</summary>
    public virtual bool Interact(Vector3 actor) => false;

    public void UseHelp() { HelpUsed = true; ApplyHelp(); }
    protected abstract void ApplyHelp();

    public virtual void Cleanup()
    {
        Ended = true;
        foreach (var m in markers) MapMarkers.Remove(m);
        markers.Clear();
        if (Root != null) Object.Destroy(Root.gameObject);
    }

    // ------------------------------------------------------------------ helpers for jobs

    protected FastTravelPoint AddMarker(string name, string glyph, Color color, Vector3 position, float radius = 0f, Transform follow = null)
    {
        var m = new FastTravelPoint { Name = name, Glyph = glyph, Color = color, Position = position, Radius = radius, Follow = follow };
        markers.Add(m);
        MapMarkers.Add(m);
        return m;
    }

    protected void RemoveMarker(FastTravelPoint m)
    {
        if (m == null) return;
        markers.Remove(m);
        MapMarkers.Remove(m);
    }

    protected static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    protected static readonly Color SearchColor = new Color(0.35f, 0.85f, 1f);
    protected static readonly Color TargetColor = new Color(1f, 0.78f, 0.15f);
}
