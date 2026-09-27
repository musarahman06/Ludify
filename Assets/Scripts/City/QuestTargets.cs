using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Something a quest asks you to find: a lost pet, a dropped item, a page. Handles its own idle animation,
/// the "Meow!"/glint cue when you're near, and following you once picked up (pets).</summary>
public class QuestTarget : MonoBehaviour
{
    public enum Kind { Pet, Item, Page }

    public Kind TargetKind;
    public string Label;          // "Whiskers", "car keys", "page"
    public string Sound;          // "Meow!", "Woof!" (pets)
    public bool PickedUp { get; private set; }

    Transform tail, player, body;
    TextMeshPro bubble;
    Transform glint;
    float bubbleUntil, nextBubble, walkPhase;
    Vector3 basePos;
    NavMeshAgent agent;
    AudioSource voice;
    bool wasHidden;
    readonly List<Transform> legs = new List<Transform>();

    public void Init(Transform tailTransform, Transform bodyTransform)
    {
        tail = tailTransform;
        body = bodyTransform;
        basePos = transform.position;
        if (TargetKind == Kind.Pet)
        {
            if (body != null)
                foreach (Transform child in body)
                    if (child.name == "Leg") legs.Add(child);

            // 3D voice: you can hear it from ~35 m and home in on it.
            voice = gameObject.AddComponent<AudioSource>();
            voice.clip = Sound == "Woof!" ? QuestTargets.WoofClip : QuestTargets.MeowClip;
            voice.playOnAwake = false;
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 4f;
            voice.maxDistance = HearingRange;
            voice.dopplerLevel = 0f;
            voice.volume = 0.9f;

            var go = new GameObject("Bubble");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            bubble = go.AddComponent<TextMeshPro>();
            bubble.alignment = TextAlignmentOptions.Center;
            bubble.fontSize = 5f;
            bubble.fontStyle = FontStyles.Bold;
            bubble.outlineWidth = 0.25f;
            bubble.outlineColor = new Color32(20, 20, 30, 255);
            bubble.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            bubble.text = "";
        }
        else
        {
            glint = QuestTargets.MakeGlint(transform);
        }
    }

    public void PickUp(Transform follow)
    {
        PickedUp = true;
        player = follow;
        if (TargetKind != Kind.Pet) { gameObject.SetActive(false); return; }   // items go in your pocket
        if (bubble != null) bubble.text = "";

        // Pets walk after you on the city NavMesh, so they stay on the ground and go around buildings.
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.radius = 0.2f;
        agent.height = 0.6f;
        agent.acceleration = 30f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0.6f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        PlaceNear(transform.position);
    }

    /// <summary>Hand the pet back to its owner: sit next to them, then leave after a while.</summary>
    public void Deliver(Transform owner)
    {
        player = null;
        PlaceNear(owner.position + owner.right * 1.2f);
        if (agent != null && agent.enabled) agent.isStopped = true;
        foreach (var leg in legs) leg.localRotation = Quaternion.identity;
        basePos = transform.position;
        Speak(true);
        bubbleUntil = Time.time + 3f;
        Destroy(gameObject, 12f);
    }

    const float CatchUpDistance = 18f;
    const float HearingRange = 35f, BubbleRange = 15f;

    /// <summary>Meow/woof out loud (and show it in a bubble if <paramref name="showBubble"/>).</summary>
    void Speak(bool showBubble)
    {
        if (voice != null)
        {
            voice.pitch = Random.Range(0.9f, 1.15f);
            voice.Play();
        }
        if (showBubble && bubble != null)
        {
            bubble.text = Sound;
            bubbleUntil = Time.time + 1.4f;
        }
    }

    /// <summary>Trot along behind the player. Hides while they're in a car and pops back out beside them.</summary>
    void FollowPlayer()
    {
        bool withPlayer = player.gameObject.activeInHierarchy;
        if (body) body.gameObject.SetActive(withPlayer);
        if (bubble) bubble.gameObject.SetActive(withPlayer);
        if (!withPlayer) { wasHidden = true; return; }
        if (Time.time > nextBubble) { Speak(true); nextBubble = Time.time + Random.Range(8f, 15f); }   // happy to be found

        Vector3 heel = player.position - player.forward * 1.3f + player.right * 0.5f;
        float dist = Flat(transform.position - player.position).magnitude;
        if (wasHidden || dist > CatchUpDistance) { PlaceNear(heel); wasHidden = false; }   // left the car, fast travel, etc.

        float speed;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = Mathf.Lerp(3f, 17f, Mathf.InverseLerp(1.5f, 8f, dist));   // run to keep up with a sprint
            if (dist > 1.6f) agent.SetDestination(heel);
            else agent.ResetPath();
            speed = agent.velocity.magnitude;
            if (speed < 0.2f) Face(player.position, 6f);
        }
        else
        {
            // Off the city NavMesh (e.g. across the river): walk straight over the ground.
            if (NavMesh.SamplePosition(transform.position, out _, 0.5f, NavMesh.AllAreas)) { PlaceNear(transform.position); return; }
            Vector3 before = transform.position;
            if (dist > 1.6f)
            {
                Vector3 next = Vector3.MoveTowards(before, heel, Mathf.Lerp(3f, 17f, Mathf.InverseLerp(1.5f, 8f, dist)) * Time.deltaTime);
                transform.position = QuestTargets.Ground(next);
                Face(transform.position + Flat(transform.position - before), 10f);
            }
            else Face(player.position, 6f);
            speed = Flat(transform.position - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        // Little trot: legs swing and the body bobs with speed.
        float amount = Mathf.Clamp01(speed / 3f);
        walkPhase += Time.deltaTime * Mathf.Lerp(6f, 16f, Mathf.Clamp01(speed / 10f));
        for (int i = 0; i < legs.Count; i++)
            legs[i].localRotation = Quaternion.Euler(Mathf.Sin(walkPhase + (i % 2 == (i / 2) % 2 ? 0f : Mathf.PI)) * 35f * amount, 0f, 0f);
        if (body) body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f * amount);
    }

    /// <summary>Put the pet on the ground at (or near) a point: on the NavMesh if there is one there.</summary>
    void PlaceNear(Vector3 p)
    {
        if (agent != null && NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
        }
        else
        {
            if (agent != null) agent.enabled = false;
            transform.position = QuestTargets.Ground(p);
        }
    }

    void Face(Vector3 point, float rate)
    {
        Vector3 look = Flat(point - transform.position);
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), rate * Time.deltaTime);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    void Update()
    {
        var cam = Camera.main;
        float t = Time.time;

        if (TargetKind == Kind.Pet)
        {
            if (tail) tail.localRotation = Quaternion.Euler(-35f, Mathf.Sin(t * (PickedUp ? 12f : 5f)) * 35f, 0f);
            if (player != null) FollowPlayer();
            else if (!PickedUp)
            {
                // Look around now and then.
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + Mathf.Sin(t * 0.7f) * 0.25f, 0f);
                var p = QuestManager.PlayerPosition;
                float d2 = p.HasValue ? (p.Value - transform.position).sqrMagnitude : float.MaxValue;
                if (d2 < HearingRange * HearingRange && t > nextBubble)
                {
                    Speak(d2 < BubbleRange * BubbleRange);
                    nextBubble = t + Random.Range(2.5f, 4f);
                }
            }
            if (bubble != null)
            {
                if (t > bubbleUntil) bubble.text = "";
                if (cam != null) bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - cam.transform.position);
            }
        }
        else
        {
            if (TargetKind == Kind.Page)
            {
                // Pages flutter on the ground.
                transform.position = basePos + Vector3.up * (0.05f + Mathf.Abs(Mathf.Sin(t * 2f + basePos.x)) * 0.12f);
                transform.rotation = Quaternion.Euler(Mathf.Sin(t * 3f + basePos.z) * 12f, transform.eulerAngles.y + 0.3f, 0f);
            }
            if (glint != null)
            {
                var p = QuestManager.PlayerPosition;
                bool near = p.HasValue && (p.Value - transform.position).sqrMagnitude < 20f * 20f;
                glint.gameObject.SetActive(near);
                if (near && cam != null)
                {
                    glint.rotation = Quaternion.LookRotation(glint.position - cam.transform.position) * Quaternion.Euler(0, 0, t * 90f);
                    float s = 0.6f + Mathf.Abs(Mathf.Sin(t * 4f)) * 0.5f;
                    glint.localScale = new Vector3(s, s, s);
                }
            }
        }
    }
}

/// <summary>Builds the blocky pets, items, pages and paw-print trails used by quests.</summary>
public static class QuestTargets
{
    public static readonly (string name, Color color)[] PetCoats =
    {
        ("orange", new Color(0.95f, 0.55f, 0.2f)), ("grey", new Color(0.55f, 0.57f, 0.6f)),
        ("black", new Color(0.12f, 0.12f, 0.14f)), ("white", new Color(0.95f, 0.95f, 0.93f)),
        ("brown", new Color(0.5f, 0.33f, 0.2f)), ("golden", new Color(0.9f, 0.72f, 0.35f)),
    };

    static Material template;
    static Sprite pawSprite, glintSprite;
    static AudioClip meow, woof;
    const int SampleRate = 44100;

    /// <summary>A cat's "mee-ow": pitch rises then falls, brightest in the middle (synthesised, no audio files).</summary>
    public static AudioClip MeowClip => meow != null ? meow : meow = Synth("Meow", 0.75f, (t, u) =>
    {
        float f0 = u < 0.35f ? Mathf.Lerp(560f, 820f, u / 0.35f) : Mathf.Lerp(820f, 430f, (u - 0.35f) / 0.65f);
        f0 *= 1f + 0.015f * Mathf.Sin(t * 2f * Mathf.PI * 6f);             // a little vibrato
        float env = Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((1f - u) / 0.3f);
        return new Voice { Pitch = f0, Amp = env, Harmonics = 7, Falloff = 2.2f - 1.4f * Mathf.Sin(u * Mathf.PI) };   // "ee" -> "ow"
    });

    /// <summary>"Woof woof": two short, low, rough barks.</summary>
    public static AudioClip WoofClip => woof != null ? woof : woof = Synth("Woof", 0.8f, (t, u) =>
    {
        const float barkLength = 0.26f;
        float bt = t < 0.4f ? t : t - 0.4f;
        if (bt > barkLength) return default;
        float bu = bt / barkLength;
        return new Voice
        {
            Pitch = Mathf.Lerp(330f, 170f, Mathf.Sqrt(bu)),
            Amp = Mathf.Clamp01(bt / 0.012f) * Mathf.Exp(-bu * 3.2f),
            Harmonics = 10,
            Falloff = 1.1f,
            Noise = 0.4f * Mathf.Exp(-bt / 0.03f),   // breathy attack
        };
    });

    struct Voice { public float Pitch, Amp, Falloff, Noise; public int Harmonics; }

    static AudioClip Synth(string name, float seconds, System.Func<float, float, Voice> voiceAt)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        float phase = 0f, smooth = 0f, peak = 0.0001f;
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            Voice v = voiceAt(t, t / seconds);
            phase = (phase + v.Pitch / SampleRate) % 1f;   // integrate so pitch glides smoothly
            float x = 0f;
            for (int k = 1; k <= v.Harmonics; k++) x += Mathf.Sin(2f * Mathf.PI * k * phase) / Mathf.Pow(k, v.Falloff);
            x = x * v.Amp + v.Noise * v.Amp * (float)(rng.NextDouble() * 2.0 - 1.0);
            smooth += (x - smooth) * 0.35f;                // soften the top end
            data[i] = smooth;
            peak = Mathf.Max(peak, Mathf.Abs(smooth));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void SetTemplate(Material m) => template = m;
    static Material M(Color c) => NpcFactory.MaterialFor(template, c);

    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = M(color);
        return go.transform;
    }

    public static QuestTarget MakePet(bool dog, Color coat, string name, Vector3 position)
    {
        var root = new GameObject(dog ? "LostDog" : "LostCat");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);

        float k = dog ? 1.35f : 1f;
        Color dark = coat * 0.75f; dark.a = 1f;
        Box(body, "Torso", new Vector3(0, 0.32f * k, 0), new Vector3(0.32f, 0.26f, 0.6f) * k, coat);
        Box(body, "Head", new Vector3(0, 0.55f * k, 0.36f * k), new Vector3(0.3f, 0.26f, 0.26f) * k, coat);
        if (dog)
        {
            Box(body, "Snout", new Vector3(0, 0.5f * k, 0.55f * k), new Vector3(0.16f, 0.12f, 0.16f) * k, dark);
            Box(body, "EarL", new Vector3(-0.15f * k, 0.52f * k, 0.36f * k), new Vector3(0.06f, 0.2f, 0.12f) * k, dark);
            Box(body, "EarR", new Vector3(0.15f * k, 0.52f * k, 0.36f * k), new Vector3(0.06f, 0.2f, 0.12f) * k, dark);
        }
        else
        {
            var earL = Box(body, "EarL", new Vector3(-0.09f, 0.72f, 0.36f), new Vector3(0.08f, 0.1f, 0.06f), dark);
            var earR = Box(body, "EarR", new Vector3(0.09f, 0.72f, 0.36f), new Vector3(0.08f, 0.1f, 0.06f), dark);
            earL.localRotation = earR.localRotation = Quaternion.Euler(0, 0, 45);
        }
        Box(body, "EyeL", new Vector3(-0.07f * k, 0.58f * k, 0.49f * k), new Vector3(0.05f, 0.05f, 0.02f) * k, Color.black);
        Box(body, "EyeR", new Vector3(0.07f * k, 0.58f * k, 0.49f * k), new Vector3(0.05f, 0.05f, 0.02f) * k, Color.black);
        foreach (var (x, z) in new[] { (-0.1f, 0.2f), (0.1f, 0.2f), (-0.1f, -0.2f), (0.1f, -0.2f) })
            Box(body, "Leg", new Vector3(x * k, 0.1f * k, z * k), new Vector3(0.08f, 0.2f, 0.08f) * k, dark);
        var tailPivot = new GameObject("TailPivot").transform;
        tailPivot.SetParent(body, false);
        tailPivot.localPosition = new Vector3(0, 0.4f * k, -0.3f * k);
        Box(tailPivot, "Tail", new Vector3(0, 0, -0.18f * k), new Vector3(0.06f, 0.06f, 0.36f) * k, coat);

        var target = root.AddComponent<QuestTarget>();
        target.TargetKind = QuestTarget.Kind.Pet;
        target.Label = name;
        target.Sound = dog ? "Woof!" : "Meow!";
        target.Init(tailPivot, body);
        return target;
    }

    public static QuestTarget MakeItem(string item, Vector3 position)
    {
        var root = new GameObject("LostItem_" + item);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var t = root.transform;
        switch (item)
        {
            case "car keys":
                Box(t, "Ring", new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.02f, 0.14f), new Color(0.85f, 0.75f, 0.3f), PrimitiveType.Cylinder);
                Box(t, "Key1", new Vector3(0.1f, 0.03f, 0.02f), new Vector3(0.16f, 0.02f, 0.05f), new Color(0.8f, 0.8f, 0.82f));
                Box(t, "Key2", new Vector3(0.08f, 0.03f, -0.06f), new Vector3(0.14f, 0.02f, 0.05f), new Color(0.85f, 0.75f, 0.3f));
                break;
            case "phone":
                Box(t, "Phone", new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.02f, 0.2f), new Color(0.12f, 0.12f, 0.15f));
                Box(t, "Screen", new Vector3(0, 0.032f, 0), new Vector3(0.085f, 0.005f, 0.17f), new Color(0.3f, 0.6f, 0.95f));
                break;
            case "backpack":
                Box(t, "Bag", new Vector3(0, 0.25f, 0), new Vector3(0.4f, 0.5f, 0.25f), new Color(0.2f, 0.45f, 0.8f));
                Box(t, "Pocket", new Vector3(0, 0.18f, 0.14f), new Vector3(0.3f, 0.2f, 0.06f), new Color(0.15f, 0.35f, 0.65f));
                break;
            default: // teddy bear
                Color fur = new Color(0.62f, 0.42f, 0.25f);
                Box(t, "Body", new Vector3(0, 0.18f, 0), new Vector3(0.24f, 0.26f, 0.18f), fur);
                Box(t, "Head", new Vector3(0, 0.4f, 0), new Vector3(0.2f, 0.18f, 0.16f), fur);
                Box(t, "EarL", new Vector3(-0.08f, 0.5f, 0), new Vector3(0.06f, 0.06f, 0.04f), fur);
                Box(t, "EarR", new Vector3(0.08f, 0.5f, 0), new Vector3(0.06f, 0.06f, 0.04f), fur);
                break;
        }
        var target = root.AddComponent<QuestTarget>();
        target.TargetKind = QuestTarget.Kind.Item;
        target.Label = item;
        target.Init(null, null);
        return target;
    }

    public static QuestTarget MakePage(Vector3 position)
    {
        var root = new GameObject("LostPage");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Box(root.transform, "Paper", Vector3.zero, new Vector3(0.3f, 0.01f, 0.4f), new Color(0.97f, 0.96f, 0.9f));
        Box(root.transform, "Line1", new Vector3(0, 0.008f, 0.08f), new Vector3(0.22f, 0.002f, 0.02f), new Color(0.3f, 0.35f, 0.5f));
        Box(root.transform, "Line2", new Vector3(0, 0.008f, 0f), new Vector3(0.22f, 0.002f, 0.02f), new Color(0.3f, 0.35f, 0.5f));
        Box(root.transform, "Line3", new Vector3(-0.03f, 0.008f, -0.08f), new Vector3(0.16f, 0.002f, 0.02f), new Color(0.3f, 0.35f, 0.5f));
        var target = root.AddComponent<QuestTarget>();
        target.TargetKind = QuestTarget.Kind.Page;
        target.Label = "page";
        target.Init(null, null);
        return target;
    }

    /// <summary>
    /// Paw prints along the way the pet ran: from the owner's direction, following the walkable streets, up to the pet.
    /// The last ~70 m before the pet are printed, getting fresher (darker) the closer you are to it.
    /// </summary>
    public static GameObject MakePawTrail(Vector3 petPosition, Vector3 from, bool dog)
    {
        var trail = new GameObject("PawTrail");
        const float spacing = 1.3f, length = 70f;

        // Route the pet took (backwards from the pet), or a straight line if there's no path.
        var route = new List<Vector3> { petPosition };
        var path = new NavMeshPath();
        if (NavMesh.CalculatePath(from, petPosition, NavMesh.AllAreas, path) && path.corners.Length > 1)
        {
            route.Clear();
            for (int i = path.corners.Length - 1; i >= 0; i--) route.Add(path.corners[i]);
        }
        else
        {
            Vector3 away = (from - petPosition).sqrMagnitude > 1f ? (from - petPosition).normalized : Vector3.forward;
            route.Add(petPosition + Vector3.ProjectOnPlane(away, Vector3.up).normalized * length);
        }

        int count = 0;
        float next = spacing * 1.5f;   // distance along the route of the next print (small gap right at the pet)
        float travelled = 0f;
        for (int seg = 0; seg + 1 < route.Count && next < length; seg++)
        {
            Vector3 a = route[seg], b = route[seg + 1];
            float segLength = Vector3.Distance(a, b);
            if (segLength < 0.01f) continue;
            Vector3 dir = (b - a) / segLength;   // pointing away from the pet
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            for (; next <= travelled + segLength && next < length; next += spacing)
            {
                bool left = count++ % 2 == 0;
                Vector3 p = a + dir * (next - travelled) + side * (left ? 0.18f : -0.18f);
                var go = new GameObject("Paw");
                go.transform.SetParent(trail.transform, false);
                go.transform.position = Ground(p) + Vector3.up * 0.03f;
                // Lies flat, toes pointing toward the pet (the way it was walking).
                go.transform.rotation = Quaternion.LookRotation(Vector3.down, -dir);
                float s = dog ? 0.7f : 0.55f;
                go.transform.localScale = new Vector3(s, s, s);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PawSprite;
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.55f, next / length));
            }
            travelled += segLength;
        }
        return trail;
    }

    /// <summary>Top of whatever is under a point (roads, pavements), or the point itself if nothing is.</summary>
    public static Vector3 Ground(Vector3 p)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 2f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            // Skip the player and cars; we want the road or pavement.
            if (hit.collider is CharacterController || hit.collider.attachedRigidbody != null) continue;
            return hit.point;
        }
        return p;
    }

    /// <summary>
    /// Hint 2's landmark: a tall cyan light pillar standing on the street right against the named building (on the
    /// side facing the lost thing) and rising above the roofline, with a "?" at eye level and one at the top. You can
    /// see it from the street nearby and from across the city.
    /// </summary>
    public static GameObject MakeBeacon(Bounds building, Vector3 target)
    {
        // The building edge closest to the target, nudged out onto the pavement.
        Vector3 edge = building.ClosestPoint(new Vector3(target.x, building.center.y, target.z));
        Vector3 outward = new Vector3(target.x - edge.x, 0f, target.z - edge.z);
        Vector3 foot = new Vector3(edge.x, 0f, edge.z) + (outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.zero) * 0.8f;
        foot = Ground(foot);
        float height = Mathf.Max(30f, building.max.y + 10f - foot.y);

        var root = new GameObject("HintBeacon");
        root.transform.position = foot;
        var pillar = Box(root.transform, "Pillar", new Vector3(0f, height / 2f, 0f), new Vector3(0.9f, height / 2f, 0.9f),
                         new Color(0.35f, 0.85f, 1f), PrimitiveType.Cylinder);
        pillar.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        QuestionMark(root.transform, 5f, 22f);
        QuestionMark(root.transform, height + 3.5f, 60f);
        return root;
    }

    static void QuestionMark(Transform parent, float y, float size)
    {
        var go = new GameObject("QuestionMark");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        var text = go.AddComponent<TextMeshPro>();
        text.text = "?";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.35f, 0.85f, 1f);
        text.outlineWidth = 0.3f;
        text.outlineColor = new Color32(10, 30, 60, 255);
        text.rectTransform.sizeDelta = new Vector2(size / 7f, size / 7f);
        go.AddComponent<Beacon>();
    }

    /// <summary>Bobs and always faces the camera.</summary>
    sealed class Beacon : MonoBehaviour
    {
        Vector3 basePos;
        void Start() => basePos = transform.position;
        void LateUpdate()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.4f;
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    public static Transform MakeGlint(Transform parent)
    {
        var go = new GameObject("Glint");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GlintSprite;
        sr.color = new Color(1f, 0.95f, 0.6f, 0.95f);
        go.SetActive(false);
        return go.transform;
    }

    static Sprite PawSprite
    {
        get
        {
            if (pawSprite != null) return pawSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var blobs = new[] { (32f, 22f, 12f), (16f, 40f, 6f), (26f, 49f, 6f), (38f, 49f, 6f), (48f, 40f, 6f) };
            var pad = new Color(0.28f, 0.17f, 0.09f);
            var halo = new Color(1f, 0.96f, 0.85f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float edge = float.MaxValue;   // distance outside the nearest pad (negative = inside)
                foreach (var (bx, by, r) in blobs)
                    edge = Mathf.Min(edge, Vector2.Distance(new Vector2(x, y), new Vector2(bx, by)) - r);
                float padAlpha = Mathf.Clamp01(0.5f - edge);
                float haloAlpha = Mathf.Clamp01(3.5f - edge) * 0.6f;
                tex.SetPixel(x, y, padAlpha > 0f
                    ? Color.Lerp(new Color(halo.r, halo.g, halo.b, haloAlpha), new Color(pad.r, pad.g, pad.b, 1f), padAlpha)
                    : new Color(halo.r, halo.g, halo.b, haloAlpha));
            }
            tex.Apply();
            return pawSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }

    static Sprite GlintSprite
    {
        get
        {
            if (glintSprite != null) return glintSprite;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x - 31.5f) / 31.5f, dy = Mathf.Abs(y - 31.5f) / 31.5f;
                float star = Mathf.Clamp01(1f - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)) * 1.1f);   // four-point sparkle
                tex.SetPixel(x, y, new Color(1, 1, 1, star));
            }
            tex.Apply();
            return glintSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 1.2f);
        }
    }
}
