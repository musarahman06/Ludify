using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Ludify.Import
{
    /// <summary>
    /// Helpers for building uGUI in code (no prefabs, no scene edits).
    /// Canvases use a 1920x1080 reference resolution matched on height.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color PanelColor = new Color(0.09f, 0.11f, 0.15f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.27f, 0.36f, 1f);
        public static readonly Color AccentColor = new Color(0.25f, 0.55f, 0.95f, 1f);
        public static readonly Color CorrectColor = new Color(0.2f, 0.65f, 0.3f, 1f);
        public static readonly Color WrongColor = new Color(0.8f, 0.25f, 0.25f, 1f);

        static Sprite _circle, _ring, _arrow, _rounded;

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            LessonFilePicker.EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors and pivots at a normalized point, with a fixed size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var tmp = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color ?? Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button Button(string name, Transform parent, string label, float fontSize, UnityAction onClick, Color? color = null)
        {
            Image bg = Image(name, parent, color ?? ButtonColor, RoundedSprite, raycast: true);
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            if (label != null) // "" still creates a label that can be filled in later
                Stretch(Text("Label", bg.transform, label, fontSize).rectTransform, 8);
            return button;
        }

        /// <summary>A TMP input field (multi-line if <paramref name="multiline"/>), with placeholder text.</summary>
        public static TMP_InputField InputField(string name, Transform parent, string placeholder, float fontSize, bool multiline)
        {
            Image bg = Image(name, parent, new Color(0.05f, 0.06f, 0.08f, 1f), RoundedSprite, raycast: true);
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            RectTransform viewport = Stretch(Rect("Viewport", bg.transform), 12);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Text("Placeholder", viewport, placeholder, fontSize,
                                        multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left, new Color(1, 1, 1, 0.35f));
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            TextMeshProUGUI text = Text("Text", viewport, "", fontSize, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            var field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = bg;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            field.characterLimit = 0;
            field.richText = false;
            field.caretWidth = 2;
            field.customCaretColor = true;
            field.caretColor = Color.white;
            field.selectionColor = new Color(0.25f, 0.55f, 0.95f, 0.5f);
            return field;
        }

        public static void SetButtonLabel(Button button, string label) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;

        // ---- Procedural sprites (no texture assets needed) ----

        public static Sprite CircleSprite => _circle ?? (_circle = MakeSprite(256, (x, y, r) => Coverage(r - Dist(x, y, r))));

        /// <summary>A ring whose thickness is ~8% of the radius.</summary>
        public static Sprite RingSprite => _ring ?? (_ring = MakeSprite(256, (x, y, r) =>
        {
            float d = Dist(x, y, r);
            return Mathf.Min(Coverage(r - d), Coverage(d - r * 0.92f));
        }));

        /// <summary>Upward-pointing arrow (north = up), for the player marker.</summary>
        public static Sprite ArrowSprite => _arrow ?? (_arrow = MakeSprite(128, (x, y, r) =>
        {
            float u = x / (2 * r) - 0.5f, v = y / (2 * r);          // u in [-0.5,0.5], v in [0,1] (bottom→top)
            float halfWidth = 0.42f * (1 - v);                      // triangle from wide base to tip
            bool inTriangle = v > 0.08f && v < 0.95f && Mathf.Abs(u) < halfWidth;
            bool notch = v < 0.35f && Mathf.Abs(u) < (0.35f - v) * 0.9f;
            return inTriangle && !notch ? 1f : 0f;
        }));

        /// <summary>Rounded rectangle for 9-sliced buttons and panels.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64, radius = 16;
                var tex = NewTexture(size);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Coverage(radius - d)));
                }
                tex.Apply();
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
                                         SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return _rounded;
            }
        }

        static Sprite MakeSprite(int size, Func<float, float, float, float> alpha)
        {
            var tex = NewTexture(size);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha(x + 0.5f, y + 0.5f, r)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        static float Dist(float x, float y, float r) => Vector2.Distance(new Vector2(x, y), new Vector2(r, r));

        /// <summary>1px anti-aliasing: signed distance (inside positive) → alpha.</summary>
        static float Coverage(float signedDistance) => Mathf.Clamp01(signedDistance + 0.5f);
    }
}
