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
        // Theme colours (see LudifyTheme): read at build time, and kept live by ThemedGraphic.
        public static Color PanelColor => LudifyTheme.Palette.Panel;
        public static Color ButtonColor => LudifyTheme.Palette.Neutral;
        public static Color AccentColor => LudifyTheme.Palette.Accent;
        public static Color TextColor => LudifyTheme.Palette.Text;
        public static Color MutedTextColor => LudifyTheme.Palette.MutedText;
        public static Color InsetColor => LudifyTheme.Palette.Inset;
        public static readonly Color CorrectColor = new Color(0.33f, 0.72f, 0.42f, 1f);
        public static readonly Color WrongColor = new Color(0.9f, 0.36f, 0.32f, 1f);

        static Sprite _circle, _ring, _arrow;
        static TMP_FontAsset _font;

        /// <summary>The UI font: Fredoka SemiBold (SIL OFL), built at runtime from the TTF so it also works in builds.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                Font ttf = Resources.Load<Font>("Fonts/Fredoka-SemiBold");
                if (ttf == null) return null;
                _font = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                                                      AtlasPopulationMode.Dynamic, true);
                if (_font == null) return null;
                _font.name = "Fredoka (runtime)";
                TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
                if (fallback != null) _font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
                return _font;
            }
        }

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

        /// <summary>
        /// An image. Panels (RoundedSprite) get the reference's cream look with a rim and a soft drop shadow.
        /// Anything drawn in a theme colour follows the theme when the subject changes.
        /// </summary>
        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            if (sprite != null && sprite == ThemeArt.Panel)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
                var shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.2f, 0.14f, 0.08f, 0.28f);
                shadow.effectDistance = new Vector2(0, -6);
            }
            ThemeRole? role = RoleOf(color);
            if (role.HasValue) ThemedGraphic.Attach(image, role.Value);
            return image;
        }

        /// <summary>Which theme colour (if any) a colour is, so it can follow subject changes.</summary>
        static ThemeRole? RoleOf(Color c)
        {
            SubjectPalette p = LudifyTheme.Palette;
            foreach (ThemeRole role in new[] { ThemeRole.Panel, ThemeRole.Neutral, ThemeRole.Accent, ThemeRole.Inset })
            {
                Color r = ThemedGraphic.ColorFor(role, p);
                if (Mathf.Abs(r.r - c.r) < 0.004f && Mathf.Abs(r.g - c.g) < 0.004f && Mathf.Abs(r.b - c.b) < 0.004f) return role;
            }
            return null;
        }

        /// <summary>Text in the UI font. Without a colour it picks dark or light to contrast with what's behind it.</summary>
        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var tmp = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color ?? TextColor;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            // Faint decorative text (e.g. subject symbols) keeps its exact colour.
            if (color == null || color.Value.a >= 0.5f) AutoContrastText.Attach(tmp, color);
            return tmp;
        }

        /// <summary>
        /// The reference's chunky pill button (darker bottom lip). With an <paramref name="icon"/>, a darker
        /// block on the left holds it, like the reference menu.
        /// </summary>
        public static Button Button(string name, Transform parent, string label, float fontSize, UnityAction onClick,
                                    Color? color = null, Sprite icon = null)
        {
            Image bg = Image(name, parent, color ?? ButtonColor, ThemeArt.Button, raycast: true);
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);

            float left = 8;
            if (icon != null)
            {
                Image block = Image("IconBlock", bg.transform, Color.Lerp(bg.color, Color.black, 0.18f), ThemeArt.Button);
                block.type = UnityEngine.UI.Image.Type.Sliced;
                RectTransform brt = block.rectTransform;
                brt.anchorMin = new Vector2(0, 0);
                brt.anchorMax = new Vector2(0, 1);
                brt.pivot = new Vector2(0, 0.5f);
                brt.sizeDelta = new Vector2(0, 0);
                brt.offsetMin = new Vector2(0, 0);
                block.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                Image glyph = Image("Icon", block.transform, Color.white, icon);
                Stretch(glyph.rectTransform, 0);
                glyph.rectTransform.anchorMin = new Vector2(0.2f, 0.24f);
                glyph.rectTransform.anchorMax = new Vector2(0.8f, 0.84f);
                glyph.preserveAspect = true;
                left = 0;   // label is inset by the block width below
                button.gameObject.AddComponent<IconButtonLayout>();
            }
            if (label != null) // "" still creates a label that can be filled in later
            {
                TextMeshProUGUI t = Text("Label", bg.transform, label, fontSize);
                Stretch(t.rectTransform, 8);
                t.rectTransform.offsetMin = new Vector2(left + 8, 12);   // leave room for the bottom lip
                t.fontStyle = FontStyles.Normal;
                if (icon != null) t.alignment = TextAlignmentOptions.MidlineLeft;
            }
            return button;
        }

        /// <summary>A TMP input field (multi-line if <paramref name="multiline"/>), with placeholder text.</summary>
        public static TMP_InputField InputField(string name, Transform parent, string placeholder, float fontSize, bool multiline)
        {
            Image bg = Image(name, parent, InsetColor, ThemeArt.Button, raycast: true);
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            RectTransform viewport = Stretch(Rect("Viewport", bg.transform), 12);
            viewport.offsetMin = new Vector2(14, 16);
            viewport.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = Text("Placeholder", viewport, placeholder, fontSize,
                                        multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left, MutedTextColor);
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            TextMeshProUGUI text = Text("Text", viewport, "", fontSize, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left, TextColor);
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
            field.caretColor = TextColor;
            field.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.4f);
            return field;
        }

        public static void SetButtonLabel(Button button, string label) =>
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;

        /// <summary>A cream "title tab" like the reference's "Menu" header, with optional subject decor.</summary>
        public static TextMeshProUGUI TitleCard(Transform parent, string title, float fontSize = 40)
        {
            Image tab = Image("TitleCard", parent, PanelColor, ThemeArt.Panel);
            tab.gameObject.AddComponent<LayoutElement>().preferredHeight = fontSize * 1.9f;
            TextMeshProUGUI t = Text("Title", tab.transform, title, fontSize, TextAlignmentOptions.Center, TextColor);
            ThemedGraphic.Attach(t, ThemeRole.Text);
            Stretch(t.rectTransform, 10);
            return t;
        }

        // ---- Procedural sprites (no texture assets needed) ----

        public static Sprite CircleSprite => Alive(_circle) ?? (_circle = MakeSprite(256, (x, y, r) => Coverage(r - Dist(x, y, r))));

        /// <summary>A ring whose thickness is ~8% of the radius.</summary>
        public static Sprite RingSprite => Alive(_ring) ?? (_ring = MakeSprite(256, (x, y, r) =>
        {
            float d = Dist(x, y, r);
            return Mathf.Min(Coverage(r - d), Coverage(d - r * 0.92f));
        }));

        /// <summary>Upward-pointing arrow (north = up), for the player marker.</summary>
        public static Sprite ArrowSprite => Alive(_arrow) ?? (_arrow = MakeSprite(128, (x, y, r) =>
        {
            float u = x / (2 * r) - 0.5f, v = y / (2 * r);          // u in [-0.5,0.5], v in [0,1] (bottom→top)
            float halfWidth = 0.42f * (1 - v);                      // triangle from wide base to tip
            bool inTriangle = v > 0.08f && v < 0.95f && Mathf.Abs(u) < halfWidth;
            bool notch = v < 0.35f && Mathf.Abs(u) < (0.35f - v) * 0.9f;
            return inTriangle && !notch ? 1f : 0f;
        }));

        /// <summary>The reference's cream panel shape (rim + rounded corners), 9-sliced. Tint with the panel colour.</summary>
        public static Sprite RoundedSprite => ThemeArt.Panel;

        /// <summary>
        /// Unity destroys objects made during Play mode when it ends, but with "Enter Play Mode Options"
        /// (no domain reload) static fields keep pointing at the dead objects. C#'s <c>??</c> doesn't see
        /// Unity's "destroyed" state, so the next session drew plain squares (e.g. a white minimap).
        /// Alive() returns null for destroyed objects, so they get rebuilt.
        /// </summary>
        internal static T Alive<T>(T obj) where T : UnityEngine.Object => obj != null ? obj : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCaches() { _circle = _ring = _arrow = null; _font = null; }

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
