using System.Collections.Generic;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Straight connections between parts for non-circuit exhibits: chemical bonds (stopping at atom
    /// surfaces), arrows and lines. Each link keeps following its parts (e.g. in the exploded view).
    /// Circuit wiring is done by <see cref="PcbBuilder"/> as board traces.
    /// </summary>
    public static class LinkBuilder
    {
        static readonly Color BondColor = new Color(0.75f, 0.75f, 0.78f);
        static readonly Color ArrowColor = new Color(1f, 0.8f, 0.2f);

        public static void Build(SceneLink link, PartLibrary.BuiltPart a, PartLibrary.BuiltPart b, Transform content)
        {
            string kind = (link.Kind ?? "line").ToLowerInvariant();
            var go = new GameObject($"{kind}:{link.From}->{link.To}").transform;
            go.SetParent(content, false);
            var follower = go.gameObject.AddComponent<LinkFollower>();
            follower.Init(a, b, content);

            switch (kind)
            {
                case "bond": follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, BondColor), 0.14f, 0f); break;
                case "double_bond":
                    follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, BondColor), 0.09f, 0.1f);
                    follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, BondColor), 0.09f, -0.1f);
                    break;
                case "arrow":
                    follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, ArrowColor), 0.08f, 0f, shortenEnd: 0.25f);
                    follower.SetHead(Cone(go, Vector3.zero, Quaternion.identity, Vector3.one * 0.25f, ArrowColor));
                    follower.EnableFlow(new Color(1f, 0.95f, 0.6f));   // particles show the direction of the flow
                    break;
                case "dimension":
                    follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, new Color(1f, 0.85f, 0.2f)), 0.03f, 0f);
                    break;
                case "wire": follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, new Color(0.85f, 0.45f, 0.2f)), 0.08f, 0f); break;
                default: follower.AddRod(Rod(go, Vector3.zero, Vector3.up, 1, new Color(0.9f, 0.9f, 0.9f)), 0.05f, 0f); break;
            }

            if (!string.IsNullOrEmpty(link.Label) && link.Label != a.Data.Label && link.Label != b.Data.Label)
                follower.SetLabel(Label(go, link.Label, Vector3.zero, kind == "dimension" ? 0.3f : 0.24f,
                                        kind == "dimension" ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.9f, 0.6f)).transform);
            follower.Refresh();
        }
    }

    /// <summary>Keeps a straight link's rods (and arrow head/label) between two parts as they move.</summary>
    public sealed class LinkFollower : MonoBehaviour
    {
        PartLibrary.BuiltPart _a, _b;
        Transform _content, _head, _label;
        readonly List<(Transform rod, float diameter, float side, float shortenEnd)> _rods = new List<(Transform, float, float, float)>();
        Vector3 _lastA, _lastB;

        public void Init(PartLibrary.BuiltPart a, PartLibrary.BuiltPart b, Transform content) { _a = a; _b = b; _content = content; }
        public void AddRod(Transform rod, float diameter, float side, float shortenEnd = 0f) => _rods.Add((rod, diameter, side, shortenEnd));
        public void SetHead(Transform head) => _head = head;
        public void SetLabel(Transform label) => _label = label;

        readonly List<Transform> _dots = new List<Transform>();
        Vector3 _p0, _p1;

        /// <summary>Small glowing particles travelling from → to along the link.</summary>
        public void EnableFlow(Color color)
        {
            for (int i = 0; i < 3; i++)
                _dots.Add(Prim(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.13f, color, emission: color * 1.8f, name: "Electron"));
        }

        void LateUpdate()
        {
            if (_a?.Root == null || _b?.Root == null) return;
            if (_a.Root.localPosition != _lastA || _b.Root.localPosition != _lastB) Refresh();
            for (int i = 0; i < _dots.Count; i++)
            {
                float t = Mathf.Repeat(Time.unscaledTime * 0.35f + i / (float)_dots.Count, 1f);
                _dots[i].localPosition = Vector3.Lerp(_p0, _p1, t);
            }
        }

        public void Refresh()
        {
            _lastA = _a.Root.localPosition;
            _lastB = _b.Root.localPosition;
            Vector3 ca = _a.Root.localPosition, cb = _b.Root.localPosition;   // both are children of the content
            Vector3 d = cb - ca;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            Vector3 n = d / len;
            float ra = _a.SurfaceRadius * _a.Root.localScale.x, rb = _b.SurfaceRadius * _b.Root.localScale.x;
            if (ra + rb >= len * 0.9f) { ra = rb = 0; }
            Vector3 p0 = ca + n * ra, p1 = cb - n * rb;
            _p0 = p0;
            _p1 = p1;

            Vector3 side = Vector3.Cross(n, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(n, Vector3.right);
            side.Normalize();

            foreach (var (rod, diameter, offset, shortenEnd) in _rods)
            {
                if (rod == null) continue;
                Vector3 a = p0 + side * offset, b = p1 - n * shortenEnd + side * offset;
                float l = Mathf.Max((b - a).magnitude, 1e-3f);
                rod.localPosition = (a + b) / 2;
                rod.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a) / l);
                rod.localScale = new Vector3(diameter, l / 2, diameter);
            }
            if (_head != null)
            {
                _head.localPosition = p1 - n * 0.12f;
                _head.localRotation = Quaternion.FromToRotation(Vector3.up, n);
            }
            if (_label != null) _label.localPosition = (p0 + p1) / 2 + Vector3.up * 0.4f;
        }
    }

    /// <summary>
    /// Glowing dots travelling along wires/traces to show current. Each wire has its own rate
    /// (content units per second; negative = backwards; 0 = no current, dots hidden).
    /// Uses unscaled time so it keeps moving while the game is paused in the exhibit viewer.
    /// </summary>
    public sealed class CurrentFlow : MonoBehaviour
    {
        const float Spacing = 0.6f;

        sealed class Wire
        {
            public List<Vector3> Path;
            public float Length, Phase, Rate;
            public readonly List<Transform> Dots = new List<Transform>();
        }

        readonly List<Wire> _wires = new List<Wire>();

        public int AddWire(List<Vector3> path, float dotSize = 0.14f)
        {
            var w = new Wire { Path = path };
            for (int i = 0; i < path.Count - 1; i++) w.Length += Vector3.Distance(path[i], path[i + 1]);
            int count = Mathf.Max(1, Mathf.RoundToInt(w.Length / Spacing));
            Color glow = new Color(0.4f, 0.9f, 1f);
            for (int i = 0; i < count && w.Length > 0.05f; i++)
            {
                Transform dot = Prim(PrimitiveType.Sphere, transform, path[0], Vector3.one * dotSize, glow, emission: glow * 2f, name: "Electron");
                dot.gameObject.SetActive(false);
                w.Dots.Add(dot);
            }
            _wires.Add(w);
            return _wires.Count - 1;
        }

        public void SetRate(int wire, float rate)
        {
            Wire w = _wires[wire];
            w.Rate = rate;
            foreach (Transform d in w.Dots) d.gameObject.SetActive(Mathf.Abs(rate) > 1e-4f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (Wire w in _wires)
            {
                if (Mathf.Abs(w.Rate) < 1e-4f || w.Dots.Count == 0) continue;
                w.Phase = Mathf.Repeat(w.Phase + w.Rate * dt, w.Length);
                for (int i = 0; i < w.Dots.Count; i++)
                    w.Dots[i].localPosition = PointAt(w.Path, Mathf.Repeat(w.Phase + i * w.Length / w.Dots.Count, w.Length));
            }
        }

        static Vector3 PointAt(List<Vector3> path, float distance)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                float seg = Vector3.Distance(path[i], path[i + 1]);
                if (distance <= seg) return Vector3.Lerp(path[i], path[i + 1], seg > 0 ? distance / seg : 0);
                distance -= seg;
            }
            return path[path.Count - 1];
        }
    }
}
