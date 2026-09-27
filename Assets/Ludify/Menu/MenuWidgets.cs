using System;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Menu
{
    /// <summary>Themed controls for the menus, built in code with <see cref="UiKit"/>.</summary>
    public static class MenuWidgets
    {
        /// <summary>A cream panel with a vertical layout, optional title card and subject decor.</summary>
        public static RectTransform Panel(Transform parent, string title, Vector2 size, Vector2 anchor, Vector2 position, bool decor = true)
        {
            Image panel = UiKit.Image("Panel", parent, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            UiKit.Place(panel.rectTransform, anchor, position, size);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 28, 30);
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            if (decor) SubjectDecor.Add(panel.rectTransform, 12, 0.12f);
            if (!string.IsNullOrEmpty(title)) UiKit.TitleCard(panel.transform, title);
            return panel.rectTransform;
        }

        public static Transform Row(Transform parent, float height, float spacing = 14)
        {
            Transform row = UiKit.Rect("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flex = -1)
        {
            LayoutElement le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            if (flex >= 0) le.flexibleWidth = flex;
            return le;
        }

        /// <summary>Vertical scrolling list; returns the content transform to add rows to.</summary>
        public static RectTransform ScrollList(Transform parent, float height, out ScrollRect scroll, float spacing = 10)
        {
            Image frame = UiKit.Image("List", parent, UiKit.InsetColor, UiKit.RoundedSprite, raycast: true);
            Size(frame, height: height, flex: 1);
            var sr = frame.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", frame.transform), 10);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(4, 4, 4, 4);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = content;
            scroll = sr;
            return content;
        }

        /// <summary>Label on the left, control on the right.</summary>
        public static Transform SettingRow(Transform parent, string label, string hint = null)
        {
            Transform row = Row(parent, hint == null ? 58 : 70);
            TextMeshProUGUI t = UiKit.Text("Label", row, hint == null ? label : $"{label}\n<size=70%>{hint}</size>", 24, TextAlignmentOptions.MidlineLeft);
            Size(t, width: 420, flex: 0);
            return row;
        }

        /// <summary>Chunky themed slider (0..1).</summary>
        public static Slider Slider(Transform parent, float value, Action<float> onChange)
        {
            RectTransform root = UiKit.Rect("Slider", parent);
            Size(root, flex: 1);
            Image track = UiKit.Image("Track", root, UiKit.InsetColor, ThemeArt.Button);
            track.type = Image.Type.Sliced;
            UiKit.Stretch(track.rectTransform);
            track.rectTransform.anchorMin = new Vector2(0, 0.3f);
            track.rectTransform.anchorMax = new Vector2(1, 0.7f);
            RectTransform fillArea = UiKit.Stretch(UiKit.Rect("FillArea", root));
            fillArea.anchorMin = new Vector2(0, 0.3f);
            fillArea.anchorMax = new Vector2(1, 0.7f);
            Image fill = UiKit.Image("Fill", fillArea, UiKit.AccentColor, ThemeArt.Button);
            fill.type = Image.Type.Sliced;
            UiKit.Stretch(fill.rectTransform);
            RectTransform handleArea = UiKit.Stretch(UiKit.Rect("HandleArea", root), 0);
            handleArea.offsetMin = new Vector2(16, 0);
            handleArea.offsetMax = new Vector2(-16, 0);
            Image handle = UiKit.Image("Handle", handleArea, UiKit.PanelColor, UiKit.CircleSprite, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(38, 38);
            handle.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0.3f, 0.2f, 0.5f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        /// <summary>A button that steps through options each click, showing the current one (with a drawn chevron).</summary>
        public static Button Cycle(Transform parent, string[] options, int index, Action<int> onChange, Color? color = null)
        {
            int current = Mathf.Clamp(index, 0, options.Length - 1);
            Button b = null;
            b = UiKit.Button("Cycle", parent, options[current], 22, () =>
            {
                current = (current + 1) % options.Length;
                UiKit.SetButtonLabel(b, options[current]);
                onChange(current);
            }, color ?? UiKit.AccentColor);
            Chevron(b.transform);
            Size(b, flex: 1);
            return b;
        }

        /// <summary>A small "›" drawn at a button's right edge (the UI font has no arrow glyphs).</summary>
        public static void Chevron(Transform button, string icon = "chevron")
        {
            Image c = UiKit.Image("Chevron", button, LudifyTheme.Palette.LightText, ThemeArt.Icon(icon));
            c.preserveAspect = true;
            RectTransform rt = c.rectTransform;
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-12, 4);
            rt.sizeDelta = new Vector2(22, 22);
            var label = button.Find("Label") as RectTransform;
            if (label != null) label.offsetMax = new Vector2(-38, label.offsetMax.y);
        }

        /// <summary>A keyboard key cap like [ Esc ].</summary>
        public static void KeyCap(Transform parent, string key)
        {
            Image cap = UiKit.Image("Key", parent, UiKit.PanelColor, ThemeArt.Button);
            cap.type = Image.Type.Sliced;
            cap.gameObject.AddComponent<Outline>().effectColor = new Color(0.45f, 0.35f, 0.25f, 0.6f);
            Size(cap, width: Mathf.Max(56, key.Length * 16 + 30), height: 50, flex: 0);
            TextMeshProUGUI t = UiKit.Text("Text", cap.transform, key, 20);
            UiKit.Stretch(t.rectTransform, 4);
            t.rectTransform.offsetMin = new Vector2(4, 10);
        }
    }
}
