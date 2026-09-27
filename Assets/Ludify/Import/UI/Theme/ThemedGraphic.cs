using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Import
{
    public enum ThemeRole { Panel, Neutral, Accent, AccentDark, Inset, Text, MutedText }

    /// <summary>
    /// Keeps a UI graphic in its theme colour and recolours it when the subject changes.
    /// Added automatically by <see cref="UiKit"/> to anything drawn in a theme colour.
    /// </summary>
    public sealed class ThemedGraphic : MonoBehaviour
    {
        public ThemeRole Role;
        /// <summary>Fixed subject (e.g. a bundle card), or null to follow the current theme.</summary>
        public Subject? Subject;
        public float Alpha = 1f;

        Graphic _graphic;

        public static ThemedGraphic Attach(Graphic g, ThemeRole role, Subject? subject = null)
        {
            float alpha = g.color.a;   // read before AddComponent: its OnEnable already applies a colour
            var t = g.GetComponent<ThemedGraphic>() ?? g.gameObject.AddComponent<ThemedGraphic>();
            t.Role = role;
            t.Subject = subject;
            t.Alpha = alpha;
            t._graphic = g;
            t.Apply();
            return t;
        }

        public static Color ColorFor(ThemeRole role, SubjectPalette p)
        {
            switch (role)
            {
                case ThemeRole.Panel: return p.Panel;
                case ThemeRole.Neutral: return p.Neutral;
                case ThemeRole.Accent: return p.Accent;
                case ThemeRole.AccentDark: return p.AccentDark;
                case ThemeRole.Inset: return p.Inset;
                case ThemeRole.Text: return p.Text;
                default: return p.MutedText;
            }
        }

        void OnEnable()
        {
            LudifyTheme.Changed += Apply;
            Apply();
        }

        void OnDisable() => LudifyTheme.Changed -= Apply;

        public void Apply()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;
            SubjectPalette p = Subject.HasValue ? LudifyTheme.For(Subject.Value) : LudifyTheme.Palette;
            Color c = ColorFor(Role, p);
            c.a = Alpha;
            _graphic.color = c;
        }
    }

    /// <summary>
    /// Keeps text readable whatever it sits on. Text without a colour becomes dark brown on light panels
    /// and white on dark or coloured ones. Text *with* a colour keeps it, except light colours on a light
    /// panel (e.g. white or gold text written for the old dark UI) are darkened so they still read.
    /// Text over the 3D world gets an outline.
    /// </summary>
    public sealed class AutoContrastText : MonoBehaviour
    {
        TMP_Text _text;
        Color? _original;
        bool _captured;
        Color _lastBackground = new Color(-1, -1, -1, -1);
        Color? _lastApplied;

        public static void Attach(TMP_Text text, Color? original)
        {
            var a = text.gameObject.AddComponent<AutoContrastText>();
            a._text = text;
            a._original = original;
            a._captured = true;
            a.Apply();
        }

        void OnEnable()
        {
            LudifyTheme.Changed += Apply;
            Apply();
        }

        void OnDisable() => LudifyTheme.Changed -= Apply;

        // Backgrounds can be recoloured after the text is created (e.g. answer buttons), so re-check cheaply.
        void LateUpdate()
        {
            // Someone else set a new colour since we last did: that's the new intended colour.
            if (_text != null && _lastApplied.HasValue && _text.color != _lastApplied.Value)
            {
                _original = _text.color;
                Apply();
                return;
            }
            Graphic bg = Background();
            Color c = bg != null ? bg.color : Color.clear;
            if (c != _lastBackground) Apply();
        }

        Graphic Background()
        {
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                Graphic g = t.GetComponent<Graphic>();
                if (g != null && !(g is TMP_Text) && g.color.a > 0.35f && g.enabled) return g;
            }
            return null;
        }

        static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        public void Apply()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null) return;
            if (!_captured) { _original = null; _captured = true; }
            Graphic bg = Background();
            _lastBackground = bg != null ? bg.color : Color.clear;
            bool lightBackground = bg != null && Luminance(bg.color) > 0.62f;

            Color result;
            if (_original == null)
                result = bg == null ? Color.white : lightBackground ? LudifyTheme.Palette.Text : LudifyTheme.Palette.LightText;
            else
            {
                Color o = _original.Value;
                result = o;
                if (lightBackground && Luminance(o) > 0.55f)
                {
                    // Written for the old dark panels: keep the hue, but make it dark enough to read on cream.
                    Color.RGBToHSV(o, out float h, out float sat, out float v);
                    result = sat < 0.15f ? LudifyTheme.Palette.Text : Color.HSVToRGB(h, Mathf.Max(sat, 0.55f), 0.55f);
                    result.a = o.a;
                }
            }
            _text.color = result;
            _lastApplied = result;
            if (bg == null)
            {
                _text.outlineWidth = 0.18f;
                _text.outlineColor = new Color32(40, 30, 20, 220);
            }
        }
    }
}
