using System;
using System.Collections.Generic;

namespace Ludify.Import
{
    /// <summary>
    /// A 3D description of an image (circuit, molecule, labeled diagram…) produced by Gemini and
    /// turned into GameObjects by <see cref="ModelBuilder"/>. Coordinates are in a 10×10×10 layout
    /// box, as seen by a viewer standing in front: x = left→right, y = up (0 = floor), z = near→far
    /// (0 = closest to the viewer). For flat diagrams: diagram left→right = x, top→bottom = z far→near.
    /// </summary>
    [Serializable]
    public sealed class SceneModel
    {
        public int SchemaVersion = 1;
        public string Title;
        public string Subject;
        /// <summary>2–3 sentences for students, shown on the pedestal plaque.</summary>
        public string Explanation;
        /// <summary>"model" = build parts; "image" = the picture isn't something to build (show it framed).</summary>
        public string DisplayMode = "model";
        public List<ScenePart> Parts = new List<ScenePart>();
        public List<SceneLink> Links = new List<SceneLink>();

        public bool IsModel => DisplayMode != "image" && Parts != null && Parts.Count > 0;
    }

    [Serializable]
    public sealed class ScenePart
    {
        public string Id;
        /// <summary>One of <see cref="PartKinds.All"/>.</summary>
        public string Kind;
        public float X, Y, Z;
        /// <summary>Overall size in layout units (about 1 = a typical component).</summary>
        public float Size = 1f;
        /// <summary>Rotation around the vertical axis, degrees.</summary>
        public float RotationY;
        /// <summary>CSS-style name or #RRGGBB. Optional.</summary>
        public string Color;
        /// <summary>Short name shown floating above the part, e.g. "R1" or "Nucleus".</summary>
        public string Label;
        /// <summary>Kind-specific value: "9V", "220Ω", element symbol "O", meter type "A"/"V"…</summary>
        public string Value;
    }

    [Serializable]
    public sealed class SceneLink
    {
        public string From;
        public string To;
        /// <summary>One of <see cref="PartKinds.LinkKinds"/>.</summary>
        public string Kind = "line";
        public string Label;
    }

    public static class PartKinds
    {
        public static readonly string[] Generic = { "box", "sphere", "cylinder", "cone", "arrow", "label", "panel" };
        public static readonly string[] Circuit = { "battery", "resistor", "bulb", "switch", "capacitor", "led", "meter", "ground", "node" };
        public static readonly string[] Chemistry = { "atom" };
        public static readonly string[] LinkKinds = { "wire", "bond", "double_bond", "arrow", "line" };

        public static string[] All
        {
            get
            {
                var all = new List<string>(Generic);
                all.AddRange(Circuit);
                all.AddRange(Chemistry);
                return all.ToArray();
            }
        }
    }
}
