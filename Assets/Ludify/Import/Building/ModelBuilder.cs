using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static Ludify.Import.ModelKit;

namespace Ludify.Import
{
    /// <summary>
    /// Turns a <see cref="SceneModel"/> into a GameObject that fits in a cube of <c>fitSize</c> metres,
    /// resting on y = 0 and centred on x/z. The exhibit's front faces −Z (a viewer at −Z looks toward +Z).
    /// </summary>
    public static class ModelBuilder
    {
        public const float DefaultFitSize = 3.2f;
        const float MinLabelHeight = 0.12f, MaxLabelHeight = 0.3f;   // metres, so labels stay readable at any model scale

        /// <summary>
        /// Builds the exhibit. Circuits become a two-sided printed circuit board with a live simulation;
        /// everything else is built from parts + straight links. The root carries an <see cref="ExhibitInfo"/>.
        /// </summary>
        public static GameObject Build(SceneModel model, float fitSize = DefaultFitSize)
        {
            var root = new GameObject("Exhibit: " + model.Title);
            var content = new GameObject("Content").transform;
            content.SetParent(root.transform, false);

            var ids = new HashSet<string>();
            for (int i = 0; i < model.Parts.Count; i++)
                if (string.IsNullOrEmpty(model.Parts[i].Id) || !ids.Add(model.Parts[i].Id)) { model.Parts[i].Id = "part" + i; ids.Add(model.Parts[i].Id); }

            LayoutRelaxer.Relax(model);
            AutoSizeComponents(model.Parts);
            SetBarHeights(model.Parts);

            var info = root.AddComponent<ExhibitInfo>();
            info.Model = model;
            info.Content = content;
            info.IsCircuit = CircuitNetlist.IsCircuit(model);

            Dictionary<string, PartLibrary.BuiltPart> parts;
            PcbBuilder.Result pcb = null;
            if (info.IsCircuit)
            {
                pcb = PcbBuilder.Build(model, content);
                parts = pcb.Parts;
            }
            else
            {
                parts = new Dictionary<string, PartLibrary.BuiltPart>();
                foreach (ScenePart p in model.Parts) parts[p.Id] = PartLibrary.Build(p, content);
                var orbits = new List<(PartLibrary.BuiltPart body, PartLibrary.BuiltPart centre)>();
                var meshes = new List<SceneLink>();
                foreach (SceneLink link in model.Links ?? new List<SceneLink>())
                {
                    if (link.From == null || link.To == null) continue;
                    if (!parts.TryGetValue(link.From, out var a) || !parts.TryGetValue(link.To, out var b) || a == b) continue;
                    string kind = (link.Kind ?? "").ToLowerInvariant();
                    if (kind == "orbit") orbits.Add((a, b));
                    else if (kind == "mesh") meshes.Add(link);
                    else LinkBuilder.Build(link, a, b, content);
                }

                // Moving systems: orbits and gear trains animate (so they don't use the exploded view).
                if (orbits.Count > 0)
                {
                    float innermost = orbits.Min(o => Vector3.Distance(o.body.Root.localPosition, o.centre.Root.localPosition));
                    foreach (var (body, centre) in orbits) Orbiter.Build(body, centre, content, innermost);
                }
                if (meshes.Count > 0) GearTrain.Build(content, parts, meshes);
                info.Animated = orbits.Count > 0 || meshes.Count > 0;
            }

            foreach (PartLibrary.BuiltPart part in parts.Values) info.Parts.Add(ExhibitPart.Attach(part.Root, part.Data));
            SetExplodeOffsets(info);

            Fit(root.transform, content, fitSize);
            foreach (ExhibitPart part in info.Parts) part.FitCollider();

            if (pcb != null)
            {
                info.Sim = root.AddComponent<CircuitSim>();
                info.Sim.Init(pcb);
            }
            return root;
        }

        /// <summary>
        /// Exploded view: circuit parts lift straight up off the board on their leads; other exhibits
        /// spread out from their centre.
        /// </summary>
        /// <summary>Bar heights proportional to their values (tallest = 6 layout units).</summary>
        static void SetBarHeights(List<ScenePart> parts)
        {
            var bars = parts.Where(p => p.Kind == "bar").ToList();
            if (bars.Count == 0) return;
            float Value(ScenePart p)
            {
                string v = p.Value ?? "";
                var number = new string(v.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                return float.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 1f;
            }
            float max = Mathf.Max(bars.Max(Value), 1e-3f);
            foreach (ScenePart b in bars) b.Height = Mathf.Max(0.15f, 6f * Mathf.Max(Value(b), 0) / max);
        }

        static void SetExplodeOffsets(ExhibitInfo info)
        {
            if (info.Parts.Count == 0) return;
            if (info.IsCircuit)
            {
                foreach (ExhibitPart p in info.Parts)
                    if (p.Data.Kind != "node" && p.Data.Kind != "ground")
                        p.ExplodeOffset = Vector3.up * (0.9f + 0.6f * p.transform.localScale.x);
                return;
            }
            Vector3 centre = Vector3.zero;
            foreach (ExhibitPart p in info.Parts) centre += p.BasePosition;
            centre /= info.Parts.Count;
            foreach (ExhibitPart p in info.Parts)
            {
                Vector3 away = p.BasePosition - centre;
                p.ExplodeOffset = away.sqrMagnitude > 1e-4f ? away * 0.6f : Vector3.up * 0.8f;
            }
        }

        /// <summary>A framed picture standing on the pedestal, for images that aren't buildable as 3D.</summary>
        public static GameObject BuildImagePanel(Texture2D image, string title, float fitSize = DefaultFitSize)
        {
            var root = new GameObject("Exhibit: " + title);
            float aspect = image != null ? (float)image.width / image.height : 4f / 3f;
            float w = aspect >= 1 ? fitSize : fitSize * aspect;
            float h = aspect >= 1 ? fitSize / aspect : fitSize;
            float bottom = 0.25f;
            Color frame = new Color(0.12f, 0.1f, 0.09f);

            Prim(PrimitiveType.Cube, root.transform, new Vector3(0, bottom + h / 2, 0.03f), new Vector3(w + 0.16f, h + 0.16f, 0.06f), frame);
            Rod(root.transform, new Vector3(-w * 0.3f, 0, 0.05f), new Vector3(-w * 0.3f, bottom, 0.05f), 0.06f, frame);
            Rod(root.transform, new Vector3(w * 0.3f, 0, 0.05f), new Vector3(w * 0.3f, bottom, 0.05f), 0.06f, frame);

            Transform quad = Prim(PrimitiveType.Quad, root.transform, new Vector3(0, bottom + h / 2, -0.005f), new Vector3(w, h, 1), Color.white, name: "Picture");
            if (image != null)
            {
                var r = quad.GetComponent<Renderer>();
                var m = new Material(r.sharedMaterial) { name = "Picture", mainTexture = image, color = Color.white };
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", image);
                m.EnableKeyword("_EMISSION");                              // keep the picture bright in shade
                m.SetColor("_EmissionColor", Color.white * 0.35f);
                if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", image);
                r.sharedMaterial = m;
            }
            return root;
        }

        /// <summary>
        /// Circuit components drawn at "size 1" look tiny when the diagram spreads them far apart, so
        /// grow each to ~45% of the distance to its nearest neighbour.
        /// </summary>
        static void AutoSizeComponents(List<ScenePart> parts)
        {
            foreach (ScenePart p in parts)
            {
                if (System.Array.IndexOf(PartKinds.Circuit, (p.Kind ?? "").ToLowerInvariant()) < 0 || p.Kind == "node") continue;
                float nearest = float.MaxValue;
                foreach (ScenePart q in parts)
                    if (q != p) nearest = Mathf.Min(nearest, Vector3.Distance(new Vector3(p.X, p.Y, p.Z), new Vector3(q.X, q.Y, q.Z)));
                if (nearest < float.MaxValue) p.Size = Mathf.Clamp(Mathf.Max(p.Size, nearest * 0.6f), 0.2f, 4f);
            }
        }

        static void Fit(Transform root, Transform content, float fitSize)
        {
            Bounds? bounds = null;
            foreach (Renderer r in content.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponent<TMP_Text>() != null || r.name == "Electron") continue;
                Bounds b = r.bounds;
                if (bounds == null) bounds = b; else { Bounds x = bounds.Value; x.Encapsulate(b); bounds = x; }
            }
            if (bounds == null) return;

            // Content is still at the origin with scale 1, so world bounds = content-local bounds here.
            Bounds local = bounds.Value;
            float extent = Mathf.Max(local.size.x, local.size.y, local.size.z, 0.01f);
            float scale = fitSize / extent;
            content.localScale = Vector3.one * scale;
            content.localPosition = new Vector3(-local.center.x * scale, -local.min.y * scale, -local.center.z * scale);

            foreach (TMP_Text label in content.GetComponentsInChildren<TMP_Text>())
            {
                float world = label.transform.lossyScale.y;
                float target = Mathf.Clamp(world, MinLabelHeight, MaxLabelHeight);
                if (world > 1e-5f) label.transform.localScale *= target / world;
            }
        }
    }
}
