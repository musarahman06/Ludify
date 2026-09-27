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
            // Stylised objects (animals, organs, tools): parts overlap on purpose where they join; keep them as designed.
            if (model.Kit == "object") return;

            // Geometry with measurements: the coordinates *are* the shape, so keep them exactly.
            if ((model.Links ?? new List<SceneLink>()).Any(l => l.Kind == "dimension")) return;

            bool isCircuit = parts.Any(p => System.Array.IndexOf(PartKinds.Circuit, p.Kind) >= 0 && p.Kind != "node");
            Stretch(parts, isCircuit);
            if (isCircuit) SpringLayout(model);
            else if (parts.Any(p => p.Kind == "gear")) MeshGears(model);
            else PushApart(parts);

            if (parts.Any(p => p.Kind == "bar")) LineUpBars(parts);
            if (parts.Any(p => p.Kind == "shell")) ContainInShell(parts);
            ScaleSpace(model);
        }

        /// <summary>Bar charts: bars stand on the floor in one evenly spaced row, in their original order.</summary>
        static void LineUpBars(List<ScenePart> parts)
        {
            var bars = parts.Where(p => p.Kind == "bar").OrderBy(p => p.X).ToList();
            for (int i = 0; i < bars.Count; i++)
            {
                bars[i].X = bars.Count == 1 ? 5f : 1f + 8f * i / (bars.Count - 1);
                bars[i].Y = 0;
                bars[i].Z = 5;
                bars[i].Size = Mathf.Min(bars[i].Size, 6f / Mathf.Max(bars.Count, 1));
            }
        }

        /// <summary>
        /// Solar systems: real proportions would make planets invisible specks, so size bodies relative
        /// to the widest orbit, and keep each moon's orbit outside its planet.
        /// </summary>
        static void ScaleSpace(SceneModel model)
        {
            List<ScenePart> parts = model.Parts;
            ScenePart star = parts.FirstOrDefault(p => p.Kind == "star");
            if (star == null) return;
            float widest = parts.Where(p => p.Kind == "planet")
                .Select(p => Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(star.X, star.Z))).DefaultIfEmpty(4f).Max();

            // Evenly spaced orbits in the planets' original order (like a classroom diagram), moons moving with their planet.
            var planets = parts.Where(p => p.Kind == "planet")
                .OrderBy(p => Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(star.X, star.Z))).ToList();
            var moonsOf = (model.Links ?? new List<SceneLink>()).Where(l => l.Kind == "orbit")
                .Select(l => (moon: parts.FirstOrDefault(p => p.Id == l.From && p.Kind == "moon"), host: l.To))
                .Where(x => x.moon != null).ToList();
            for (int i = 0; i < planets.Count; i++)
            {
                ScenePart pl = planets[i];
                Vector2 d = new Vector2(pl.X - star.X, pl.Z - star.Z);
                if (d.sqrMagnitude < 1e-4f) d = Vector2.right;
                Vector2 target = d.normalized * widest * (0.35f + 0.65f * (i + 1) / planets.Count);
                Vector2 shift = new Vector2(star.X, star.Z) + target - new Vector2(pl.X, pl.Z);
                pl.X += shift.x; pl.Z += shift.y;
                foreach (var (moon, host) in moonsOf)
                    if (host == pl.Id) { moon.X += shift.x; moon.Z += shift.y; }
            }
            star.Size = Mathf.Max(star.Size, widest * 0.28f);
            foreach (ScenePart p in parts)
            {
                if (p.Kind == "planet") p.Size = Mathf.Clamp(p.Size, widest * 0.09f, widest * 0.22f);
                if (p.Kind == "moon") p.Size = Mathf.Clamp(p.Size, widest * 0.04f, widest * 0.07f);
            }
            var byId = parts.ToDictionary(p => p.Id);
            foreach (SceneLink l in (model.Links ?? new List<SceneLink>()).Where(l => l.Kind == "orbit"))
            {
                if (!byId.TryGetValue(l.From, out var moon) || !byId.TryGetValue(l.To, out var host) || moon.Kind != "moon") continue;
                Vector2 d = new Vector2(moon.X - host.X, moon.Z - host.Z);
                if (d.sqrMagnitude < 1e-4f) d = Vector2.up;
                float need = host.Size * 0.5f + moon.Size + 0.3f;
                if (d.magnitude < need) { d = d.normalized * need; moon.X = host.X + d.x; moon.Z = host.Z + d.y; moon.Y = host.Y; }
            }
        }

        /// <summary>Cells: grow and centre the membrane so every organelle sits inside it.</summary>
        static void ContainInShell(List<ScenePart> parts)
        {
            ScenePart shell = parts.Where(p => p.Kind == "shell").OrderByDescending(p => p.Size).First();
            var inside = parts.Where(p => p != shell && p.Kind != "label").ToList();
            if (inside.Count == 0) return;
            Vector3 centre = Vector3.zero;
            foreach (ScenePart p in inside) centre += new Vector3(p.X, p.Y, p.Z);
            centre /= inside.Count;
            float reach = inside.Max(p => Vector3.Distance(new Vector3(p.X, p.Y, p.Z), centre) + p.Size * 0.5f);
            const float maxRadius = 6f;
            if (reach > maxRadius)
            {
                // Too spread out for one membrane: pull the organelles in toward the middle instead.
                float f = (maxRadius - 0.4f) / reach;
                foreach (ScenePart p in inside)
                {
                    p.X = centre.x + (p.X - centre.x) * f;
                    p.Y = centre.y + (p.Y - centre.y) * f;
                    p.Z = centre.z + (p.Z - centre.z) * f;
                }
                reach = maxRadius;
            }
            shell.X = centre.x; shell.Y = centre.y; shell.Z = centre.z;
            shell.Size = Mathf.Max(shell.Size, reach * 2f + 0.8f);
        }

        /// <summary>Moves meshing gears so their teeth just touch (radius ≈ half the size).</summary>
        static void MeshGears(SceneModel model)
        {
            var byId = model.Parts.ToDictionary(p => p.Id);
            var placed = new HashSet<string>();
            foreach (SceneLink l in (model.Links ?? new List<SceneLink>()).Where(l => l.Kind == "mesh"))
            {
                if (!byId.TryGetValue(l.From, out var a) || !byId.TryGetValue(l.To, out var b)) continue;
                if (placed.Contains(b.Id) && !placed.Contains(a.Id)) (a, b) = (b, a);
                placed.Add(a.Id);
                if (placed.Contains(b.Id)) continue;
                Vector2 d = new Vector2(b.X - a.X, b.Y - a.Y);          // gears face the viewer: they sit in the x/y plane
                if (d.sqrMagnitude < 1e-4f) d = Vector2.right;
                d = d.normalized * (a.Size + b.Size) * 0.5f * 0.95f;
                b.X = Mathf.Clamp(a.X + d.x, Min, Max);
                b.Y = Mathf.Clamp(a.Y + d.y, 0f, Max);
                b.Z = a.Z;
                placed.Add(b.Id);
            }
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
            foreach (ScenePart p in parts) if (p.Kind != "shell") p.Size = Mathf.Min(p.Size, 2.5f);
            for (int iter = 0; iter < 100; iter++)
            {
                bool moved = false;
                for (int i = 0; i < parts.Count; i++)
                for (int j = i + 1; j < parts.Count; j++)
                {
                    ScenePart a = parts[i], b = parts[j];
                    // Labels float freely; shells (cell membranes) are meant to contain the other parts.
                    if (a.Kind == "label" || b.Kind == "label" || a.Kind == "shell" || b.Kind == "shell") continue;
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
