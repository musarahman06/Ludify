using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Builds a circuit as a two-sided printed circuit board (content space, board top at y = 0):
    /// components mounted on top with leads through plated holes, silkscreen labels, solder-masked
    /// traces on top and bare copper traces + solder joints underneath. Returns the netlist so the
    /// circuit can be simulated exactly as wired.
    /// </summary>
    public static class PcbBuilder
    {
        public const float Thickness = 0.22f;
        const float TraceWidth = 0.12f, PadRadius = 0.13f, Margin = 1.2f;

        static readonly Color Fr4 = new Color(0.05f, 0.34f, 0.16f);
        static readonly Color MaskedTrace = new Color(0.12f, 0.5f, 0.24f);
        static readonly Color Copper = new Color(0.86f, 0.5f, 0.22f);
        static readonly Color Solder = new Color(0.78f, 0.8f, 0.82f);
        static readonly Color Lead = new Color(0.72f, 0.72f, 0.75f);
        static readonly Color Hole = new Color(0.08f, 0.08f, 0.08f);
        static readonly Color Silk = new Color(0.95f, 0.95f, 0.92f);

        public sealed class Result
        {
            public Dictionary<string, PartLibrary.BuiltPart> Parts;
            public CircuitNetlist Netlist;
            public CurrentFlow Flow;
            /// <summary>For each link: the wire indices in <see cref="Flow"/> (top and bottom).</summary>
            public readonly List<(CircuitNetlist.LinkEnds link, int top, int bottom)> Wires = new List<(CircuitNetlist.LinkEnds, int, int)>();
            public Transform Board;
        }

        public static Result Build(SceneModel model, Transform content)
        {
            var result = new Result { Parts = new Dictionary<string, PartLibrary.BuiltPart>() };

            // 1. Components, standing on the board.
            foreach (ScenePart p in model.Parts)
                result.Parts[p.Id] = p.Kind == "node" || p.Kind == "ground" ? Pad(p, content) : Mount(p, content);

            // 2. Which lead each wire goes to, and the electrical nets.
            result.Netlist = CircuitNetlist.Build(model, result.Parts, content);

            // 3. Board sized to everything on it.
            var points = new List<Vector3>();
            foreach (PartLibrary.BuiltPart part in result.Parts.Values)
            {
                points.Add(part.Root.localPosition);
                for (int t = 0; t < part.Terminals.Length; t++) points.Add(PadPosition(part, t, content));
            }
            float minX = points.Min(v => v.x) - Margin, maxX = points.Max(v => v.x) + Margin;
            float minZ = points.Min(v => v.z) - Margin, maxZ = points.Max(v => v.z) + Margin;
            result.Board = Board(content, minX, maxX, minZ, maxZ, model.Title);

            // 4. Pads, holes and solder joints for every lead in use; leads down through the board.
            foreach (PartLibrary.BuiltPart part in result.Parts.Values)
            {
                if (part.Data.Kind == "node" || part.Data.Kind == "ground") continue;
                for (int t = 0; t < part.Terminals.Length; t++)
                {
                    Vector3 pad = PadPosition(part, t, content);
                    PlatedHole(content, pad);
                    Vector3 lead = CircuitNetlist.TerminalPosition(part, t, content);
                    // Leads belong to the part, so the exploded view pulls them out of the board with it.
                    Transform rod = Rod(content, lead, new Vector3(pad.x, -Thickness - 0.08f, pad.z), 0.05f, Lead);
                    rod.SetParent(part.Root, true);
                }
                SilkscreenLabel(content, part);
            }

            // 5. Traces: solder-masked on top (so you can follow them), bare copper underneath.
            result.Flow = content.gameObject.AddComponent<CurrentFlow>();
            foreach (CircuitNetlist.LinkEnds le in result.Netlist.Links)
            {
                Vector3 a = PadPosition(le.From, le.FromTerminal, content), b = PadPosition(le.To, le.ToTerminal, content);
                List<Vector3> route = Route(a, b);
                for (int i = 0; i < route.Count - 1; i++)
                {
                    Strip(content, route[i], route[i + 1], 0.004f, MaskedTrace);
                    Strip(content, route[i], route[i + 1], -Thickness - 0.012f, Copper);
                }
                int top = result.Flow.AddWire(route.Select(v => new Vector3(v.x, 0.07f, v.z)).ToList(), 0.22f);
                int bottom = result.Flow.AddWire(route.Select(v => new Vector3(v.x, -Thickness - 0.08f, v.z)).ToList(), 0.22f);
                result.Wires.Add((le, top, bottom));
            }
            return result;
        }

        /// <summary>Height of each component's origin above the board so it sits on the surface.</summary>
        static float MountHeight(string kind)
        {
            switch (kind)
            {
                case "battery": return 0.24f;
                case "resistor": return 0.18f;
                case "switch": return 0.1f;
                case "capacitor": return 0.3f;
                case "led": return 0.08f;
                case "meter": return 0.13f;
                case "bulb": return 0.02f;
                default: return 0.5f;
            }
        }

        static PartLibrary.BuiltPart Mount(ScenePart p, Transform content)
        {
            PartLibrary.BuiltPart part = PartLibrary.Build(p, content, floatingLabel: false);
            float size = part.Root.localScale.x;
            part.Root.localPosition = new Vector3(p.X, MountHeight(p.Kind) * size, p.Z);
            if (p.Kind == "battery")
            {
                // Black plastic holder with metal end clips around the cell.
                Prim(PrimitiveType.Cube, part.Root, new Vector3(-0.05f, -0.1f, 0), new Vector3(1.0f, 0.22f, 0.5f), new Color(0.1f, 0.1f, 0.11f));
                Prim(PrimitiveType.Cube, part.Root, new Vector3(-0.52f, 0.02f, 0), new Vector3(0.04f, 0.4f, 0.4f), Lead);
                Prim(PrimitiveType.Cube, part.Root, new Vector3(0.47f, 0.02f, 0), new Vector3(0.04f, 0.4f, 0.4f), Lead);
            }
            return part;
        }

        /// <summary>Junctions become vias; ground becomes a labelled square pad.</summary>
        static PartLibrary.BuiltPart Pad(ScenePart p, Transform content)
        {
            var root = new GameObject($"{p.Kind}:{p.Id}").transform;
            root.SetParent(content, false);
            root.localPosition = new Vector3(p.X, 0, p.Z);
            if (p.Kind == "ground")
            {
                Prim(PrimitiveType.Cube, root, new Vector3(0, 0.006f, 0), new Vector3(0.5f, 0.012f, 0.5f), Copper);
                Prim(PrimitiveType.Cube, root, new Vector3(0, -Thickness - 0.006f, 0), new Vector3(0.5f, 0.012f, 0.5f), Copper);
                FlatText(root, "GND", new Vector3(0, 0.02f, -0.45f), 0.22f, true);
            }
            PlatedHole(root, Vector3.zero, 0.1f);
            return new PartLibrary.BuiltPart { Data = p, Root = root, Terminals = new[] { Vector3.zero }, SurfaceRadius = 0.1f };
        }

        static Vector3 PadPosition(PartLibrary.BuiltPart part, int terminal, Transform content)
        {
            Vector3 lead = CircuitNetlist.TerminalPosition(part, terminal, content);
            return new Vector3(lead.x, 0, lead.z);
        }

        static void PlatedHole(Transform parent, Vector3 at, float radius = PadRadius)
        {
            Prim(PrimitiveType.Cylinder, parent, at + new Vector3(0, 0.006f, 0), new Vector3(radius * 2, 0.006f, radius * 2), Copper);
            Prim(PrimitiveType.Cylinder, parent, at + new Vector3(0, -Thickness - 0.006f, 0), new Vector3(radius * 2, 0.006f, radius * 2), Copper);
            Prim(PrimitiveType.Cylinder, parent, at + new Vector3(0, -Thickness / 2, 0), new Vector3(radius * 0.8f, Thickness / 2 + 0.008f, radius * 0.8f), Hole);
            Prim(PrimitiveType.Sphere, parent, at + new Vector3(0, -Thickness - 0.04f, 0), new Vector3(radius * 1.6f, 0.09f, radius * 1.6f), Solder, name: "Solder");
        }

        static Transform Board(Transform content, float minX, float maxX, float minZ, float maxZ, string title)
        {
            float w = maxX - minX, d = maxZ - minZ;
            Vector3 c = new Vector3((minX + maxX) / 2, -Thickness / 2, (minZ + maxZ) / 2);
            Transform board = Prim(PrimitiveType.Cube, content, c, new Vector3(w, Thickness, d), Fr4, name: "Board");

            // Mounting holes with copper rings in the corners.
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                Vector3 at = new Vector3(c.x + sx * (w / 2 - 0.35f), 0, c.z + sz * (d / 2 - 0.35f));
                Prim(PrimitiveType.Cylinder, content, at + new Vector3(0, 0.005f, 0), new Vector3(0.34f, 0.005f, 0.34f), Copper);
                Prim(PrimitiveType.Cylinder, content, at + new Vector3(0, -Thickness / 2, 0), new Vector3(0.2f, Thickness / 2 + 0.01f, 0.2f), Hole);
                Prim(PrimitiveType.Cylinder, content, at + new Vector3(0, -Thickness - 0.005f, 0), new Vector3(0.34f, 0.005f, 0.34f), Copper);
            }

            // Silkscreen: title on top edge, and underneath (readable when the board is flipped).
            FlatText(content, title ?? "Circuit", new Vector3(c.x, 0.012f, maxZ - 0.35f), 0.3f, true);
            FlatText(content, (title ?? "Circuit") + "  ·  bottom (copper) side", new Vector3(c.x, -Thickness - 0.012f, c.z), 0.3f, false);
            return board;
        }

        static void SilkscreenLabel(Transform content, PartLibrary.BuiltPart part)
        {
            string text = PartLibrary.LabelText(part.Data, part.Data.Kind);
            if (string.IsNullOrEmpty(text)) return;
            float size = part.Root.localScale.x;
            // Just in front of (below, on the board) the component, relative to its orientation.
            Vector3 offset = part.Root.localRotation * new Vector3(0, 0, -0.45f * size);
            Vector3 p = part.Root.localPosition;
            FlatText(content, text, new Vector3(p.x + offset.x, 0.012f, p.z + offset.z), Mathf.Clamp(0.22f * size, 0.18f, 0.4f), true);
        }

        /// <summary>Text printed flat on the board, readable from above (top) or below (bottom).</summary>
        static void FlatText(Transform parent, string text, Vector3 at, float height, bool top)
        {
            TextMeshPro t = Label(parent, text, at, height, Silk);
            Kill(t.GetComponent<Billboard>());
            t.outlineWidth = 0;
            t.transform.localRotation = top ? Quaternion.Euler(90, 0, 0) : Quaternion.Euler(-90, 0, 0);
        }

        /// <summary>Right-angle route between two pads (along X, then Z).</summary>
        static List<Vector3> Route(Vector3 a, Vector3 b)
        {
            var route = new List<Vector3> { a };
            var corner = new Vector3(b.x, 0, a.z);
            if ((corner - a).sqrMagnitude > 1e-4f && (corner - b).sqrMagnitude > 1e-4f) route.Add(corner);
            route.Add(b);
            return route;
        }

        /// <summary>A flat trace segment at height y.</summary>
        static void Strip(Transform parent, Vector3 a, Vector3 b, float y, Color color)
        {
            Vector3 d = new Vector3(b.x - a.x, 0, b.z - a.z);
            float len = d.magnitude;
            if (len < 1e-3f) return;
            Vector3 mid = new Vector3((a.x + b.x) / 2, y, (a.z + b.z) / 2);
            Prim(PrimitiveType.Cube, parent, mid, new Vector3(TraceWidth, 0.012f, len + TraceWidth),
                 color, Quaternion.LookRotation(d / len, Vector3.up), name: "Trace");
        }
    }
}
