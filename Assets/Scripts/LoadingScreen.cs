using System;
using System.Collections.Generic;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen loading screen for CityMap. It appears before the scene's first frame and stays until every
/// runtime system has finished building: world dressing, the race track, the farm (including its ground
/// painting), the cars, City Life, the jobs, the art gallery and the map. The player is frozen until then, so
/// nobody can walk around a half-built world. Animated gradient, progress bar with shimmer, checklist and
/// rotating tips. A safety timeout lets the game continue if a system never reports in.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    /// <summary>True while the loading screen is up (other systems can hold input until it's gone).</summary>
    public static bool IsLoading { get; private set; }

    const float MinSeconds = 1.5f, SettleSeconds = 0.6f, FadeSeconds = 0.7f, TimeoutSeconds = 30f;

    static readonly string[] Tips =
    {
        "Walk up to a race car and press <b>X</b> to start a Knowledge Time Trial.",
        "Answer lap questions correctly for a speed boost!",
        "Press <b>M</b> to open the map, and click a marker to fast travel.",
        "Look for people with a yellow <b>!</b>: they have quests and jobs for you.",
        "Press <b>B</b> anywhere to visit the clothing store.",
        "Import a lecture at the top of the screen to get questions about your own class.",
        "Deliver newspapers by bike in the suburbs, or help out on the farm.",
        "Paste an image at an art gallery pedestal to build a 3D exhibit.",
    };

    sealed class Step
    {
        public string Label;
        public Func<bool> Ready;
        public bool Done;
        public Image Dot;
        public TextMeshProUGUI Text;
    }

    readonly List<Step> steps = new List<Step>();
    CanvasGroup group;
    RectTransform barFill, shimmer, spinner;
    TextMeshProUGUI status, percent, tip;
    readonly List<(RectTransform rt, Vector2 home, float speed, float phase)> blobs = new List<(RectTransform, Vector2, float, float)>();
    float shownAt, allReadyAt = -1f, fadeStart = -1f, shownProgress, nextTipAt;
    int tipIndex;
    PlayerController frozenPlayer;
    bool isCityMap, sceneChecked;

    // ------------------------------------------------------------------ install

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        IsLoading = true;
        var canvas = UiKit.CreateCanvas("LoadingScreen", 1000);
        DontDestroyOnLoad(canvas.gameObject);
        canvas.gameObject.AddComponent<LoadingScreen>().Build(canvas.transform);
    }

    void Build(Transform canvas)
    {
        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        shownAt = Time.realtimeSinceStartup;

        // Background: deep navy → teal gradient, with slow drifting glows.
        var bg = UiKit.Image("Background", canvas, Color.white, GradientSprite(new Color(0.04f, 0.06f, 0.15f), new Color(0.04f, 0.28f, 0.34f)), raycast: true);
        UiKit.Stretch(bg.rectTransform);
        Color[] glow = { new Color(0.2f, 0.6f, 1f, 0.10f), new Color(1f, 0.75f, 0.2f, 0.07f), new Color(0.4f, 1f, 0.6f, 0.07f), new Color(0.9f, 0.3f, 0.8f, 0.06f) };
        Vector2[] homes = { new Vector2(-560f, 260f), new Vector2(620f, -240f), new Vector2(420f, 330f), new Vector2(-480f, -320f) };
        for (int i = 0; i < glow.Length; i++)
        {
            var b = UiKit.Image("Glow", canvas, glow[i], UiKit.CircleSprite);
            var rt = UiKit.Place(b.rectTransform, new Vector2(0.5f, 0.5f), homes[i], Vector2.one * (520f + i * 90f));
            blobs.Add((rt, homes[i], 0.12f + i * 0.04f, i * 1.7f));
        }

        // Title.
        var title = UiKit.Text("Title", canvas, "LUDIFY", 150, TextAlignmentOptions.Center, Color.white);
        UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 180f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 18f;
        title.enableVertexGradient = true;
        title.colorGradient = new VertexGradient(Color.white, Color.white, new Color(0.55f, 0.85f, 1f), new Color(1f, 0.85f, 0.45f));
        var subtitle = UiKit.Text("Subtitle", canvas, "Learn it. Play it. Own it.", 30, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.65f));
        UiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1000f, 50f));
        subtitle.fontStyle = FontStyles.Italic;

        // Spinner ring.
        var ring = UiKit.Image("Spinner", canvas, new Color(1f, 0.82f, 0.3f, 0.9f), UiKit.RingSprite);
        spinner = UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(64f, 64f));
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillAmount = 0.75f;

        // Progress bar with a moving shimmer.
        var barBg = UiKit.Image("Bar", canvas, new Color(1f, 1f, 1f, 0.12f), UiKit.RoundedSprite);
        barBg.type = Image.Type.Sliced;
        var barRt = UiKit.Place(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(760f, 18f));
        var mask = barBg.gameObject.AddComponent<RectMask2D>();
        var fill = UiKit.Image("Fill", barRt, Color.white, GradientSprite(new Color(0.25f, 0.65f, 1f), new Color(0.4f, 1f, 0.7f), horizontal: true));
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0f, 1f); barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = Vector2.zero; barFill.offsetMax = Vector2.zero;
        var sh = UiKit.Image("Shimmer", barFill, new Color(1f, 1f, 1f, 0.35f));
        shimmer = sh.rectTransform;
        shimmer.anchorMin = new Vector2(0f, 0f); shimmer.anchorMax = new Vector2(0f, 1f); shimmer.pivot = new Vector2(0.5f, 0.5f);
        shimmer.sizeDelta = new Vector2(60f, 0f);
        _ = mask;

        status = UiKit.Text("Status", canvas, "Getting ready...", 26, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.85f));
        UiKit.Place(status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-80f, -78f), new Vector2(600f, 40f));
        percent = UiKit.Text("Percent", canvas, "0%", 26, TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.4f));
        UiKit.Place(percent.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(330f, -78f), new Vector2(100f, 40f));
        percent.fontStyle = FontStyles.Bold;

        // Checklist (two columns).
        AddStep("Shaping the world", () => Exists("_WorldDressing"));
        AddStep("Laying the race track", () => Exists("_TrackExtension"));
        AddStep("Planting the farm", () => FarmGround.Painted || (Exists("_TrackExtension") && FindAnyObjectByType<FarmGround>() == null));
        AddStep("Parking the race cars", () => Exists("_VehicleInteraction"));
        AddStep("Waking up the city", () => FindAnyObjectByType<QuestManager>() != null);
        AddStep("Hiring farm & suburb workers", () => FindAnyObjectByType<JobManager>() != null);
        AddStep("Opening the art gallery", () => Exists("_Gallery"));
        AddStep("Drawing the map", () => { var m = FindAnyObjectByType<Ludify.Map.MapSystem>(); return m != null && m.Points != null; });
        for (int i = 0; i < steps.Count; i++)
        {
            float x = i % 2 == 0 ? -190f : 190f, y = -140f - (i / 2) * 40f;
            var row = UiKit.Rect("Step", canvas);
            UiKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(340f, 34f));
            steps[i].Dot = UiKit.Image("Dot", row, new Color(1f, 1f, 1f, 0.25f), UiKit.CircleSprite);
            UiKit.Place(steps[i].Dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(16f, 16f));
            steps[i].Text = UiKit.Text("Label", row, steps[i].Label, 22, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.55f));
            UiKit.Place(steps[i].Text.rectTransform, new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(310f, 34f));
        }

        // Tips.
        tip = UiKit.Text("Tip", canvas, "", 24, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.75f));
        UiKit.Place(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1400f, 60f));
        tipIndex = UnityEngine.Random.Range(0, Tips.Length);
        ShowTip();
    }

    void AddStep(string label, Func<bool> ready) => steps.Add(new Step { Label = label, Ready = ready });

    static bool Exists(string name) => GameObject.Find(name) != null;

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        Animate(now);

        if (fadeStart >= 0f)
        {
            group.alpha = 1f - Mathf.Clamp01((now - fadeStart) / FadeSeconds);
            if (group.alpha <= 0f) Destroy(gameObject);
            return;
        }

        // Only CityMap has all these systems; any other scene just gets a brief splash.
        if (!sceneChecked && SceneManager.GetActiveScene().isLoaded)
        {
            sceneChecked = true;
            isCityMap = GameObject.Find("F1Circuit") != null;
            if (!isCityMap) foreach (var s in steps) s.Done = true;
        }
        FreezePlayer();

        int done = 0;
        foreach (var s in steps)
        {
            if (!s.Done && isCityMap)
            {
                bool ready;
                try { ready = s.Ready(); } catch { ready = false; }
                if (ready) MarkDone(s);
            }
            if (s.Done) done++;
        }

        float target = steps.Count == 0 ? 1f : done / (float)steps.Count;
        shownProgress = Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * 1.5f);
        barFill.anchorMax = new Vector2(Mathf.Max(0.02f, shownProgress), 1f);
        percent.text = $"{Mathf.RoundToInt(shownProgress * 100f)}%";
        var pending = steps.Find(s => !s.Done);
        status.text = pending != null ? pending.Label + "..." : "Ready!";

        bool allDone = done == steps.Count && shownProgress >= 0.999f;
        bool timedOut = now - shownAt > TimeoutSeconds;
        if ((allDone || timedOut) && allReadyAt < 0f)
        {
            allReadyAt = now;
            if (timedOut && !allDone) Debug.LogWarning($"[LoadingScreen] Timed out waiting for: {pending?.Label}. Continuing anyway.");
        }
        // Let things settle for a moment (first renders, shader warm-up) and show the screen for a minimum time.
        if (allReadyAt >= 0f && now - allReadyAt >= SettleSeconds && now - shownAt >= MinSeconds) Finish();
    }

    void MarkDone(Step s)
    {
        s.Done = true;
        s.Dot.color = new Color(0.4f, 1f, 0.6f);
        s.Text.color = new Color(1f, 1f, 1f, 0.9f);
        s.Text.text = s.Label;
    }

    void FreezePlayer()
    {
        if (frozenPlayer != null) { frozenPlayer.enabled = false; return; }
        frozenPlayer = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (frozenPlayer != null) frozenPlayer.enabled = false;
    }

    void Finish()
    {
        fadeStart = Time.realtimeSinceStartup;
        IsLoading = false;
        group.blocksRaycasts = false;
        if (frozenPlayer != null) frozenPlayer.enabled = true;   // spawn: the player can move now
        status.text = "Ready!";
    }

    void Animate(float now)
    {
        spinner.localRotation = Quaternion.Euler(0f, 0f, -now * 220f);
        float barWidth = ((RectTransform)barFill.parent).rect.width;
        float x = Mathf.Repeat(now * 520f, barWidth + 200f) - 100f;
        shimmer.anchoredPosition = new Vector2(x, 0f);
        foreach (var (rt, home, speed, phase) in blobs)
            rt.anchoredPosition = home + new Vector2(Mathf.Sin(now * speed + phase) * 90f, Mathf.Cos(now * speed * 0.8f + phase) * 60f);
        foreach (var s in steps)
            if (!s.Done) s.Dot.color = new Color(1f, 1f, 1f, 0.2f + 0.2f * Mathf.Sin(now * 4f + s.Label.Length));
        if (now >= nextTipAt) { tipIndex = (tipIndex + 1) % Tips.Length; ShowTip(); }
    }

    void ShowTip()
    {
        tip.text = "<color=#FFD24D><b>TIP</b></color>   " + Tips[tipIndex];
        nextTipAt = Time.realtimeSinceStartup + 4f;
    }

    // ------------------------------------------------------------------ sprites

    static Sprite GradientSprite(Color a, Color b, bool horizontal = false)
    {
        const int n = 256;
        var tex = new Texture2D(horizontal ? n : 2, horizontal ? 2 : n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        for (int i = 0; i < n; i++)
        {
            var c = Color.Lerp(a, b, i / (n - 1f));
            if (horizontal) { tex.SetPixel(i, 0, c); tex.SetPixel(i, 1, c); }
            else { tex.SetPixel(0, n - 1 - i, c); tex.SetPixel(1, n - 1 - i, c); }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }
}
