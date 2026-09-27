using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right coin display and helper stars/title, the quest tracker under it, the "Press E" prompt and
/// completion toasts.
/// </summary>
public class CityHud : MonoBehaviour
{
    TextMeshProUGUI coins, stars, trackerTitle, trackerBody, prompt, toast;
    RectTransform coinGroup, tracker;
    CanvasGroup toastGroup;
    float coinPop, toastUntil;
    int shownCoins;

    public static CityHud Create()
    {
        var canvas = UiKit.CreateCanvas("CityHudCanvas", 30);
        var hud = canvas.gameObject.AddComponent<CityHud>();
        hud.Build(canvas.transform);
        return hud;
    }

    void Build(Transform canvas)
    {
        // Coins + stars (top-right).
        var bg = UiKit.Image("Wallet", canvas, UiKit.PanelColor, UiKit.RoundedSprite);
        bg.type = Image.Type.Sliced;
        coinGroup = UiKit.Place(bg.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(340f, 104f));

        var coinIcon = UiKit.Image("Coin", coinGroup, new Color(1f, 0.8f, 0.2f), UiKit.CircleSprite);
        UiKit.Place(coinIcon.rectTransform, new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(40f, 40f));
        var coinInner = UiKit.Image("CoinInner", coinIcon.transform, new Color(0.85f, 0.6f, 0.1f), UiKit.RingSprite);
        UiKit.Stretch(coinInner.rectTransform, 5f);
        coins = UiKit.Text("Coins", coinGroup, "0", 36, TextAlignmentOptions.Left, Color.white);
        UiKit.Place(coins.rectTransform, new Vector2(0f, 1f), new Vector2(76f, -10f), new Vector2(150f, 48f));
        coins.fontStyle = FontStyles.Bold;

        var starIcon = UiKit.Image("Star", coinGroup, new Color(1f, 0.85f, 0.35f), StarSprite);
        UiKit.Place(starIcon.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -64f), new Vector2(28f, 28f));
        stars = UiKit.Text("Stars", coinGroup, "", 20, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.35f));
        UiKit.Place(stars.rectTransform, new Vector2(0f, 1f), new Vector2(76f, -62f), new Vector2(250f, 32f));
        var shopHint = UiKit.Text("ShopHint", coinGroup, "[B] Shop", 18, TextAlignmentOptions.Right, new Color(1f, 0.6f, 0.8f));
        UiKit.Place(shopHint.rectTransform, new Vector2(0f, 1f), new Vector2(226f, -20f), new Vector2(100f, 30f));
        stars.enableAutoSizing = true;   // "City Legend" is long
        stars.fontSizeMin = 14f;
        stars.fontSizeMax = 20f;

        // Quest tracker (under the wallet).
        var tbg = UiKit.Image("Tracker", canvas, UiKit.PanelColor, UiKit.RoundedSprite);
        tbg.type = Image.Type.Sliced;
        tracker = UiKit.Place(tbg.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -140f), new Vector2(420f, 220f));
        trackerTitle = UiKit.Text("Title", tracker, "", 24, TextAlignmentOptions.TopLeft, new Color(1f, 0.82f, 0.3f));
        UiKit.Place(trackerTitle.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(384f, 34f));
        trackerTitle.rectTransform.pivot = new Vector2(0f, 1f);
        trackerTitle.fontStyle = FontStyles.Bold;
        trackerBody = UiKit.Text("Body", tracker, "", 20, TextAlignmentOptions.TopLeft);
        UiKit.Place(trackerBody.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -48f), new Vector2(384f, 170f));
        trackerBody.rectTransform.pivot = new Vector2(0f, 1f);
        tracker.gameObject.SetActive(false);

        // "Press E" prompt (bottom centre, above the car hint).
        prompt = UiKit.Text("Prompt", canvas, "", 30, TextAlignmentOptions.Center);
        UiKit.Place(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(900f, 50f));
        prompt.fontStyle = FontStyles.Bold;
        prompt.outlineWidth = 0.2f;
        prompt.outlineColor = new Color32(0, 0, 0, 200);

        // Toast (upper centre).
        toast = UiKit.Text("Toast", canvas, "", 34, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
        UiKit.Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1200f, 60f));
        toast.fontStyle = FontStyles.Bold;
        toast.outlineWidth = 0.2f;
        toast.outlineColor = new Color32(0, 0, 0, 220);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();

        shownCoins = PlayerProgress.Coins;
        RefreshWallet();
        PlayerProgress.Changed += OnProgressChanged;
    }

    void OnDestroy() => PlayerProgress.Changed -= OnProgressChanged;

    void OnProgressChanged()
    {
        coinPop = 1f;
        RefreshWallet();
    }

    void RefreshWallet()
    {
        int next = PlayerProgress.NextTitleAt;
        string progress = next > 0 ? $"  ({PlayerProgress.Stars}/{next})" : "";
        stars.text = $"{PlayerProgress.Stars}   {PlayerProgress.Title}{progress}";
    }

    static Sprite star;

    /// <summary>Five-pointed star (the UI font has no ★ glyph).</summary>
    static Sprite StarSprite
    {
        get
        {
            if (star != null) return star;
            const int size = 64;
            var outline = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.48f : 0.2f) * size;
                outline[i] = new Vector2(size / 2f + Mathf.Cos(a) * r, size / 2f + Mathf.Sin(a) * r);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 4x supersampled point-in-polygon for soft edges.
                int hits = 0;
                for (int s = 0; s < 4; s++)
                    if (Inside(outline, new Vector2(x + 0.25f + 0.5f * (s & 1), y + 0.25f + 0.5f * (s >> 1)))) hits++;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return star = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
    }

    public void SetPrompt(string text) => prompt.text = text ?? "";

    public void SetTracker(string title, string body)
    {
        bool show = !string.IsNullOrEmpty(title);
        tracker.gameObject.SetActive(show);
        if (!show) return;
        trackerTitle.text = title;
        trackerBody.text = body;
        tracker.sizeDelta = new Vector2(420f, Mathf.Max(90f, 60f + trackerBody.GetPreferredValues(body, 384f, 0f).y));
    }

    public void Toast(string text, float seconds = 3.5f)
    {
        toast.text = text;
        toastUntil = Time.unscaledTime + seconds;
    }

    void Update()
    {
        // Count coins up smoothly and pop the wallet when they change.
        int target = PlayerProgress.Coins;
        if (shownCoins != target)
            shownCoins = (int)Mathf.MoveTowards(shownCoins, target, Mathf.Max(1f, Mathf.Abs(target - shownCoins) * 4f * Time.unscaledDeltaTime));
        coins.text = shownCoins.ToString();
        coinPop = Mathf.MoveTowards(coinPop, 0f, Time.unscaledDeltaTime * 2.5f);
        coinGroup.localScale = Vector3.one * (1f + Mathf.Sin(coinPop * Mathf.PI) * 0.12f);

        float left = toastUntil - Time.unscaledTime;
        toastGroup.alpha = Mathf.Clamp01(left / 0.5f);
    }
}
