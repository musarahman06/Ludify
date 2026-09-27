using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludify.Import;
using Ludify.Map;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the suburb/farm jobs with the same flow as the city quests (QuestManager): givers show a yellow "!" (also
/// on the maps), you accept in a dialogue, the tracker shows progress and where to go, the giver offers one
/// question-gated "help" mid-job (blue "?"), and you turn in at a green "!" for coins + a star. One job at a time,
/// independent of city quests. E to talk / act.
/// </summary>
public class JobManager : MonoBehaviour
{
    const float TalkRange = 3f, TalkRangeOnBike = 4.5f;

    sealed class Giver
    {
        public Npc Npc;
        public Func<Job> Create;
        public FastTravelPoint Marker;
    }

    public PlayerController Player { get; private set; }
    public OrbitCamera Camera { get; private set; }
    /// <summary>The bike of the running newspaper job (null otherwise).</summary>
    public BikeController Bike { get; set; }

    readonly List<Giver> givers = new List<Giver>();
    DialogueBox dialogue;
    CityHud cityHud;
    JobHud hud;
    TimeTrialManager timeTrial;
    MapSystem map;
    Job active;
    Giver activeGiver;
    Npc talkingTo;
    bool asking;

    static readonly Color OfferColor = new Color(1f, 0.78f, 0.15f), TurnInColor = new Color(0.3f, 0.85f, 0.35f),
                          HelpColor = new Color(0.3f, 0.8f, 1f);

    public void Init(PlayerController player, DialogueBox box, CityHud city, JobHud jobHud)
    {
        Player = player;
        Camera = FindAnyObjectByType<OrbitCamera>();
        dialogue = box;
        cityHud = city;
        hud = jobHud;
    }

    public void AddGiver(Npc npc, Func<Job> create) => givers.Add(new Giver { Npc = npc, Create = create });

    public void Toast(string text, float seconds = 3.5f)
    {
        if (cityHud != null) cityHud.Toast(text, seconds);
    }

    bool OnBike => Bike != null && Bike.IsRidden;
    Vector3 Actor => OnBike ? Bike.transform.position : Player.transform.position;

    void OnDestroy()
    {
        foreach (var g in givers) if (g.Marker != null) MapMarkers.Remove(g.Marker);
        active?.Cleanup();
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (Player == null) return;
        timeTrial ??= FindAnyObjectByType<TimeTrialManager>();
        if (talkingTo != null && !dialogue.IsOpen && !asking) { talkingTo.EndTalk(); talkingTo = null; }

        active?.Tick(Actor, Time.deltaTime);
        SyncIcons();
        UpdateTracker();

        if (dialogue.IsOpen || !CanInteract()) { hud.SetPrompt(null); return; }

        // Talking wins, except beside your own giver mid-job when there's something to do (e.g. hop on Pete's bike).
        var near = NearestGiver();
        string jobPrompt = active?.Prompt(Actor);
        bool useJob = jobPrompt != null && (near == null || (near == activeGiver && !active.IsComplete));
        string prompt = useJob ? jobPrompt : near != null ? $"[E]  Talk to {near.Npc.DisplayName}" : null;
        hud.SetPrompt(prompt);

        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (useJob) { active.Interact(Actor); return; }
        if (near != null) Talk(near);
    }

    bool CanInteract()
    {
        if (!OnBike && (!Player.gameObject.activeInHierarchy || !Player.enabled)) return false;   // driving, map open, busy
        if (timeTrial != null && timeTrial.CurrentState != TimeTrialManager.State.Idle) return false;
        if (QuestionPrompt.IsOpen || LessonFilePicker.IsOpen || asking) return false;
        map ??= FindAnyObjectByType<MapSystem>();
        return map == null || !map.IsFullMapOpen;
    }

    Giver NearestGiver()
    {
        float range = OnBike ? TalkRangeOnBike : TalkRange;
        Giver best = null;
        float bestD = range * range;
        foreach (var g in givers)
        {
            float d = (g.Npc.transform.position - Actor).sqrMagnitude;
            if (d < bestD) { bestD = d; best = g; }
        }
        return best;
    }

    void SyncIcons()
    {
        foreach (var g in givers)
        {
            Npc.Icon icon = active == null ? Npc.Icon.QuestAvailable
                : g != activeGiver ? Npc.Icon.None
                : active.IsComplete ? Npc.Icon.TurnIn
                : !active.HelpUsed ? Npc.Icon.Hint : Npc.Icon.None;
            if (g.Npc.CurrentIcon != icon) g.Npc.SetIcon(icon);

            Color? color = icon == Npc.Icon.QuestAvailable ? OfferColor : icon == Npc.Icon.TurnIn ? TurnInColor
                         : icon == Npc.Icon.Hint ? HelpColor : (Color?)null;
            if (g.Marker != null && (!color.HasValue || g.Marker.Color != color.Value)) { MapMarkers.Remove(g.Marker); g.Marker = null; }
            if (g.Marker == null && color.HasValue)
            {
                g.Marker = new FastTravelPoint
                {
                    Name = g.Npc.DisplayName, Glyph = icon == Npc.Icon.Hint ? "?" : "!", Color = color.Value, Follow = g.Npc.transform,
                };
                MapMarkers.Add(g.Marker);
            }
        }
    }

    void UpdateTracker()
    {
        if (active == null) { hud.SetTracker(null, null, 0f); return; }
        string body = active.Objective;
        float? left = active.TimeLeft;
        if (left.HasValue)
            body += left.Value > 0f ? $"\n<color=#FFD24D>Bonus time: {Mathf.CeilToInt(left.Value)} s</color>" : "\n<color=#AAAAAA>Out of bonus time, but keep going!</color>";
        Vector3? next = active.NextTarget(Actor);
        if (next.HasValue && Job_FlatDistance(Actor, next.Value) > 6f)
            body += $"\n<color=#5CD9FF><b>Next: {CityArea.DescribeDirection(Actor, next.Value, "you")}</b></color>";
        if (active.QuestionsAnswered > 0)
            body += $"\n<color=#B9F27C>Questions answered: {active.QuestionsAnswered}</color>";
        if (!active.IsComplete && !active.HelpUsed)
            body += $"\n<size=85%>Stuck? Talk to {activeGiver.Npc.DisplayName} (blue ?) for help.</size>";
        hud.SetTracker(active.Title, body, active.Progress01);
    }

    static float Job_FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    // ------------------------------------------------------------------ talking

    void Talk(Giver g)
    {
        if (OnBike) Bike.Dismount();
        talkingTo = g.Npc;
        g.Npc.BeginTalk(Player.transform);
        string who = g.Npc.DisplayName;

        if (active == null)
        {
            var job = g.Create();
            dialogue.Show(who, job.Intro,
                ("Sure, I'll do it!", () => Accept(g, job)),
                ("Not right now", null));
            return;
        }
        if (g != activeGiver)
        {
            dialogue.Show(who, $"You're busy helping {activeGiver.Npc.DisplayName} right now. Come back when you're done!", ("OK!", null));
            return;
        }
        if (active.IsComplete) { Complete(); return; }

        var options = new List<(string, Action)>();
        if (!active.HelpUsed) options.Add(("Help me out!", () => AskForHelp(g)));
        options.Add(("I'm on it!", null));
        options.Insert(options.Count - 1, ("Sorry, I quit", GiveUp));
        string line = active.HelpUsed ? "How's it going? You're doing great!" : $"How's it going? Need a hand? {active.HelpOffer}";
        dialogue.Show(who, line, options.ToArray());
    }

    void Accept(Giver g, Job job)
    {
        activeGiver = g;
        active = job;
        job.Start(this, g.Npc);
        Toast($"New job: {job.Title}");
    }

    /// <summary>
    /// Asks a practice question from the imported lectures (Musa's QuestionPrompt keeps asking until one is right).
    /// Returns true when answered correctly, or straight away if no lecture is imported; false if cancelled.
    /// The player and bike stand still while it's open. Correct answers count toward the job's tally.
    /// </summary>
    public async Task<bool> AskAsync(string title, string button)
    {
        if (!QuestionPool.HasQuestions) return true;
        var job = active;
        asking = true;
        if (Bike != null) Bike.Stop();
        bool wasEnabled = Player.enabled;
        Player.enabled = false;
        QuestionResult result;
        try { result = await QuestionPrompt.AskAsync(title, button); }
        finally
        {
            asking = false;
            if (Player != null && wasEnabled) Player.enabled = true;
        }
        if (result == QuestionResult.Cancelled) return false;
        if (job != null) job.QuestionsAnswered++;
        return true;
    }

    /// <summary>Help costs a correct answer to a practice question (free if no lecture), like the city quest hints.</summary>
    async void AskForHelp(Giver g)
    {
        var job = active;
        g.Npc.BeginTalk(Player.transform);
        bool ok = await AskAsync($"Answer correctly to get help from {g.Npc.DisplayName}", "Get help");
        if (this == null || active != job) return;
        g.Npc.EndTalk();
        if (!ok) { Toast($"No help yet. Talk to {g.Npc.DisplayName} again when you're ready."); return; }
        job.UseHelp();
        Toast(QuestionPool.HasQuestions ? "Correct! Help is on." : "Help is on! (No lecture imported, so it's free.)");
    }

    void Complete()
    {
        var job = active;
        int coins = job.Coins + job.Bonus;
        dialogue.Show(activeGiver.Npc.DisplayName, $"{job.ThanksLine}  (+{coins} coins, +1 star)", ("Happy to help!", null));
        string before = PlayerProgress.Title;
        PlayerProgress.AddQuestReward(coins);
        string promoted = PlayerProgress.Title != before ? $"   You're now a {PlayerProgress.Title}!" : "";
        Toast($"Job done: {job.Title}  +{coins} coins  +1 star{promoted}", 5f);
        job.Cleanup();
        active = null;
        activeGiver = null;
    }

    void GiveUp()
    {
        active?.Cleanup();
        active = null;
        activeGiver = null;
        Toast("Job cancelled");
    }
}
