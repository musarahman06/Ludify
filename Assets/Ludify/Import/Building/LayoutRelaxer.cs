using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Cleans up AI-generated layouts before building. Gemini gets the parts and connections right,
    /// but its coordinates are often cramped or overlapping:
    ///  - circuits: spring layout on the table plane, using the wires as springs, then components
    ///    are turned to line up with the wire through them;
    ///  - other diagrams: stretched to fill the box, overlaps pushed apart;
    ///  - molecules: left alone (their geometry is meaningful).
    /// Deterministic, so the same model always builds the same way.
    /// </summary>
    public static class LayoutRelaxer
    {
        const float Min = 0.5f, Max = 9.5f;

        public static void Relax(SceneModel model)
        {
            List<ScenePart> parts = model.Parts;
            if (parts == null || parts.Count < 2) return;

            bool isMolecule = parts.All(p => p.Kind == "atom");
            if (isMolecule) return;

            bool isCircuit = parts.Any(p => System.Array.IndexOf(PartKinds.Circuit, p.Kind) >= 0);
            Stretch(parts, isCircuit);
            if (isCircuit) SpringLayout(model);
            else PushApart(parts);
        }

        /// <summary>Scale positions so the used area fills the layout box (per axis, keeping 0.5 margins).</summary>
        static void Stretch(List<ScenePart> parts, bool flat)
        {
            void Axis(System.Func<ScenePart, float> get, System.Action<ScenePart, float> set, float lo, float hi)
            {
                float min = parts.Min(get), max = parts.Max(get);
                if (max - min < 0.01f) return;
                foreach (ScenePart p in parts) set(p, Mathf.Lerp(lo, hi, (get(p) - min) / (max - min)));
            }
            Axis(p => p.X, (p, v) => p.X = v, 1f, 9f);
            Axis(p => p.Z, (p, v) => p.Z = v, 1f, 9f);
            if (flat) foreach (ScenePart p in parts) p.Y = 0.5f;
            else Axis(p => p.Y, (p, v) => p.Y = v, 0.5f, 8f);
        }

        static void SpringLayout(SceneModel model)
        {
            List<ScenePart> parts = model.Parts;
            var index = new Dictionary<string, int>();
            for (int i = 0; i < parts.Count; i++) index[parts[i].Id] = i;
            var edges = (model.Links ?? new List<SceneLink>())
                .Where(l => index.ContainsKey(l.From) && index.ContainsKey(l.To))
                .Select(l => (index[l.From], index[l.To])).ToList();

            var pos = parts.Select(p => new Vector2(p.X, p.Z)).ToArray();
            float restLength = Mathf.Clamp(9f / Mathf.Sqrt(parts.Count), 2.2f, 4f);

            for (int iter = 0; iter < 400; iter++)
            {
                var force = new Vector2[pos.Length];
                for (int i = 0; i < pos.Length; i++)
                for (int j = i + 1; j < pos.Length; j++)
                {
                    Vector2 d = pos[i] - pos[j];
                    float dist = Mathf.Max(d.magnitude, 0.1f);
                    Vector2 push = d / dist * (restLength * restLength * 0.6f / (dist * dist));
                    force[i] += push; force[j] -= push;
                }
                foreach (var (a, b) in edges)
                {
                    Vector2 d = pos[b] - pos[a];
                    float dist = Mathf.Max(d.magnitude, 0.01f);
                    Vector2 pull = d / dist * (dist - restLength) * 0.15f;
                    force[a] += pull; force[b] -= pull;
                }
                float step = Mathf.Lerp(0.4f, 0.02f, iter / 400f);
                for (int i = 0; i < pos.Length; i++)
                {
                    pos[i] += Vector2.ClampMagnitude(force[i], 1f) * step;
                    pos[i] = new Vector2(Mathf.Clamp(pos[i].x, Min, Max), Mathf.Clamp(pos[i].y, Min, Max));
                }
            }

            for (int i = 0; i < parts.Count; i++) { parts[i].X = pos[i].x; parts[i].Z = pos[i].y; }

            // Turn two-terminal components so their leads point along the wire through them.
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].Kind == "node" || parts[i].Kind == "ground") continue;
                var neighbours = edges.Where(e => e.Item1 == i || e.Item2 == i)
                                      .Select(e => pos[e.Item1 == i ? e.Item2 : e.Item1]).ToList();
                if (neighbours.Count < 2) continue;
                Vector2 axis = neighbours[1] - neighbours[0];
                parts[i].RotationY = Mathf.Abs(axis.x) >= Mathf.Abs(axis.y) ? 0 : 90;
            }
        }

        static void PushApart(List<ScenePart> parts)
        {
            foreach (ScenePart p in parts) p.Size = Mathf.Min(p.Size, 2.5f);
            for (int iter = 0; iter < 100; iter++)
            {
                bool moved = false;
                for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    ScenePart a = parts[i], b = parts[j];
                    if (a.Kind == "label" || b.Kind == "label") continue;
                    Vector3 d = new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
                    float need = (a.Size + b.Size) * 0.55f + 0.6f;
                    float dist = d.magnitude;
                    if (dist >= need) continue;
                    Vector3 n = dist > 1e-3f ? d / dist : new Vector3(1, 0, 0.3f).normalized;
                    Vector3 shift = n * (need - dist) * 0.5f;
                    Move(a, -shift); Move(b, shift);
                    moved = true;
                }
                if (!moved) break;
            }
        }

        static void Move(ScenePart p, Vector3 d)
        {
            p.X = Mathf.Clamp(p.X + d.x, Min, Max);
            p.Y = Mathf.Clamp(p.Y + d.y, 0f, Max);
            p.Z = Mathf.Clamp(p.Z + d.z, Min, Max);
        }
    }
}
