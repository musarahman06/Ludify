using System.Collections.Generic;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Connections between parts: circuit wires (routed at right angles between component terminals),
    /// chemical bonds (straight, stopping at atom surfaces), arrows and plain lines.
    /// </summary>
    public static class LinkBuilder
    {
        static readonly Color Copper = new Color(0.85f, 0.45f, 0.2f);
        static readonly Color BondColor = new Color(0.75f, 0.75f, 0.78f);
        const float WireDiameter = 0.1f;

        /// <summary>Builds the link and returns its polyline (content-local) if it carries current.</summary>
        public static List<Vector3> Build(SceneLink link, PartLibrary.BuiltPart a, PartLibrary.BuiltPart b, Transform content)
        {
            string kind = (link.Kind ?? "line").ToLowerInvariant();
            var go = new GameObject($"{kind}:{link.From}->{link.To}").transform;
            go.SetParent(content, false);
            List<Vector3> wirePath = null;

            switch (kind)
            {
                case "wire":
                    wirePath = WirePath(a, b, content);
                    for (int i = 0; i < wirePath.Count - 1; i++)
                    {
                        Rod(go, wirePath[i], wirePath[i + 1], WireDiameter, Copper);
                        if (i > 0) Prim(PrimitiveType.Sphere, go, wirePath[i], Vector3.one * WireDiameter, Copper);
                    }
                    break;
                case "bond":
                case "double_bond":
                {
                    (Vector3 p0, Vector3 p1) = Trimmed(a, b, content);
                    if (kind == "bond") Rod(go, p0, p1, 0.14f, BondColor);
                    else
                    {
                        Vector3 side = Vector3.Cross(p1 - p0, Vector3.up);
                        if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(p1 - p0, Vector3.right);
                        side = side.normalized * 0.1f;
                        Rod(go, p0 + side, p1 + side, 0.09f, BondColor);
                        Rod(go, p0 - side, p1 - side, 0.09f, BondColor);
                    }
                    break;
                }
                case "arrow":
                {
                    (Vector3 p0, Vector3 p1) = Trimmed(a, b, content);
                    Vector3 dir = (p1 - p0).normalized;
                    Rod(go, p0, p1 - dir * 0.25f, 0.08f, new Color(1f, 0.8f, 0.2f));
                    Cone(go, p1 - dir * 0.12f, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one * 0.25f, new Color(1f, 0.8f, 0.2f));
                    break;
                }
                default:
                {
                    (Vector3 p0, Vector3 p1) = Trimmed(a, b, content);
                    Rod(go, p0, p1, 0.05f, new Color(0.9f, 0.9f, 0.9f));
                    break;
                }
            }

            if (!string.IsNullOrEmpty(link.Label) && link.Label != a.Data.Label && link.Label != b.Data.Label)
            {
                Vector3 mid = wirePath != null ? wirePath[wirePath.Count / 2] : (Center(a, content) + Center(b, content)) / 2;
                Label(go, link.Label, mid + Vector3.up * 0.4f, 0.24f, new Color(1f, 0.9f, 0.6f));
            }
            return wirePath;
        }

        static Vector3 Center(PartLibrary.BuiltPart p, Transform content) => content.InverseTransformPoint(p.Root.position);

        static Vector3 TerminalNear(PartLibrary.BuiltPart p, Vector3 target, Transform content)
        {
            Vector3 best = Center(p, content);
            float bestDist = float.MaxValue;
            foreach (Vector3 t in p.Terminals)
            {
                Vector3 world = content.InverseTransformPoint(p.Root.TransformPoint(t));
                float d = (world - target).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = world; }
            }
            return best;
        }

        /// <summary>Right-angle route: along X, then Z, then Y, between the closest terminals.</summary>
        static List<Vector3> WirePath(PartLibrary.BuiltPart a, PartLibrary.BuiltPart b, Transform content)
        {
            Vector3 p0 = TerminalNear(a, Center(b, content), content);
            Vector3 p1 = TerminalNear(b, p0, content);
            var path = new List<Vector3> { p0 };
            void Add(Vector3 v) { if ((v - path[path.Count - 1]).sqrMagnitude > 1e-4f) path.Add(v); }
            Add(new Vector3(p1.x, p0.y, p0.z));
            Add(new Vector3(p1.x, p0.y, p1.z));
            Add(p1);
            if (path.Count == 1) path.Add(p1);
            return path;
        }

        static (Vector3, Vector3) Trimmed(PartLibrary.BuiltPart a, PartLibrary.BuiltPart b, Transform content)
        {
            Vector3 ca = Center(a, content), cb = Center(b, content);
            Vector3 d = cb - ca;
            float len = d.magnitude;
            if (len < 1e-3f) return (ca, cb);
            float ra = a.SurfaceRadius * a.Root.localScale.x, rb = b.SurfaceRadius * b.Root.localScale.x;
            if (ra + rb >= len * 0.9f) return (ca, cb);
            Vector3 n = d / len;
            return (ca + n * ra, cb - n * rb);
        }
    }

    /// <summary>Glowing dots travelling along circuit wires to show current flowing.</summary>
    public sealed class CurrentFlow : MonoBehaviour
    {
        const float Speed = 0.8f;     // content units per second
        const float Spacing = 0.7f;

        readonly List<(List<Vector3> path, float length, List<Transform> dots)> _wires =
            new List<(List<Vector3>, float, List<Transform>)>();

        public void AddWire(List<Vector3> path)
        {
            float length = 0;
            for (int i = 0; i < path.Count - 1; i++) length += Vector3.Distance(path[i], path[i + 1]);
            if (length < 0.05f) return;
            var dots = new List<Transform>();
            int count = Mathf.Max(1, Mathf.RoundToInt(length / Spacing));
            Color glow = new Color(0.4f, 0.9f, 1f);
            for (int i = 0; i < count; i++)
                dots.Add(Prim(PrimitiveType.Sphere, transform, path[0], Vector3.one * 0.16f, glow, emission: glow * 2f, name: "Electron"));
            _wires.Add((path, length, dots));
        }

        void Update()
        {
            foreach (var (path, length, dots) in _wires)
                for (int i = 0; i < dots.Count; i++)
                {
                    float d = Mathf.Repeat(Time.time * Speed + i * length / dots.Count, length);
                    dots[i].localPosition = PointAt(path, d);
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
