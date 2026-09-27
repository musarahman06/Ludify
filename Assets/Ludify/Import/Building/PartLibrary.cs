using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Builds one <see cref="ScenePart"/> out of primitives, in a ~1×1×1 unit box around its origin
    /// (the part root is then scaled by <see cref="ScenePart.Size"/>). Two-terminal components run
    /// along local X with terminals at x = ±0.5 so wires can attach to their ends.
    /// </summary>
    public static class PartLibrary
    {
        static readonly Color Lead = new Color(0.72f, 0.72f, 0.75f);
        static readonly Color Dark = new Color(0.15f, 0.15f, 0.17f);
        static readonly Quaternion AlongX = Quaternion.Euler(0, 0, 90);

        public sealed class BuiltPart
        {
            public ScenePart Data;
            public Transform Root;
            /// <summary>Connection points in the part root's local space.</summary>
            public Vector3[] Terminals;
            /// <summary>How far (local units) straight links should stop short of the centre, e.g. atom radius.</summary>
            public float SurfaceRadius;
        }

        /// <param name="floatingLabel">Add a camera-facing name label above the part (off for PCBs, which print labels on the board).</param>
        public static BuiltPart Build(ScenePart p, Transform parent, bool floatingLabel = true)
        {
            var root = new GameObject($"{p.Kind}:{p.Id}").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(p.X, p.Y, p.Z);
            root.localRotation = Quaternion.Euler(0, p.RotationY, 0);
            root.localScale = Vector3.one * Mathf.Clamp(p.Size <= 0 ? 1 : p.Size, 0.2f, p.Kind == "shell" ? 14f : 5f);

            var built = new BuiltPart { Data = p, Root = root, Terminals = new[] { Vector3.zero }, SurfaceRadius = 0.3f };
            Color c = ParseColor(p.Color, DefaultColor(p.Kind));
            string kind = (p.Kind ?? "box").ToLowerInvariant();

            switch (kind)
            {
                case "battery": Battery(root, c); TwoTerminal(built); break;
                case "resistor": Resistor(root, p.Value); TwoTerminal(built); break;
                case "bulb": Bulb(root); built.Terminals = new[] { new Vector3(-0.35f, 0.08f, 0), new Vector3(0.35f, 0.08f, 0) }; break;
                case "switch": Switch(root); TwoTerminal(built); break;
                case "capacitor": Capacitor(root); TwoTerminal(built); break;
                case "led": Led(root, c); TwoTerminal(built); break;
                case "meter": Meter(root, p.Value); TwoTerminal(built); break;
                case "ground": Ground(root); built.Terminals = new[] { new Vector3(0, 0.45f, 0) }; break;
                case "node": Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.18f, Dark); built.SurfaceRadius = 0.05f; break;
                case "atom": built.SurfaceRadius = Atom(root, p.Value, p.Color); break;
                case "star": Star(root, c); built.SurfaceRadius = 0.5f; break;
                case "planet": Planet(root, c, p.Value); built.SurfaceRadius = 0.5f; break;
                case "moon": Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, c); built.SurfaceRadius = 0.5f; break;
                case "shell": Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, c).GetComponent<Renderer>().sharedMaterial =
                                  ModelKit.Glass(new Color(c.r, c.g, c.b, 0.22f)); built.SurfaceRadius = 0.5f; break;
                case "gear": Gear(root, c, p.Value); built.SurfaceRadius = 0.5f; break;
                case "bar": Bar(root, c, p.Height > 0 ? p.Height : 1f); break;
                case "pyramid": MeshObject("Pyramid", PyramidMesh, root, Vector3.zero, Quaternion.identity, Vector3.one, Mat(c)); break;
                case "prism": MeshObject("Prism", PrismMesh, root, Vector3.zero, Quaternion.identity, Vector3.one, Mat(c)); break;
                case "sphere": Prim(PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, c); built.SurfaceRadius = 0.5f; break;
                case "cylinder": Prim(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1, 0.5f, 1), c); break;
                case "cone": Cone(root, Vector3.zero, Quaternion.identity, Vector3.one, c); break;
                case "arrow": Arrow(root, c); break;
                case "panel": Panel(root, c, p.Value); break;
                case "label": break;                                     // text only (added below)
                default: Prim(PrimitiveType.Cube, root, Vector3.zero, Vector3.one, c); built.SurfaceRadius = 0.5f; break;
            }

            string text = LabelText(p, kind);
            if (floatingLabel && !string.IsNullOrEmpty(text))
            {
                float above = kind == "label" ? 0f
                    : kind == "atom" ? built.SurfaceRadius + 0.35f
                    : kind == "bar" ? (p.Height > 0 ? p.Height : 1f) + 0.35f
                    : kind == "shell" ? 0.62f
                    : 0.85f;
                Label(root, text, new Vector3(0, above, 0), kind == "label" ? 0.45f : 0.28f);
            }
            return built;
        }

        static void TwoTerminal(BuiltPart b) => b.Terminals = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0) };

        internal static string LabelText(ScenePart p, string kind)
        {
            string label = p.Label?.Trim();
            string value = p.Value?.Trim();
            if (kind == "atom") return string.IsNullOrEmpty(label) ? value : label;
            // Space bodies: just the name (their value is a style hint like "ringed").
            if (kind == "planet" || kind == "star" || kind == "moon")
                return !string.IsNullOrEmpty(label) ? label
                     : string.IsNullOrEmpty(p.Id) ? null : char.ToUpperInvariant(p.Id[0]) + p.Id.Substring(1);
            if (kind == "panel") return label;
            if (string.IsNullOrEmpty(value) || kind == "meter" || value == label) return label;
            return string.IsNullOrEmpty(label) ? value : $"{label}  {value}";
        }

        static Color DefaultColor(string kind)
        {
            switch ((kind ?? "").ToLowerInvariant())
            {
                case "battery": return new Color(0.2f, 0.2f, 0.22f);
                case "led": return new Color(1f, 0.15f, 0.1f);
                case "arrow": return new Color(1f, 0.8f, 0.2f);
                case "panel": return new Color(0.9f, 0.9f, 0.85f);
                case "star": return new Color(1f, 0.78f, 0.25f);
                case "planet": return new Color(0.35f, 0.55f, 0.9f);
                case "moon": return new Color(0.7f, 0.7f, 0.72f);
                case "shell": return new Color(0.55f, 0.85f, 0.65f);
                case "gear": return new Color(0.7f, 0.72f, 0.76f);
                case "bar": return new Color(0.3f, 0.6f, 0.95f);
                default: return new Color(0.55f, 0.7f, 0.95f);
            }
        }

        // ---- Circuit components ----

        static void Battery(Transform r, Color body)
        {
            Prim(PrimitiveType.Cylinder, r, new Vector3(-0.05f, 0, 0), new Vector3(0.4f, 0.36f, 0.4f), body, AlongX);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0.33f, 0, 0), new Vector3(0.41f, 0.05f, 0.41f), new Color(0.85f, 0.55f, 0.2f), AlongX); // copper top
            Prim(PrimitiveType.Cylinder, r, new Vector3(0.43f, 0, 0), new Vector3(0.12f, 0.05f, 0.12f), Lead, AlongX);                     // + nub
            Label(r, "+", new Vector3(0.3f, 0.32f, 0), 0.22f, new Color(1f, 0.4f, 0.3f));
            Label(r, "−", new Vector3(-0.45f, 0.32f, 0), 0.22f, new Color(0.5f, 0.7f, 1f));
        }

        static readonly Color[] BandColors =
        {
            Color.black, new Color(0.45f, 0.25f, 0.1f), Color.red, new Color(1f, 0.55f, 0f), Color.yellow,
            Color.green, Color.blue, new Color(0.55f, 0.2f, 0.8f), Color.gray, Color.white,
        };

        static void Resistor(Transform r, string value)
        {
            Rod(r, new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), 0.05f, Lead);
            Prim(PrimitiveType.Capsule, r, Vector3.zero, new Vector3(0.24f, 0.32f, 0.24f), new Color(0.85f, 0.75f, 0.55f), AlongX);
            // Colour bands from the value's first two digits and multiplier (e.g. 220Ω → red red brown).
            int[] bands = ResistorBands(value);
            for (int i = 0; i < bands.Length; i++)
                Prim(PrimitiveType.Cylinder, r, new Vector3(-0.16f + i * 0.1f, 0, 0), new Vector3(0.25f, 0.02f, 0.25f), BandColors[bands[i]], AlongX);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0.2f, 0, 0), new Vector3(0.25f, 0.02f, 0.25f), new Color(0.85f, 0.7f, 0.2f), AlongX); // gold tolerance
        }

        static int[] ResistorBands(string value)
        {
            if (string.IsNullOrEmpty(value)) return new[] { 1, 0, 2 };
            string v = value.ToLowerInvariant().Replace("ω", "").Replace("ohm", "").Replace("s", "").Trim();
            float mult = 1;
            if (v.EndsWith("k")) { mult = 1e3f; v = v.TrimEnd('k'); }
            else if (v.EndsWith("m")) { mult = 1e6f; v = v.TrimEnd('m'); }
            if (!float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ohms) || ohms <= 0)
                return new[] { 1, 0, 2 };
            ohms *= mult;
            int exp = Mathf.Max(0, Mathf.FloorToInt(Mathf.Log10(ohms)) - 1);
            int digits = Mathf.Clamp(Mathf.RoundToInt(ohms / Mathf.Pow(10, exp)), 10, 99);
            return new[] { digits / 10, digits % 10, Mathf.Min(exp, 9) };
        }

        static void Bulb(Transform r)
        {
            Rod(r, new Vector3(-0.35f, 0.08f, 0), new Vector3(-0.1f, 0.08f, 0), 0.05f, Lead);
            Rod(r, new Vector3(0.1f, 0.08f, 0), new Vector3(0.35f, 0.08f, 0), 0.05f, Lead);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0, 0.1f, 0), new Vector3(0.24f, 0.1f, 0.24f), Lead);       // screw base
            Prim(PrimitiveType.Sphere, r, new Vector3(0, 0.42f, 0), Vector3.one * 0.5f, new Color(1f, 0.95f, 0.6f),
                 emission: new Color(1f, 0.8f, 0.3f) * 1.5f, name: "Glass");
        }

        static void Switch(Transform r)
        {
            Prim(PrimitiveType.Cube, r, new Vector3(0, -0.06f, 0), new Vector3(1f, 0.08f, 0.35f), Dark);
            Prim(PrimitiveType.Cylinder, r, new Vector3(-0.3f, 0.02f, 0), new Vector3(0.12f, 0.04f, 0.12f), Lead);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0.3f, 0.02f, 0), new Vector3(0.12f, 0.04f, 0.12f), Lead);
            // Lever hinged at the left contact, lifted open.
            Prim(PrimitiveType.Cube, r, new Vector3(0.0f, 0.16f, 0), new Vector3(0.65f, 0.05f, 0.08f), new Color(0.85f, 0.2f, 0.2f),
                 Quaternion.Euler(0, 0, 25), name: "Lever");
        }

        static void Capacitor(Transform r)
        {
            Rod(r, new Vector3(-0.5f, 0, 0), new Vector3(-0.08f, 0, 0), 0.05f, Lead);
            Rod(r, new Vector3(0.08f, 0, 0), new Vector3(0.5f, 0, 0), 0.05f, Lead);
            Prim(PrimitiveType.Cube, r, new Vector3(-0.08f, 0, 0), new Vector3(0.05f, 0.55f, 0.45f), new Color(0.3f, 0.45f, 0.8f));
            Prim(PrimitiveType.Cube, r, new Vector3(0.08f, 0, 0), new Vector3(0.05f, 0.55f, 0.45f), new Color(0.3f, 0.45f, 0.8f));
        }

        static void Led(Transform r, Color c)
        {
            Rod(r, new Vector3(-0.5f, 0, 0), new Vector3(-0.08f, 0, 0), 0.04f, Lead);
            Rod(r, new Vector3(0.08f, 0, 0), new Vector3(0.5f, 0, 0), 0.04f, Lead);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0, 0.05f, 0), new Vector3(0.26f, 0.08f, 0.26f), c, emission: c * 1.2f);
            Prim(PrimitiveType.Sphere, r, new Vector3(0, 0.16f, 0), Vector3.one * 0.26f, c, emission: c * 1.6f, name: "Glass");
        }

        static void Meter(Transform r, string value)
        {
            Rod(r, new Vector3(-0.5f, 0, 0), new Vector3(-0.3f, 0, 0), 0.05f, Lead);
            Rod(r, new Vector3(0.3f, 0, 0), new Vector3(0.5f, 0, 0), 0.05f, Lead);
            Prim(PrimitiveType.Cylinder, r, Vector3.zero, new Vector3(0.62f, 0.12f, 0.62f), Dark);
            Prim(PrimitiveType.Cylinder, r, new Vector3(0, 0.12f, 0), new Vector3(0.52f, 0.01f, 0.52f), Color.white);
            string symbol = string.IsNullOrEmpty(value) ? "A" : value.Trim().Substring(0, 1).ToUpperInvariant();
            Label(r, symbol, new Vector3(0, 0.35f, 0), 0.4f, new Color(0.2f, 0.5f, 1f));
        }

        static void Ground(Transform r)
        {
            Rod(r, new Vector3(0, 0.45f, 0), new Vector3(0, 0.05f, 0), 0.05f, Lead);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cube, r, new Vector3(0, 0.02f - i * 0.12f, 0), new Vector3(0.6f - i * 0.2f, 0.05f, 0.1f), Dark);
        }

        // ---- Chemistry ----

        static readonly Dictionary<string, (Color color, float radius)> Elements = new Dictionary<string, (Color, float)>
        {
            ["H"] = (Color.white, 0.32f), ["C"] = (new Color(0.2f, 0.2f, 0.2f), 0.5f), ["N"] = (new Color(0.2f, 0.35f, 1f), 0.48f),
            ["O"] = (new Color(0.95f, 0.15f, 0.1f), 0.47f), ["S"] = (new Color(1f, 0.85f, 0.2f), 0.58f), ["P"] = (new Color(1f, 0.5f, 0f), 0.56f),
            ["Cl"] = (new Color(0.2f, 0.9f, 0.2f), 0.56f), ["F"] = (new Color(0.55f, 0.9f, 0.3f), 0.42f), ["Na"] = (new Color(0.6f, 0.35f, 0.9f), 0.62f),
            ["Fe"] = (new Color(0.85f, 0.45f, 0.2f), 0.6f), ["Ca"] = (new Color(0.4f, 0.8f, 0.3f), 0.64f), ["Mg"] = (new Color(0.3f, 0.7f, 0.3f), 0.6f),
        };

        static float Atom(Transform r, string symbol, string colorOverride)
        {
            string key = string.IsNullOrEmpty(symbol) ? "C" : symbol.Trim();
            if (key.Length > 0) key = char.ToUpperInvariant(key[0]) + (key.Length > 1 ? key.Substring(1).ToLowerInvariant() : "");
            (Color color, float radius) = Elements.TryGetValue(key, out var e) ? e : (new Color(1f, 0.55f, 0.75f), 0.52f);
            color = ParseColor(colorOverride, color);
            Prim(PrimitiveType.Sphere, r, Vector3.zero, Vector3.one * radius * 2, color);
            return radius;
        }

        // ---- Space ----

        static void Star(Transform r, Color c) =>
            Prim(PrimitiveType.Sphere, r, Vector3.zero, Vector3.one, c, emission: c * 2.2f, name: "Glass");

        static void Planet(Transform r, Color c, string value)
        {
            Prim(PrimitiveType.Sphere, r, Vector3.zero, Vector3.one, c);
            if (!string.IsNullOrEmpty(value) && value.ToLowerInvariant().Contains("ring"))
                MeshObject("Rings", RingMesh(0.62f), r, Vector3.zero, Quaternion.Euler(22, 0, 8), Vector3.one * 0.95f,
                           ModelKit.Glass(new Color(0.9f, 0.8f, 0.6f, 0.7f)));
        }

        // ---- Mechanics ----

        /// <summary>Toothed gear facing the viewer (axle along Z). Teeth count from the value (default 12).</summary>
        static void Gear(Transform r, Color c, string value)
        {
            int teeth = GearTeeth(value);
            var wheel = new GameObject("Wheel").transform;          // spun by GearTrain
            wheel.SetParent(r, false);
            Quaternion axleZ = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cylinder, wheel, Vector3.zero, new Vector3(0.84f, 0.08f, 0.84f), c, axleZ);
            float toothW = Mathf.Clamp(2.4f / teeth, 0.06f, 0.2f);
            for (int i = 0; i < teeth; i++)
            {
                float a = i * 360f / teeth;
                Quaternion rot = Quaternion.Euler(0, 0, a);
                Prim(PrimitiveType.Cube, wheel, rot * new Vector3(0, 0.46f, 0), new Vector3(toothW, 0.14f, 0.16f), c, rot);
            }
            Prim(PrimitiveType.Cylinder, wheel, new Vector3(0, 0, -0.05f), new Vector3(0.22f, 0.1f, 0.22f), Dark, axleZ);   // hub
            Prim(PrimitiveType.Cube, wheel, new Vector3(0, 0.25f, -0.09f), new Vector3(0.05f, 0.22f, 0.02f), new Color(0.9f, 0.3f, 0.2f)); // marker so rotation is visible
        }

        public static int GearTeeth(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                string digits = new string(value.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int n)) return Mathf.Clamp(n, 6, 48);
            }
            return 12;
        }

        // ---- Data ----

        static void Bar(Transform r, Color c, float height) =>
            Prim(PrimitiveType.Cube, r, new Vector3(0, height / 2, 0), new Vector3(0.8f, height, 0.8f), c);

        // ---- Generic ----

        static void Arrow(Transform r, Color c)
        {
            Rod(r, new Vector3(-0.5f, 0, 0), new Vector3(0.2f, 0, 0), 0.12f, c);
            Cone(r, new Vector3(0.35f, 0, 0), Quaternion.Euler(0, 0, -90), new Vector3(0.3f, 0.3f, 0.3f), c);
        }

        static void Panel(Transform r, Color c, string text)
        {
            Prim(PrimitiveType.Cube, r, Vector3.zero, new Vector3(1.2f, 0.8f, 0.06f), c);
            if (!string.IsNullOrEmpty(text))
            {
                var t = Label(r, text, new Vector3(0, 0, -0.05f), 0.16f, Color.black);
                t.outlineWidth = 0;
                t.textWrappingMode = TMPro.TextWrappingModes.Normal;
                t.rectTransform.sizeDelta = new Vector2(7, 5);
                Kill(t.GetComponent<Billboard>());
                t.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
