using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Import
{
    /// <summary>
    /// Faint subject symbols floating behind a panel: DNA, beakers and flasks for science; operators and
    /// numbers for math; books, scrolls and columns for history; and so on. Follows the current theme
    /// unless given a fixed subject (e.g. a subject bundle card).
    /// </summary>
    public sealed class SubjectDecor : MonoBehaviour
    {
        Subject? _fixed;
        int _seed;
        int _count;
        float _alpha;
        readonly List<(RectTransform rt, Vector2 home, float phase, float spin)> _items = new List<(RectTransform, Vector2, float, float)>();

        /// <summary>Adds decor as the first child of <paramref name="panel"/> (so it sits behind the content).</summary>
        public static SubjectDecor Add(RectTransform panel, int count = 10, float alpha = 0.13f, Subject? subject = null)
        {
            RectTransform layer = UiKit.Rect("Decor", panel);
            UiKit.Stretch(layer);
            layer.SetAsFirstSibling();
            var le = layer.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            layer.gameObject.AddComponent<RectMask2D>();
            var decor = layer.gameObject.AddComponent<SubjectDecor>();
            decor._fixed = subject;
            decor._count = count;
            decor._alpha = alpha;
            decor.Build();
            return decor;
        }

        void OnEnable() { if (_fixed == null) LudifyTheme.Changed += Build; }
        void OnDisable() { if (_fixed == null) LudifyTheme.Changed -= Build; }

        void Build()
        {
            foreach (var item in _items) if (item.rt != null) Destroy(item.rt.gameObject);
            _items.Clear();

            Subject s = _fixed ?? LudifyTheme.Current;
            string[] symbols = ThemeArt.Symbols[s];
            Color tint = LudifyTheme.For(s).Accent;
            tint.a = _alpha;
            var random = new System.Random(_seed == 0 ? (_seed = UnityEngine.Random.Range(1, int.MaxValue)) : _seed);   // stable per panel
            for (int i = 0; i < _count; i++)
            {
                string sym = symbols[i % symbols.Length];
                float size = 44 + (float)random.NextDouble() * 40;
                Vector2 pos = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                RectTransform rt;
                if (ThemeArt.IsGlyph(sym))
                {
                    TextMeshProUGUI t = UiKit.Text("Glyph", transform, sym, size * 0.9f, TextAlignmentOptions.Center, tint);
                    Destroy(t.GetComponent<AutoContrastText>());
                    t.color = tint;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    rt = t.rectTransform;
                }
                else
                {
                    Image img = UiKit.Image("Symbol", transform, tint, ThemeArt.Icon(sym));
                    rt = img.rectTransform;
                }
                rt.anchorMin = rt.anchorMax = pos;
                rt.sizeDelta = new Vector2(size, size);
                rt.localRotation = Quaternion.Euler(0, 0, (float)random.NextDouble() * 50 - 25);
                _items.Add((rt, Vector2.zero, (float)random.NextDouble() * 10f, (float)random.NextDouble() * 6f - 3f));
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            foreach (var (rt, _, phase, spin) in _items)
            {
                if (rt == null) continue;
                rt.anchoredPosition = new Vector2(Mathf.Sin(t * 0.4f + phase) * 6f, Mathf.Sin(t * 0.55f + phase * 1.3f) * 8f);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.3f + phase) * 10f + spin * 3f);
            }
        }
    }
}
