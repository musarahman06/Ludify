using System.Collections.Generic;
using System.Linq;
using Ludify.Import;
using Ludify.Map;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Kind = QuestTemplates.Kind;

/// <summary>
/// Runs the little city quests: a few NPCs offer one (shown with "!"), you accept, other NPCs give hints ("?"),
/// you find the pet/item/pages (clues: paw prints, sparkles, "Meow!"), return it (green "!"), and get coins + a star.
/// Also handles talking to NPCs with E.
/// </summary>
public class QuestManager : MonoBehaviour
{
    const int OffersAtOnce = 3;
    const float TalkRange = 3f, PickupRange = 2.5f;
    const int PagesNeeded = 5;

    sealed class Offer
    {
        public Kind Kind;
        public string PetName, Coat, Item;
        public bool Dog;
        public Color CoatColor;
    }

    sealed class Active
    {
        public Offer Offer;
        public Npc Giver, Recipient;
        public string Title;
        public readonly List<QuestTarget> Targets = new List<QuestTarget>();
        public GameObject Trail;
        public int Found;
        public readonly List<Npc> Hinters = new List<Npc>();
        public readonly List<string> Hints = new List<string>();
        public bool ReadyToTurnIn;
        public Vector3 HintAnchor;
        /// <summary>Search circle on the map, smaller with each hint.</summary>
        public FastTravelPoint SearchArea;
        /// <summary>Floating "?" over the building named in hint 2.</summary>
        public GameObject Beacon;
        public int Needed => Offer.Kind == Kind.Pages ? PagesNeeded : 1;
    }

    static QuestManager instance;

    /// <summary>Where the player is (null while driving), for proximity cues like "Meow!".</summary>
    public static Vector3? PlayerPosition =>
        instance != null && instance.player != null && instance.player.gameObject.activeInHierarchy ? instance.player.transform.position : (Vector3?)null;

    List<Npc> npcs;
    DialogueBox dialogue;
    CityHud hud;
    PlayerController player;
    TimeTrialManager timeTrial;
    Ludify.Map.MapSystem map;
    readonly Dictionary<Npc, Offer> offers = new Dictionary<Npc, Offer>();
    Active active;
    Npc talkingTo, asking;
    float nextOfferTime;

    public void Init(List<Npc> residents, DialogueBox box, CityHud cityHud, PlayerController playerController)
    {
        instance = this;
        npcs = residents;
        dialogue = box;
        hud = cityHud;
        player = playerController;
        for (int i = 0; i < OffersAtOnce; i++) AddOffer();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (var marker in mapMarkers.Values) MapMarkers.Remove(marker);
    }

    // ------------------------------------------------------------------ map

    static readonly Color OfferColor = new Color(1f, 0.78f, 0.15f), TurnInColor = new Color(0.3f, 0.85f, 0.35f),
                          HintColor = new Color(0.3f, 0.8f, 1f), SearchColor = new Color(0.35f, 0.85f, 1f);
    readonly Dictionary<Npc, FastTravelPoint> mapMarkers = new Dictionary<Npc, FastTravelPoint>();

    /// <summary>People with a quest for you (and the person to hand one back to) are marked on the minimap and
    /// full map, where you can click them to fast travel.</summary>
    void SyncMapMarkers()
    {
        foreach (var npc in npcs)
        {
            Color? color = offers.ContainsKey(npc) ? OfferColor
                : active != null && active.ReadyToTurnIn && npc == active.Giver ? TurnInColor
                : active != null && !active.ReadyToTurnIn && active.Hinters.Contains(npc) ? HintColor
                : (Color?)null;
            mapMarkers.TryGetValue(npc, out var marker);
            if (marker != null && (!color.HasValue || marker.Color != color.Value))
            {
                MapMarkers.Remove(marker);
                mapMarkers.Remove(npc);
                marker = null;
            }
            if (marker == null && color.HasValue)
            {
                string glyph = color.Value == HintColor ? "?" : "!";
                marker = new FastTravelPoint { Name = npc.DisplayName, Glyph = glyph, Color = color.Value, Follow = npc.transform };
                mapMarkers[npc] = marker;
                MapMarkers.Add(marker);
            }
        }
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (player == null) return;
        timeTrial ??= FindAnyObjectByType<TimeTrialManager>();

        if (talkingTo != null && !dialogue.IsOpen && asking == null) { talkingTo.EndTalk(); talkingTo = null; }

        if (active == null && offers.Count < OffersAtOnce && Time.time >= nextOfferTime) AddOffer();
        UpdateDeliveryIcon();
        UpdateTracker();
        SyncMapMarkers();

        if (dialogue.IsOpen || !CanInteract()) { hud.SetPrompt(null); return; }

        // Something to pick up beats someone to talk to.
        QuestTarget target = NearestTarget();
        Npc npc = target == null ? NearestNpc() : null;
        if (target != null) hud.SetPrompt($"[E]  Pick up {(target.TargetKind == QuestTarget.Kind.Pet ? target.Label : "the " + target.Label)}");
        else if (npc != null) hud.SetPrompt($"[E]  Talk to {npc.DisplayName}");
        else hud.SetPrompt(null);

        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;
        if (target != null) PickUp(target);
        else if (npc != null) Talk(npc);
    }

    bool CanInteract()
    {
        if (!player.gameObject.activeInHierarchy || !player.enabled) return false;   // driving, or the full map is open
        if (timeTrial != null && timeTrial.CurrentState != TimeTrialManager.State.Idle) return false;
        if (QuestionPrompt.IsOpen || LessonFilePicker.IsOpen) return false;
        map ??= FindAnyObjectByType<Ludify.Map.MapSystem>();
        return map == null || !map.IsFullMapOpen;
    }

    Npc NearestNpc()
    {
        Npc best = null;
        float bestDist = TalkRange * TalkRange;
        Vector3 p = player.transform.position;
        foreach (var n in npcs)
        {
            float d = (n.transform.position - p).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = n; }
        }
        return best;
    }

    QuestTarget NearestTarget()
    {
        if (active == null) return null;
        Vector3 p = player.transform.position;
        foreach (var t in active.Targets)
            if (t != null && !t.PickedUp && (t.transform.position - p).sqrMagnitude < PickupRange * PickupRange) return t;
        return null;
    }

    // ------------------------------------------------------------------ talking

    void Talk(Npc npc)
    {
        talkingTo = npc;
        npc.BeginTalk(player.transform);

        if (active != null)
        {
            if (npc == active.Giver) { TalkToGiver(); return; }
            if (active.Offer.Kind == Kind.Delivery && npc == active.Recipient) { Deliver(); return; }
            if (active.Hinters.Contains(npc) && !active.ReadyToTurnIn) { OfferHint(npc); return; }
            if (offers.ContainsKey(npc))
            {
                dialogue.Show(npc.DisplayName, $"I could really use some help too, but you're already helping {active.Giver.DisplayName}. Come back when you're done!",
                    ("OK, I'll be back!", null));
                return;
            }
        }
        else if (offers.TryGetValue(npc, out var offer))
        {
            string recipient = offer.Kind == Kind.Delivery ? PickRecipient(npc)?.DisplayName ?? "my friend" : null;
            dialogue.Show(npc.DisplayName, QuestTemplates.Intro(offer.Kind, offer.PetName, offer.Dog, offer.Coat, offer.Item, recipient),
                ("Sure, I'll help!", () => Accept(npc, offer)),
                ("Not right now", null));
            return;
        }

        dialogue.Show(npc.DisplayName, QuestTemplates.SmallTalk[Random.Range(0, QuestTemplates.SmallTalk.Length)], ("Bye!", null));
    }

    void TalkToGiver()
    {
        var q = active;
        if (q.ReadyToTurnIn) { Complete(); return; }
        string waiting = q.Offer.Kind == Kind.Delivery ? $"Did you find {q.Recipient?.DisplayName} yet?"
            : q.Offer.Kind == Kind.Pages ? $"Any luck? You've found {q.Found} of {PagesNeeded} pages so far."
            : $"Any luck finding {(q.Offer.Kind == Kind.LostPet ? q.Offer.PetName : "my " + q.Offer.Item)}? Try asking people around town.";
        dialogue.Show(q.Giver.DisplayName, waiting,
            ("Still looking!", null),
            ("Sorry, I give up", GiveUp));
    }

    /// <summary>Hints cost a correct answer to a practice question from the imported lectures (free if none).</summary>
    void OfferHint(Npc npc)
    {
        if (!QuestionPool.HasQuestions) { GiveHint(npc); return; }
        dialogue.Show(npc.DisplayName, "Oh, I think I saw something! Answer my question and I'll tell you what I know.",
            ("OK, ask me!", () => AskForHint(npc, active)),
            ("Maybe later", null));
    }

    async void AskForHint(Npc npc, Active q)
    {
        asking = npc;
        npc.BeginTalk(player.transform);
        bool wasEnabled = player.enabled;
        player.enabled = false;   // stand still while answering
        QuestionResult result;
        try
        {
            result = await QuestionPrompt.AskAsync($"Answer correctly to get {npc.DisplayName}'s hint", "Get hint");
        }
        finally
        {
            asking = null;
            if (player != null && wasEnabled) player.enabled = true;
        }
        if (this == null || active != q || npc == null) return;
        if (result == QuestionResult.Cancelled)
        {
            npc.EndTalk();
            hud.Toast($"No hint yet. Talk to {npc.DisplayName} again when you're ready.");
            return;
        }
        GiveHint(npc);
    }

    static readonly float[] SearchRadius = { 50f, 30f, 12f };

    void GiveHint(Npc npc)
    {
        var q = active;
        talkingTo = npc;
        npc.BeginTalk(player.transform);
        int level = Mathf.Min(q.Hints.Count, SearchRadius.Length - 1);
        bool delivery = q.Offer.Kind == Kind.Delivery && q.Recipient != null;
        Vector3 anchor = delivery ? q.Recipient.transform.position : q.HintAnchor;

        // Hint 2 points at one building you can see from far away: it gets a floating "?".
        CityColorizer.Building? landmark = null;
        if (!delivery && level == 1)
        {
            landmark = CityColorizer.NearestBuilding(anchor);
            if (landmark.HasValue)
            {
                if (q.Beacon != null) Destroy(q.Beacon);
                q.Beacon = QuestTargets.MakeBeacon(landmark.Value.Bounds, anchor);
            }
        }

        string noun = QuestTemplates.Noun(q.Offer.Kind, q.Offer.Dog, q.Offer.Coat, q.Offer.Item);
        string hint = QuestTemplates.Hint(q.Offer.Kind, level, noun, q.Recipient?.DisplayName, anchor, npc.transform.position, landmark);
        q.Hints.Add(hint);
        q.Hinters.Remove(npc);
        npc.SetIcon(Npc.Icon.None);
        SetSearchArea(q, anchor, SearchRadius[level], delivery ? q.Recipient.transform : null);
        dialogue.Show(npc.DisplayName, hint, ("Thanks!", null));
    }

    /// <summary>Draws the search circle on the maps. The target is inside it but not at the centre.</summary>
    void SetSearchArea(Active q, Vector3 target, float radius, Transform follow)
    {
        if (q.SearchArea != null) MapMarkers.Remove(q.SearchArea);
        Vector2 offset = Random.insideUnitCircle * radius * 0.6f;
        q.SearchArea = new FastTravelPoint
        {
            Name = "Search here",
            Glyph = "?",
            Color = SearchColor,
            Radius = radius,
            Position = follow != null ? target : target + new Vector3(offset.x, 0f, offset.y),
            Follow = follow,   // deliveries: the circle moves with the person you're looking for
        };
        MapMarkers.Add(q.SearchArea);
    }

    // ------------------------------------------------------------------ quest flow

    void AddOffer()
    {
        var free = npcs.Where(n => !offers.ContainsKey(n) && (active == null || (n != active.Giver && n != active.Recipient))).ToList();
        if (free.Count == 0) return;
        var npc = free[Random.Range(0, free.Count)];

        var kinds = new[] { Kind.LostPet, Kind.LostPet, Kind.LostItem, Kind.Pages, Kind.Delivery };
        var offer = new Offer { Kind = kinds[Random.Range(0, kinds.Length)] };
        offer.Dog = Random.value < 0.4f;
        offer.PetName = offer.Dog ? QuestTemplates.DogNames[Random.Range(0, QuestTemplates.DogNames.Length)]
                                  : QuestTemplates.CatNames[Random.Range(0, QuestTemplates.CatNames.Length)];
        var coat = QuestTargets.PetCoats[Random.Range(0, QuestTargets.PetCoats.Length)];
        offer.Coat = coat.name;
        offer.CoatColor = coat.color;
        offer.Item = QuestTemplates.Items[Random.Range(0, QuestTemplates.Items.Length)];
        offers[npc] = offer;
        npc.SetIcon(Npc.Icon.QuestAvailable);
    }

    Npc pendingRecipient;

    Npc PickRecipient(Npc giver)
    {
        // Someone well away from the giver, so the delivery is a little journey.
        var candidates = npcs.Where(n => n != giver && !offers.ContainsKey(n)).OrderByDescending(n => (n.transform.position - giver.transform.position).sqrMagnitude).Take(5).ToList();
        pendingRecipient = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
        return pendingRecipient;
    }

    void Accept(Npc giver, Offer offer)
    {
        offers.Remove(giver);
        giver.SetIcon(Npc.Icon.None);

        var q = new Active { Offer = offer, Giver = giver };
        Vector3 spot = FindHidingSpot(giver.transform.position);
        q.HintAnchor = spot;

        switch (offer.Kind)
        {
            case Kind.LostPet:
                var pet = QuestTargets.MakePet(offer.Dog, offer.CoatColor, offer.PetName, spot);
                q.Targets.Add(pet);
                q.Trail = QuestTargets.MakePawTrail(spot, giver.transform.position, offer.Dog);
                break;
            case Kind.LostItem:
                q.Targets.Add(QuestTargets.MakeItem(offer.Item, spot));
                break;
            case Kind.Pages:
                for (int i = 0; i < PagesNeeded; i++)
                {
                    Vector3? p = null;
                    for (int tries = 0; tries < 12 && !p.HasValue; tries++)
                    {
                        Vector2 r = Random.insideUnitCircle * 18f;
                        p = SnapToNavMesh(spot + new Vector3(r.x, 0f, r.y));
                    }
                    q.Targets.Add(QuestTargets.MakePage(p ?? spot));
                }
                break;
            case Kind.Delivery:
                q.Recipient = pendingRecipient ?? PickRecipient(giver);
                break;
        }

        q.Title = QuestTemplates.Title(offer.Kind, offer.PetName, offer.Dog, offer.Coat, offer.Item, q.Recipient?.DisplayName);
        active = q;

        // Three other people have seen something.
        var hinters = npcs.Where(n => n != giver && n != q.Recipient && !offers.ContainsKey(n)).OrderBy(_ => Random.value).Take(3);
        foreach (var h in hinters) { q.Hinters.Add(h); h.SetIcon(Npc.Icon.Hint); }

        hud.Toast($"New quest: {q.Title}");
    }

    void PickUp(QuestTarget target)
    {
        var q = active;
        target.PickUp(player.transform);
        q.Found++;
        if (q.Found >= q.Needed)
        {
            q.ReadyToTurnIn = true;
            q.Giver.SetIcon(Npc.Icon.TurnIn);
            if (q.Trail != null) Destroy(q.Trail);
            if (q.Beacon != null) Destroy(q.Beacon);
            if (q.SearchArea != null) { MapMarkers.Remove(q.SearchArea); q.SearchArea = null; }
            foreach (var h in q.Hinters) h.SetIcon(Npc.Icon.None);
            string what = q.Offer.Kind == Kind.LostPet ? $"You found {q.Offer.PetName}!"
                : q.Offer.Kind == Kind.Pages ? "You found all the pages!" : $"You found the {q.Offer.Item}!";
            hud.Toast($"{what} Take {(q.Offer.Kind == Kind.Pages ? "them" : "it")} back to {q.Giver.DisplayName}.");
        }
        else
        {
            hud.Toast($"Page {q.Found} of {q.Needed}");
        }
    }

    void Deliver()
    {
        var q = active;
        string recipient = q.Recipient.DisplayName;
        dialogue.Show(recipient, $"A parcel from {q.Giver.DisplayName}? I've been waiting for this, thank you!",
            ("You're welcome!", null));
        Reward($"Parcel delivered to {recipient}!");
    }

    void Complete()
    {
        var q = active;
        string thanks = q.Offer.Kind == Kind.LostPet ? $"{q.Offer.PetName}! You found {(q.Offer.Dog ? "him" : "her")}! Thank you so much!"
            : q.Offer.Kind == Kind.Pages ? "All five pages! You're a lifesaver, now I can study."
            : $"My {q.Offer.Item}! Thank you, I'd have been lost without {(q.Offer.Item == "car keys" ? "them" : "it")}.";
        int coins = QuestTemplates.Reward(q.Offer.Kind);
        dialogue.Show(q.Giver.DisplayName, $"{thanks} Here, take this.  (+{coins} coins, +1 star)", ("Happy to help!", null));
        if (q.Offer.Kind == Kind.LostPet && q.Targets.Count > 0 && q.Targets[0] != null) q.Targets[0].Deliver(q.Giver.transform);
        Reward(q.Offer.Kind == Kind.LostPet ? $"{q.Offer.PetName} is home!" : "Quest complete!");
    }

    void Reward(string headline)
    {
        var q = active;
        int coins = QuestTemplates.Reward(q.Offer.Kind);
        string before = PlayerProgress.Title;
        PlayerProgress.AddQuestReward(coins);
        string promoted = PlayerProgress.Title != before ? $"   You're now a {PlayerProgress.Title}!" : "";
        hud.Toast($"{headline}  +{coins} coins  +1 star{promoted}", 5f);
        Cleanup(q, keepDeliveredPet: true);
        active = null;
        nextOfferTime = Time.time + 6f;
    }

    void GiveUp()
    {
        var q = active;
        Cleanup(q, keepDeliveredPet: false);
        active = null;
        hud.Toast("Quest abandoned");
        nextOfferTime = Time.time + 4f;
    }

    void Cleanup(Active q, bool keepDeliveredPet)
    {
        foreach (var t in q.Targets)
        {
            if (t == null) continue;
            if (keepDeliveredPet && t.TargetKind == QuestTarget.Kind.Pet && t.PickedUp) continue;   // sits with its owner, then leaves
            Destroy(t.gameObject);
        }
        if (q.Trail != null) Destroy(q.Trail);
        if (q.Beacon != null) Destroy(q.Beacon);
        if (q.SearchArea != null) MapMarkers.Remove(q.SearchArea);
        foreach (var h in q.Hinters) h.SetIcon(Npc.Icon.None);
        q.Giver.SetIcon(Npc.Icon.None);
        if (q.Recipient != null) q.Recipient.SetIcon(Npc.Icon.None);
    }

    void UpdateDeliveryIcon()
    {
        if (active?.Offer.Kind != Kind.Delivery || active.Recipient == null) return;
        bool near = (active.Recipient.transform.position - player.transform.position).sqrMagnitude < 30f * 30f;
        active.Recipient.SetIcon(near ? Npc.Icon.TurnIn : active.Hinters.Contains(active.Recipient) ? Npc.Icon.Hint : Npc.Icon.None);
    }

    void UpdateTracker()
    {
        if (active == null) { hud.SetTracker(null, null); return; }
        var q = active;
        string objective;
        if (q.Offer.Kind == Kind.Delivery) objective = $"Carrying a parcel for {q.Recipient?.DisplayName}.";
        else if (q.ReadyToTurnIn) objective = $"Go back to {q.Giver.DisplayName} (green !).";
        else if (q.Offer.Kind == Kind.Pages) objective = $"Pages found: {q.Found}/{PagesNeeded}.";
        else objective = q.Offer.Kind == Kind.LostPet ? "Look for paw prints and listen for a sound." : "Look for a sparkle.";

        string body = objective;
        if (!q.ReadyToTurnIn)
        {
            if (q.SearchArea != null && PlayerPosition.HasValue)
            {
                Vector3 me = PlayerPosition.Value;
                Vector3 centre = q.SearchArea.Follow != null ? q.SearchArea.Follow.position : q.SearchArea.Position;
                float d = Vector3.Distance(new Vector3(me.x, 0f, me.z), new Vector3(centre.x, 0f, centre.z));
                body += d <= q.SearchArea.Radius
                    ? "\n<color=#5CD9FF><b>You're in the search area. Look around!</b></color>"
                    : $"\n<color=#5CD9FF><b>Search area: {CityArea.DescribeDirection(me, centre, "you")}</b></color>";
            }
            if (q.Hinters.Count > 0)
                body += q.Hints.Count == 0 ? "\nAsk people with a blue ? for hints (also on your map)." : $"\n{q.Hinters.Count} more {(q.Hinters.Count == 1 ? "person has" : "people have")} a hint (blue ?).";
            if (q.Hints.Count > 0) body += "\n• " + q.Hints[q.Hints.Count - 1];
        }
        hud.SetTracker(q.Title, body);
    }

    // ------------------------------------------------------------------ placement

    Vector3 FindHidingSpot(Vector3 from)
    {
        Vector3? fallback = null;
        for (int i = 0; i < 120; i++)
        {
            Vector3? p = CityNav.RandomPoint(1);
            if (!p.HasValue) continue;
            float d = Vector3.Distance(p.Value, from);
            if (d < 40f || d > 120f) continue;
            fallback ??= p;
            if (NearBuilding(p.Value, 7f)) return p.Value;   // tucked in beside a building
        }
        return fallback ?? SnapToNavMesh(from + Vector3.forward * 50f) ?? from;
    }

    static bool NearBuilding(Vector3 p, float within)
    {
        foreach (var b in CityColorizer.Buildings)
            if (b.Bounds.SqrDistance(new Vector3(p.x, b.Bounds.center.y, p.z)) < within * within) return true;
        return false;
    }

    static Vector3? SnapToNavMesh(Vector3 p) => CityNav.Snap(p);
}
