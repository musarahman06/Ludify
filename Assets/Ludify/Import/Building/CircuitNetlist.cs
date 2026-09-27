using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Turns a circuit <see cref="SceneModel"/> into electrical nets: decides which lead of each
    /// component every wire attaches to, then merges connected leads (union-find) and builds a
    /// <see cref="CircuitSolver"/>. The same attachments are used to route PCB traces, so what you
    /// see is exactly what gets simulated.
    /// </summary>
    public sealed class CircuitNetlist
    {
        public static readonly HashSet<string> TwoTerminal =
            new HashSet<string> { "battery", "resistor", "bulb", "switch", "capacitor", "led", "meter" };

        public sealed class LinkEnds
        {
            public SceneLink Link;
            public PartLibrary.BuiltPart From, To;
            public int FromTerminal, ToTerminal;
        }

        public readonly List<LinkEnds> Links = new List<LinkEnds>();
        public readonly CircuitSolver Solver = new CircuitSolver();
        public readonly Dictionary<string, CircuitSolver.Element> ElementByPart = new Dictionary<string, CircuitSolver.Element>();

        public static bool IsCircuit(SceneModel m) =>
            m.Parts.Any(p => TwoTerminal.Contains(p.Kind) || p.Kind == "ground");

        /// <summary>Terminal position in content space.</summary>
        public static Vector3 TerminalPosition(PartLibrary.BuiltPart p, int terminal, Transform content) =>
            content.InverseTransformPoint(p.Root.TransformPoint(p.Terminals[Mathf.Clamp(terminal, 0, p.Terminals.Length - 1)]));

        public static CircuitNetlist Build(SceneModel model, Dictionary<string, PartLibrary.BuiltPart> parts, Transform content)
        {
            var net = new CircuitNetlist();
            var usable = (model.Links ?? new List<SceneLink>())
                .Where(l => l.From != l.To && parts.ContainsKey(l.From ?? "") && parts.ContainsKey(l.To ?? ""))
                .ToList();

            foreach (SceneLink l in usable)
                net.Links.Add(new LinkEnds { Link = l, From = parts[l.From], To = parts[l.To] });

            // Choose a lead for each link end.
            foreach (PartLibrary.BuiltPart part in parts.Values)
            {
                var ends = new List<(LinkEnds link, bool isFrom)>();
                foreach (LinkEnds le in net.Links)
                {
                    if (le.From == part) ends.Add((le, true));
                    if (le.To == part) ends.Add((le, false));
                }
                if (ends.Count == 0 || part.Terminals.Length < 2) continue;   // single-terminal: stays 0

                Vector3 Other(int i) => Center(ends[i].isFrom ? ends[i].link.To : ends[i].link.From, content);
                var assignment = new int[ends.Count];
                if (ends.Count == 2)
                {
                    // Two wires on a two-lead part: one per lead, whichever pairing is shorter.
                    float straight = Dist(part, 0, Other(0), content) + Dist(part, 1, Other(1), content);
                    float crossed = Dist(part, 1, Other(0), content) + Dist(part, 0, Other(1), content);
                    assignment[0] = straight <= crossed ? 0 : 1;
                    assignment[1] = 1 - assignment[0];
                }
                else
                {
                    for (int i = 0; i < ends.Count; i++)
                        assignment[i] = Dist(part, 0, Other(i), content) <= Dist(part, 1, Other(i), content) ? 0 : 1;
                }
                for (int i = 0; i < ends.Count; i++)
                {
                    if (ends[i].isFrom) ends[i].link.FromTerminal = assignment[i];
                    else ends[i].link.ToTerminal = assignment[i];
                }
            }

            // Nets: union-find over (part, terminal).
            var parent = new Dictionary<(string, int), (string, int)>();
            (string, int) Find((string, int) k)
            {
                if (!parent.TryGetValue(k, out var p)) { parent[k] = k; return k; }
                if (p.Equals(k)) return k;
                var root = Find(p);
                parent[k] = root;
                return root;
            }
            void Union((string, int) a, (string, int) b) { var ra = Find(a); var rb = Find(b); if (!ra.Equals(rb)) parent[ra] = rb; }

            foreach (LinkEnds le in net.Links)
                Union((le.From.Data.Id, le.FromTerminal), (le.To.Data.Id, le.ToTerminal));

            var netIndex = new Dictionary<(string, int), int>();
            int NetOf(string id, int terminal)
            {
                var root = Find((id, terminal));
                if (!netIndex.TryGetValue(root, out int i)) { i = netIndex.Count; netIndex[root] = i; }
                return i;
            }

            foreach (PartLibrary.BuiltPart part in parts.Values)
            {
                string kind = part.Data.Kind;
                if (!TwoTerminal.Contains(kind)) continue;
                string value = part.Data.Value ?? "";
                var e = new CircuitSolver.Element
                {
                    Id = part.Data.Id, Kind = kind,
                    NodeA = NetOf(part.Data.Id, 0), NodeB = NetOf(part.Data.Id, 1),
                };
                switch (kind)
                {
                    case "battery": e.Emf = CircuitSolver.ParseVolts(value, 9); e.Resistance = 0.2; break;
                    case "resistor": e.Resistance = CircuitSolver.ParseOhms(value, 1000); break;
                    case "bulb": e.Resistance = CircuitSolver.ParseOhms(value, 50); break;
                    case "switch": e.Resistance = 0.01; e.IsOpen = value.ToLowerInvariant().Contains("open"); break;
                    case "capacitor": e.IsOpen = true; break;
                    case "led": e.IsLed = true; break;
                    case "meter": e.Resistance = value.Trim().ToUpperInvariant().StartsWith("V") ? 1e7 : 0.01; break;
                }
                net.Solver.Elements.Add(e);
                net.ElementByPart[part.Data.Id] = e;
            }

            PartLibrary.BuiltPart ground = parts.Values.FirstOrDefault(p => p.Data.Kind == "ground");
            CircuitSolver.Element battery = net.Solver.Elements.FirstOrDefault(e => e.Kind == "battery");
            net.Solver.Ground = ground != null ? NetOf(ground.Data.Id, 0) : battery != null ? battery.NodeA : 0;
            net.Solver.NodeCount = Mathf.Max(netIndex.Count, 1);
            return net;
        }

        /// <summary>Current flowing out of <paramref name="part"/> into the wire at <paramref name="terminal"/>, or null if unknown (junctions).</summary>
        public double? CurrentLeaving(PartLibrary.BuiltPart part, int terminal)
        {
            if (!ElementByPart.TryGetValue(part.Data.Id, out var e)) return null;
            return terminal == 1 ? e.Current : -e.Current;
        }

        static Vector3 Center(PartLibrary.BuiltPart p, Transform content) => content.InverseTransformPoint(p.Root.position);

        static float Dist(PartLibrary.BuiltPart p, int terminal, Vector3 target, Transform content) =>
            Vector3.Distance(TerminalPosition(p, terminal, content), target);
    }
}
