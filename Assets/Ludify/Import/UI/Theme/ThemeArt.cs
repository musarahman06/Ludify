using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// All UI art is drawn in code (no image assets): the reference's chunky panel and button shapes,
    /// and white icons (tinted at use) for menus and subject symbols.
    /// Coordinates for icons are normalised 0..1 with y up.
    /// </summary>
    public static class ThemeArt
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Cache.Clear();

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;   // Unity null check: rebuilt after Play mode ends
            return Cache[key] = make();
        }

        // ---- Panels and buttons (grey levels become shades of the tint colour) ----

        /// <summary>Rounded panel with a darker rim, 9-sliced (tint = panel colour).</summary>
        public static Sprite Panel => Cached("panel", () => Shape(96, 30, rim: 7, lip: 0));

        /// <summary>Chunky pill with a darker bottom "lip", 9-sliced (tint = button colour).</summary>
        public static Sprite Button => Cached("button", () => Shape(96, 30, rim: 0, lip: 9));

        static Sprite Shape(int size, int radius, int rim, int lip)
        {
            var tex = NewTexture(size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                float shade = 1f;
                if (rim > 0 && radius - d < rim) shade = 0.84f;                       // darker rim
                if (lip > 0 && y < lip) shade = 0.74f;                                // bottom lip (3D edge)
                if (lip > 0 && y >= size - radius / 2 && radius - d > 3) shade = 1f;  // keep top bright
                px[y * size + x] = new Color(shade, shade, shade, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            int b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                                 new Vector4(b, Mathf.Max(b, lip + 4), b, b));
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

        // ---- Icons ----

        public static Sprite Icon(string name) => Cached("icon:" + name, () => Paint(name));

        /// <summary>Icon names by subject, used by <see cref="SubjectDecor"/>. Glyph strings (e.g. "×") are drawn as text instead.</summary>
        public static readonly Dictionary<Subject, string[]> Symbols = new Dictionary<Subject, string[]>
        {
            [Subject.Science] = new[] { "dna", "beaker", "flask", "atom", "testtube", "dna", "flask" },
            [Subject.Math] = new[] { "+", "−", "×", "÷", "=", "π", "√", "Σ", "%", "7", "3", "x²" },
            [Subject.History] = new[] { "book", "scroll", "column", "hourglass", "quill", "book" },
            [Subject.Language] = new[] { "Aa", "“ ”", "book", "quill", "abc", "?!" },
            [Subject.Geography] = new[] { "globe", "compass", "mountain", "globe" },
            [Subject.ComputerScience] = new[] { "</>", "01", "gear", "{ }", "if", "gear" },
            [Subject.Art] = new[] { "palette", "brush", "palette", "star" },
            [Subject.Music] = new[] { "note", "note2", "note", "♯" },
            [Subject.General] = new[] { "circle", "triangle", "square", "star" },
        };

        public static bool IsGlyph(string symbol) => !Painters.ContainsKey(symbol);

        static Sprite Paint(string name)
        {
            const int n = 128;
            var p = new Painter(n);
            if (Painters.TryGetValue(name, out Action<Painter> draw)) draw(p);
            var tex = NewTexture(n);
            tex.SetPixels(p.ToColors());
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        const float W = 0.055f;   // default stroke width

        static readonly Dictionary<string, Action<Painter>> Painters = new Dictionary<string, Action<Painter>>
        {
            // Menu
            ["play"] = p => p.Poly(0.32f, 0.2f, 0.32f, 0.8f, 0.8f, 0.5f),
            ["import"] = p => { p.Line(0.5f, 0.85f, 0.5f, 0.38f, 0.1f); p.Poly(0.28f, 0.46f, 0.72f, 0.46f, 0.5f, 0.22f);
                                p.Line(0.15f, 0.3f, 0.15f, 0.12f, 0.08f); p.Line(0.15f, 0.12f, 0.85f, 0.12f, 0.08f); p.Line(0.85f, 0.12f, 0.85f, 0.3f, 0.08f); },
            ["folders"] = p => { p.RoundRect(0.12f, 0.15f, 0.72f, 0.62f, 0.06f); p.RoundRect(0.28f, 0.32f, 0.88f, 0.8f, 0.06f, erase: true);
                                 p.RoundRect(0.32f, 0.36f, 0.84f, 0.76f, 0.05f); },
            ["gear"] = p => { p.Circle(0.5f, 0.5f, 0.3f); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4;
                              p.Line(0.5f, 0.5f, 0.5f + Mathf.Cos(a) * 0.42f, 0.5f + Mathf.Sin(a) * 0.42f, 0.14f); } p.Circle(0.5f, 0.5f, 0.12f, erase: true); },
            ["help"] = p => { p.Ring(0.5f, 0.62f, 0.2f, 0.09f); p.Rect(0.2f, 0.4f, 0.5f, 0.62f, erase: true); p.Line(0.66f, 0.55f, 0.5f, 0.42f, 0.09f);
                              p.Line(0.5f, 0.42f, 0.5f, 0.32f, 0.09f); p.Circle(0.5f, 0.17f, 0.065f); },
            ["close"] = p => { p.Line(0.25f, 0.25f, 0.75f, 0.75f, 0.12f); p.Line(0.25f, 0.75f, 0.75f, 0.25f, 0.12f); },
            ["plus"] = p => { p.Line(0.5f, 0.2f, 0.5f, 0.8f, 0.13f); p.Line(0.2f, 0.5f, 0.8f, 0.5f, 0.13f); },
            ["sparkle"] = p => { p.Poly(0.5f, 0.95f, 0.6f, 0.6f, 0.95f, 0.5f, 0.6f, 0.4f, 0.5f, 0.05f, 0.4f, 0.4f, 0.05f, 0.5f, 0.4f, 0.6f); },
            ["trash"] = p => { p.Line(0.2f, 0.78f, 0.8f, 0.78f, 0.08f); p.Line(0.4f, 0.86f, 0.6f, 0.86f, 0.08f);
                               p.Poly(0.27f, 0.72f, 0.73f, 0.72f, 0.67f, 0.12f, 0.33f, 0.12f); },
            ["check"] = p => { p.Line(0.2f, 0.5f, 0.42f, 0.28f, 0.12f); p.Line(0.42f, 0.28f, 0.82f, 0.75f, 0.12f); },
            // Science
            ["beaker"] = p => { p.Line(0.28f, 0.85f, 0.28f, 0.18f, W); p.Line(0.28f, 0.18f, 0.72f, 0.18f, W); p.Line(0.72f, 0.18f, 0.72f, 0.85f, W);
                                p.Line(0.2f, 0.85f, 0.28f, 0.85f, W); p.Rect(0.31f, 0.21f, 0.69f, 0.5f); p.Line(0.72f, 0.65f, 0.6f, 0.65f, 0.035f); },
            ["flask"] = p => { p.Line(0.42f, 0.9f, 0.42f, 0.62f, W); p.Line(0.58f, 0.9f, 0.58f, 0.62f, W); p.Line(0.42f, 0.62f, 0.16f, 0.14f, W);
                               p.Line(0.58f, 0.62f, 0.84f, 0.14f, W); p.Line(0.16f, 0.14f, 0.84f, 0.14f, W); p.Poly(0.22f, 0.17f, 0.78f, 0.17f, 0.66f, 0.4f, 0.34f, 0.4f);
                               p.Circle(0.45f, 0.5f, 0.035f); p.Circle(0.55f, 0.56f, 0.025f); },
            ["testtube"] = p => { p.Line(0.36f, 0.9f, 0.36f, 0.25f, W); p.Line(0.64f, 0.9f, 0.64f, 0.25f, W); p.Ring(0.5f, 0.25f, 0.14f, W, bottomHalf: true);
                                  p.Rect(0.39f, 0.25f, 0.61f, 0.55f); p.Circle(0.5f, 0.26f, 0.11f); },
            ["atom"] = p => { p.Circle(0.5f, 0.5f, 0.08f); for (int i = 0; i < 3; i++) p.Ellipse(0.5f, 0.5f, 0.42f, 0.15f, i * 60f, 0.04f); },
            ["dna"] = p => { for (int k = 0; k < 2; k++) { float ph = k * Mathf.PI; Vector2 prev = default;
                                 for (int i = 0; i <= 24; i++) { float t = i / 24f; var v = new Vector2(0.5f + 0.24f * Mathf.Sin(t * Mathf.PI * 2.2f + ph), 0.08f + t * 0.84f);
                                     if (i > 0) p.Line(prev.x, prev.y, v.x, v.y, W); prev = v; } }
                             for (int i = 1; i < 8; i++) { float t = i / 8f; float s = Mathf.Sin(t * Mathf.PI * 2.2f);
                                 p.Line(0.5f + 0.24f * s, 0.08f + t * 0.84f, 0.5f - 0.24f * s, 0.08f + t * 0.84f, 0.03f); } },
            // History
            ["book"] = p => { p.Poly(0.08f, 0.26f, 0.48f, 0.18f, 0.48f, 0.78f, 0.08f, 0.84f); p.Poly(0.52f, 0.18f, 0.92f, 0.26f, 0.92f, 0.84f, 0.52f, 0.78f);
                              for (int i = 0; i < 4; i++) { float y = 0.66f - i * 0.12f; p.Line(0.15f, y + 0.02f, 0.42f, y - 0.02f, 0.025f, erase: true);
                                  p.Line(0.58f, y - 0.02f, 0.85f, y + 0.02f, 0.025f, erase: true); } },
            ["scroll"] = p => { p.Rect(0.24f, 0.2f, 0.76f, 0.8f); p.Circle(0.24f, 0.8f, 0.08f); p.Circle(0.76f, 0.2f, 0.08f); p.Rect(0.16f, 0.72f, 0.84f, 0.88f);
                                for (int i = 0; i < 4; i++) p.Line(0.32f, 0.62f - i * 0.1f, 0.68f, 0.62f - i * 0.1f, 0.025f, erase: true); },
            ["column"] = p => { p.Rect(0.18f, 0.8f, 0.82f, 0.9f); p.Rect(0.24f, 0.74f, 0.76f, 0.8f); p.Rect(0.3f, 0.18f, 0.7f, 0.74f);
                                for (int i = 1; i < 4; i++) p.Line(0.3f + i * 0.1f, 0.2f, 0.3f + i * 0.1f, 0.72f, 0.02f, erase: true); p.Rect(0.2f, 0.08f, 0.8f, 0.18f); },
            ["hourglass"] = p => { p.Rect(0.2f, 0.84f, 0.8f, 0.92f); p.Rect(0.2f, 0.08f, 0.8f, 0.16f); p.Line(0.28f, 0.84f, 0.5f, 0.5f, W); p.Line(0.72f, 0.84f, 0.5f, 0.5f, W);
                                   p.Line(0.28f, 0.16f, 0.5f, 0.5f, W); p.Line(0.72f, 0.16f, 0.5f, 0.5f, W); p.Poly(0.35f, 0.19f, 0.65f, 0.19f, 0.5f, 0.38f); },
            ["quill"] = p => { p.Poly(0.2f, 0.12f, 0.35f, 0.3f, 0.85f, 0.92f, 0.6f, 0.45f); p.Line(0.12f, 0.06f, 0.3f, 0.26f, 0.035f); },
            // Geography
            ["globe"] = p => { p.Ring(0.5f, 0.5f, 0.38f, W); p.Ellipse(0.5f, 0.5f, 0.16f, 0.38f, 0, 0.04f); p.Line(0.12f, 0.5f, 0.88f, 0.5f, 0.04f);
                               p.Line(0.19f, 0.7f, 0.81f, 0.7f, 0.035f); p.Line(0.19f, 0.3f, 0.81f, 0.3f, 0.035f); },
            ["compass"] = p => { p.Ring(0.5f, 0.5f, 0.4f, W); p.Poly(0.5f, 0.85f, 0.6f, 0.5f, 0.5f, 0.15f, 0.4f, 0.5f); p.Circle(0.5f, 0.5f, 0.05f, erase: true); },
            ["mountain"] = p => { p.Poly(0.05f, 0.15f, 0.4f, 0.75f, 0.62f, 0.4f, 0.72f, 0.55f, 0.95f, 0.15f); p.Poly(true, 0.33f, 0.63f, 0.4f, 0.75f, 0.47f, 0.63f); },
            // Art & music
            ["palette"] = p => { p.Circle(0.5f, 0.5f, 0.4f); p.Circle(0.66f, 0.3f, 0.1f, erase: true); foreach (var d in new[] { (0.35f, 0.66f), (0.52f, 0.72f), (0.68f, 0.62f), (0.3f, 0.44f) }) p.Circle(d.Item1, d.Item2, 0.06f, erase: true); },
            ["brush"] = p => { p.Line(0.8f, 0.9f, 0.45f, 0.45f, 0.08f); p.Poly(0.45f, 0.45f, 0.35f, 0.55f, 0.12f, 0.12f); },
            ["note"] = p => { p.Circle(0.35f, 0.25f, 0.13f); p.Line(0.47f, 0.25f, 0.47f, 0.85f, 0.06f); p.Line(0.47f, 0.85f, 0.7f, 0.68f, 0.07f); },
            ["note2"] = p => { p.Circle(0.28f, 0.25f, 0.11f); p.Circle(0.7f, 0.33f, 0.11f); p.Line(0.38f, 0.25f, 0.38f, 0.8f, 0.05f);
                               p.Line(0.8f, 0.33f, 0.8f, 0.88f, 0.05f); p.Line(0.38f, 0.8f, 0.8f, 0.88f, 0.1f); },
            // General shapes
            ["circle"] = p => p.Ring(0.5f, 0.5f, 0.35f, 0.08f),
            ["triangle"] = p => { p.Line(0.5f, 0.85f, 0.15f, 0.2f, 0.08f); p.Line(0.15f, 0.2f, 0.85f, 0.2f, 0.08f); p.Line(0.85f, 0.2f, 0.5f, 0.85f, 0.08f); },
            ["square"] = p => { p.Rect(0.18f, 0.18f, 0.82f, 0.82f); p.Rect(0.26f, 0.26f, 0.74f, 0.74f, erase: true); },
            ["star"] = p => { var pts = new List<float>(); for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5; float r = i % 2 == 0 ? 0.42f : 0.18f;
                              pts.Add(0.5f + Mathf.Cos(a) * r); pts.Add(0.5f + Mathf.Sin(a) * r); } p.Poly(pts.ToArray()); },
        };

        /// <summary>Tiny signed-distance rasteriser: shapes add (max) or erase coverage, with 1-pixel anti-aliasing.</summary>
        sealed class Painter
        {
            readonly int _n;
            readonly float[] _a;
            public Painter(int n) { _n = n; _a = new float[n * n]; }

            void Apply(Func<float, float, float> signedDistance, bool erase)
            {
                float px = 1f / _n;
                for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float d = signedDistance((x + 0.5f) * px, (y + 0.5f) * px);   // < 0 inside
                    float c = Mathf.Clamp01(0.5f - d / px);
                    int i = y * _n + x;
                    _a[i] = erase ? Mathf.Min(_a[i], 1f - c) : Mathf.Max(_a[i], c);
                }
            }

            public void Circle(float cx, float cy, float r, bool erase = false) =>
                Apply((x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r, erase);

            public void Ring(float cx, float cy, float r, float w, bool erase = false, bool bottomHalf = false) =>
                Apply((x, y) => bottomHalf && y > cy ? 1f : Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) - w / 2, erase);

            public void Line(float ax, float ay, float bx, float by, float w, bool erase = false) =>
                Apply((x, y) =>
                {
                    Vector2 pa = new Vector2(x - ax, y - ay), ba = new Vector2(bx - ax, by - ay);
                    float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(ba.sqrMagnitude, 1e-6f));
                    return (pa - ba * h).magnitude - w / 2;
                }, erase);

            public void Rect(float x0, float y0, float x1, float y1, bool erase = false) => RoundRect(x0, y0, x1, y1, 0, erase);

            public void RoundRect(float x0, float y0, float x1, float y1, float r, bool erase = false) =>
                Apply((x, y) =>
                {
                    float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = (x1 - x0) / 2 - r, hy = (y1 - y0) / 2 - r;
                    float dx = Mathf.Abs(x - cx) - hx, dy = Mathf.Abs(y - cy) - hy;
                    return new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0) - r;
                }, erase);

            public void Ellipse(float cx, float cy, float rx, float ry, float degrees, float w)
            {
                float a = degrees * Mathf.Deg2Rad;
                Vector2 prev = default;
                for (int i = 0; i <= 48; i++)
                {
                    float t = i * Mathf.PI * 2 / 48;
                    Vector2 e = new Vector2(Mathf.Cos(t) * rx, Mathf.Sin(t) * ry);
                    var v = new Vector2(cx + e.x * Mathf.Cos(a) - e.y * Mathf.Sin(a), cy + e.x * Mathf.Sin(a) + e.y * Mathf.Cos(a));
                    if (i > 0) Line(prev.x, prev.y, v.x, v.y, w);
                    prev = v;
                }
            }

            /// <summary>Filled polygon from x,y pairs.</summary>
            public void Poly(params float[] xy) => Poly(false, xy);

            public void Poly(bool erase, params float[] xy)
            {
                int n = xy.Length / 2;
                Apply((x, y) =>
                {
                    float d = float.MaxValue;
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                        Vector2 e = new Vector2(xj - xi, yj - yi), w = new Vector2(x - xi, y - yi);
                        Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Mathf.Max(e.sqrMagnitude, 1e-6f));
                        d = Mathf.Min(d, b.magnitude);
                        if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    return inside ? -d : d;
                }, erase);
            }

            public Color[] ToColors()
            {
                var c = new Color[_a.Length];
                for (int i = 0; i < _a.Length; i++) c[i] = new Color(1, 1, 1, _a[i]);
                return c;
            }
        }
    }
}
