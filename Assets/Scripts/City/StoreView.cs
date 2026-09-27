using System.Collections.Generic;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Slot = Wardrobe.Slot;

/// <summary>
/// The clothing store: tabs for shirts, hats, glasses, pants and shoes, a grid of items, and a live preview of your
/// character. Click an item to try it on, then buy it with coins (or wear it if you own it). Open it from the
/// shopkeeper in the city (E) or anywhere on foot with B. Closing puts your saved outfit back on.
/// </summary>
public class StoreView : MonoBehaviour
{
    const int Columns = 4;
    static readonly string[] TabNames = { "Shirts", "Hats", "Glasses", "Pants", "Shoes" };

    public static StoreView Instance { get; private set; }
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    PlayerController player;
    RectTransform panel, grid;
    TextMeshProUGUI coins, itemName, status;
    Button action;
    readonly List<Button> tabs = new List<Button>();
    readonly List<(Wardrobe.Item item, Button card, TextMeshProUGUI price)> cards = new List<(Wardrobe.Item, Button, TextMeshProUGUI)>();
    Slot tab = Slot.Shirt;
    Wardrobe.Item selected;
    int openedFrame;

    // Preview
    Camera previewCamera;
    RenderTexture previewTexture;
    RawImage preview;
    float previewAngle;

    // Paused while shopping
    readonly List<Behaviour> paused = new List<Behaviour>();

    public static StoreView Create(PlayerController player)
    {
        var canvas = UiKit.CreateCanvas("StoreCanvas", 70);
        var view = canvas.gameObject.AddComponent<StoreView>();
        view.player = player;
        view.Build(canvas.transform);
        Instance = view;
        return view;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (previewTexture != null) previewTexture.Release();
    }

    // ------------------------------------------------------------------ layout

    void Build(Transform canvas)
    {
        var dim = UiKit.Image("Dim", canvas, new Color(0, 0, 0, 0.6f), raycast: true);
        UiKit.Stretch(dim.rectTransform);
        var bg = UiKit.Image("Panel", dim.transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
        bg.type = Image.Type.Sliced;
        panel = dim.rectTransform;
        var box = UiKit.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1440f, 820f));

        var title = UiKit.Text("Title", box, "Clothing Store", 44, TextAlignmentOptions.Left, new Color(1f, 0.6f, 0.8f));
        title.fontStyle = FontStyles.Bold;
        UiKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -24f), new Vector2(600f, 60f));

        var coinIcon = UiKit.Image("Coin", box, new Color(1f, 0.8f, 0.2f), UiKit.CircleSprite);
        UiKit.Place(coinIcon.rectTransform, new Vector2(1f, 1f), new Vector2(-250f, -34f), new Vector2(40f, 40f));
        coins = UiKit.Text("Coins", box, "", 36, TextAlignmentOptions.Left);
        coins.fontStyle = FontStyles.Bold;
        UiKit.Place(coins.rectTransform, new Vector2(1f, 1f), new Vector2(-100f, -28f), new Vector2(140f, 50f));

        var close = UiKit.Button("Close", box, "X", 28, Close, UiKit.WrongColor);
        UiKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(52f, 52f));

        // Category tabs.
        for (int i = 0; i < TabNames.Length; i++)
        {
            var slot = Wardrobe.Slots[i];
            var b = UiKit.Button("Tab" + TabNames[i], box, TabNames[i], 26, () => ShowTab(slot));
            UiKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(40f + i * 180f, -104f), new Vector2(170f, 56f));
            tabs.Add(b);
        }

        // Item grid (left).
        grid = UiKit.Place(UiKit.Rect("Grid", box), new Vector2(0f, 1f), new Vector2(40f, -186f), new Vector2(880f, 600f));

        // Preview + buy button (right).
        var frame = UiKit.Image("PreviewFrame", box, new Color(0.16f, 0.2f, 0.27f), UiKit.RoundedSprite);
        frame.type = Image.Type.Sliced;
        UiKit.Place(frame.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -104f), new Vector2(440f, 520f));
        preview = UiKit.Rect("Preview", frame.transform).gameObject.AddComponent<RawImage>();
        UiKit.Stretch(preview.rectTransform, 8f);
        preview.raycastTarget = false;

        itemName = UiKit.Text("ItemName", box, "", 30, TextAlignmentOptions.Center);
        itemName.fontStyle = FontStyles.Bold;
        UiKit.Place(itemName.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -636f), new Vector2(440f, 40f));
        action = UiKit.Button("Action", box, "", 28, OnAction, UiKit.CorrectColor);
        UiKit.Place((RectTransform)action.transform, new Vector2(1f, 1f), new Vector2(-40f, -684f), new Vector2(440f, 64f));
        status = UiKit.Text("Status", box, "", 22, TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 1f));
        UiKit.Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -756f), new Vector2(440f, 34f));

        var help = UiKit.Text("Help", box, "Click an item to try it on  ·  Esc / B to close", 20, TextAlignmentOptions.Left, new Color(1f, 1f, 1f, 0.55f));
        UiKit.Place(help.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 18f), new Vector2(880f, 30f));

        panel.gameObject.SetActive(false);
        PlayerProgress.Changed += RefreshCoins;
    }

    void OnDisable() => PlayerProgress.Changed -= RefreshCoins;
    void OnEnable() { if (panel != null) PlayerProgress.Changed += RefreshCoins; }

    void RefreshCoins() { if (coins != null) coins.text = PlayerProgress.Coins.ToString(); }

    // ------------------------------------------------------------------ open / close

    /// <summary>On foot, not racing, and nothing else (map, dialogue, question) open.</summary>
    public bool CanOpen()
    {
        if (player == null || !player.gameObject.activeInHierarchy || !player.enabled) return false;
        var trial = FindAnyObjectByType<TimeTrialManager>();
        if (trial != null && trial.CurrentState != TimeTrialManager.State.Idle) return false;
        if (QuestionPrompt.IsOpen || LessonFilePicker.IsOpen) return false;
        var map = FindAnyObjectByType<Ludify.Map.MapSystem>();
        if (map != null && map.IsFullMapOpen) return false;
        var dialogue = FindAnyObjectByType<DialogueBox>();
        return dialogue == null || !dialogue.IsOpen;
    }

    public void Open()
    {
        if (IsOpen || player == null) return;
        panel.gameObject.SetActive(true);
        openedFrame = Time.frameCount;
        Pause(player);
        Pause(FindAnyObjectByType<OrbitCamera>());
        Pause(FindAnyObjectByType<VehicleInteraction>());
        StartPreview();
        RefreshCoins();
        ShowTab(tab);
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.gameObject.SetActive(false);
        Wardrobe.ApplyEquipped(player.visualRoot);   // take off anything only tried on
        StopPreview();
        SetPreviewLayer(false);
        foreach (var b in paused) if (b != null) b.enabled = true;
        paused.Clear();
    }

    void Pause(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        paused.Add(b);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!IsOpen)
        {
            if (kb.bKey.wasPressedThisFrame && CanOpen()) Open();
            return;
        }
        if (Time.frameCount != openedFrame && (kb.escapeKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame)) Close();
        var map = FindAnyObjectByType<Ludify.Map.MapSystem>();
        if (map != null && map.IsFullMapOpen) map.CloseFullMap();   // M while shopping: stay in the store
        UpdatePreview();
    }

    // ------------------------------------------------------------------ items

    void ShowTab(Slot slot)
    {
        tab = slot;
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].targetGraphic.color = Wardrobe.Slots[i] == slot ? UiKit.AccentColor : UiKit.ButtonColor;

        foreach (var c in cards) Destroy(c.card.gameObject);
        cards.Clear();
        int index = 0;
        foreach (var item in Wardrobe.InSlot(slot))
        {
            int col = index % Columns, row = index / Columns;
            var it = item;
            var card = UiKit.Button("Card_" + item.Id, grid, null, 0, () => Select(it));
            UiKit.Place((RectTransform)card.transform, new Vector2(0f, 1f), new Vector2(col * 220f, -row * 200f), new Vector2(205f, 185f));

            var swatch = UiKit.Image("Swatch", card.transform, Wardrobe.SwatchColor(item, player.visualRoot), UiKit.CircleSprite);
            UiKit.Place(swatch.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(70f, 70f));
            if (item.Style == Wardrobe.Style.Striped)
                foreach (float y in new[] { -14f, 0f, 14f })
                    UiKit.Place(UiKit.Image("Stripe", swatch.transform, item.Accent).rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(56f, 6f));
            UiKit.Stretch(UiKit.Image("Ring", swatch.transform, new Color(1f, 1f, 1f, 0.6f), UiKit.RingSprite).rectTransform);

            var name = UiKit.Text("Name", card.transform, item.Name, 22);
            UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(195f, 34f));
            var price = UiKit.Text("Price", card.transform, "", 20, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.35f));
            UiKit.Place(price.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -134f), new Vector2(195f, 30f));

            cards.Add((item, card, price));
            index++;
        }
        Select(Wardrobe.Equipped(slot));
    }

    void Select(Wardrobe.Item item)
    {
        selected = item;
        // Try it on (the rest of the outfit stays as saved).
        Wardrobe.ApplyEquipped(player.visualRoot);
        Wardrobe.Apply(player.visualRoot, item);
        SetPreviewLayer(true);
        Refresh();
    }

    void Refresh()
    {
        var wearing = Wardrobe.Equipped(tab);
        foreach (var (item, card, price) in cards)
        {
            card.targetGraphic.color = item == selected ? new Color(0.55f, 0.3f, 0.5f) : UiKit.ButtonColor;
            price.text = item == wearing ? "Wearing" : Wardrobe.Owns(item) ? "Owned" : $"{item.Price} coins";
            price.color = item == wearing ? new Color(0.45f, 1f, 0.55f) : Wardrobe.Owns(item) ? Color.white : new Color(1f, 0.85f, 0.35f);
        }
        if (selected == null) return;

        itemName.text = selected.Name;
        bool owned = Wardrobe.Owns(selected);
        bool isWorn = selected == wearing;
        bool affordable = PlayerProgress.Coins >= selected.Price;
        action.interactable = !isWorn && (owned || affordable);
        action.targetGraphic.color = isWorn ? UiKit.ButtonColor : owned ? UiKit.AccentColor : affordable ? UiKit.CorrectColor : UiKit.WrongColor;
        UiKit.SetButtonLabel(action, isWorn ? "Wearing" : owned ? "Wear" : affordable ? $"Buy  ({selected.Price} coins)" : $"Need {selected.Price - PlayerProgress.Coins} more coins");
    }

    void OnAction()
    {
        if (selected == null) return;
        if (!Wardrobe.Owns(selected))
        {
            if (!PlayerProgress.TrySpend(selected.Price))
            {
                status.text = "Not enough coins yet. Help people in the city or race to earn more!";
                return;
            }
            PlayerProgress.AddOwned(selected.Id);
            status.text = $"You bought the {selected.Name}!";
        }
        else status.text = $"Now wearing the {selected.Name}.";
        PlayerProgress.Equip(selected.Slot.ToString(), selected.Id);
        Wardrobe.ApplyEquipped(player.visualRoot);
        SetPreviewLayer(true);
        Refresh();
    }

    // ------------------------------------------------------------------ live preview

    void StartPreview()
    {
        if (previewTexture == null)
        {
            previewTexture = new RenderTexture(512, 600, 24, RenderTextureFormat.ARGB32) { name = "StorePreview", antiAliasing = 2 };
            previewTexture.Create();
        }
        preview.texture = previewTexture;
        var go = new GameObject("StorePreviewCamera");
        previewCamera = go.AddComponent<Camera>();
        previewCamera.targetTexture = previewTexture;
        previewCamera.fieldOfView = 30f;
        previewCamera.nearClipPlane = 0.1f;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.16f, 0.2f, 0.27f);
        previewCamera.cullingMask = 1 << PreviewLayer;   // just the character, on a plain background
        SetPreviewLayer(true);
        previewAngle = 0f;
        UpdatePreview();
    }

    /// <summary>A spare layer only the preview camera draws alone (the main camera still sees everything).</summary>
    const int PreviewLayer = 31;
    readonly Dictionary<GameObject, int> savedLayers = new Dictionary<GameObject, int>();

    void SetPreviewLayer(bool on)
    {
        if (player == null) return;
        foreach (var t in player.visualRoot.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            if (on)
            {
                if (go.layer != PreviewLayer) { savedLayers[go] = go.layer; go.layer = PreviewLayer; }
            }
            else if (savedLayers.TryGetValue(go, out int layer)) go.layer = layer;
            else if (go.layer == PreviewLayer) go.layer = 0;   // pieces put on during the visit
        }
        if (!on) savedLayers.Clear();
    }

    void StopPreview()
    {
        if (previewCamera != null) Destroy(previewCamera.gameObject);
        previewCamera = null;
    }

    void UpdatePreview()
    {
        if (previewCamera == null || player == null) return;
        previewAngle += Time.unscaledDeltaTime * 25f;
        // Mostly the front, swinging gently from side to side so hats and glasses read well.
        float yaw = player.transform.eulerAngles.y + Mathf.Sin(previewAngle * Mathf.Deg2Rad * 2f) * 50f;
        Vector3 focus = player.transform.position + Vector3.up * 1.15f;
        Vector3 dir = Quaternion.Euler(8f, yaw, 0f) * Vector3.forward;
        previewCamera.transform.position = focus + dir * 5.2f;
        previewCamera.transform.LookAt(focus);
    }
}
