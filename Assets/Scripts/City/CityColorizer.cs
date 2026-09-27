using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives every city building its own color. The Kenney buildings share one texture atlas, so a plain tint would turn
/// brick walls muddy; instead each atlas is hue-shifted into ~10 named palettes (walls/roofs change hue, windows and
/// grey trim stay as they are). Assignment is seeded by position so the city looks the same every session, and a
/// building never matches a close neighbour. The color names are used by quest hints ("next to a lavender building").
/// </summary>
public static class CityColorizer
{
    public static readonly string[] Groups = { "DowntownBuildings", "InnerOutskirtsBuildings", "CityOutskirtsBuildings" };

    readonly struct Palette
    {
        public readonly string Name;
        public readonly float Hue, Saturation;
        public Palette(string name, float hue, float saturation) { Name = name; Hue = hue; Saturation = saturation; }
    }

    static readonly Palette[] Palettes =
    {
        new Palette("coral", 0.99f, 0.55f),
        new Palette("terracotta", 0.04f, 0.62f),
        new Palette("sand", 0.10f, 0.35f),
        new Palette("butter yellow", 0.14f, 0.55f),
        new Palette("sage green", 0.27f, 0.35f),
        new Palette("mint", 0.42f, 0.42f),
        new Palette("teal", 0.50f, 0.50f),
        new Palette("sky blue", 0.57f, 0.50f),
        new Palette("lavender", 0.74f, 0.38f),
        new Palette("pink", 0.90f, 0.42f),
    };

    public readonly struct Building
    {
        public readonly Bounds Bounds;
        public readonly string ColorName;
        public Building(Bounds bounds, string colorName) { Bounds = bounds; ColorName = colorName; }
    }

    /// <summary>Every recolored building with its color name (for hints).</summary>
    public static readonly List<Building> Buildings = new List<Building>();

    const float NeighbourDistance = 28f;

    public static void Apply()
    {
        Buildings.Clear();
        var textureCache = new Dictionary<(Texture, int), Texture2D>();
        var materialCache = new Dictionary<(Material, int), Material>();
        var assigned = new List<(Vector3 pos, int palette)>();

        foreach (var groupName in Groups)
        {
            var group = GameObject.Find(groupName);
            if (group == null) continue;
            foreach (Transform building in group.transform)
            {
                var renderers = building.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;
                Vector3 pos = building.position;

                // Seeded pick, bumped until it differs from every close neighbour.
                int seed = Mathf.Abs((Mathf.RoundToInt(pos.x) * 73856093) ^ (Mathf.RoundToInt(pos.z) * 19349663));
                int palette = seed % Palettes.Length;
                for (int tries = 0; tries < Palettes.Length; tries++)
                {
                    bool clash = false;
                    foreach (var a in assigned)
                        if (a.palette == palette && (a.pos - pos).sqrMagnitude < NeighbourDistance * NeighbourDistance) { clash = true; break; }
                    if (!clash) break;
                    palette = (palette + 1) % Palettes.Length;
                }
                assigned.Add((pos, palette));

                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null) mats[i] = Recolored(mats[i], palette, textureCache, materialCache);
                    r.sharedMaterials = mats;
                }
                Buildings.Add(new Building(bounds, Palettes[palette].Name));
            }
        }
    }

    /// <summary>Name of the colored building nearest to a point (null if none within 40 m).</summary>
    public static string NearestBuildingColor(Vector3 p)
    {
        string best = null;
        float bestDist = 40f * 40f;
        foreach (var b in Buildings)
        {
            float d = b.Bounds.SqrDistance(new Vector3(p.x, b.Bounds.center.y, p.z));
            if (d < bestDist) { bestDist = d; best = b.ColorName; }
        }
        return best;
    }

    static Material Recolored(Material source, int palette, Dictionary<(Texture, int), Texture2D> textures, Dictionary<(Material, int), Material> materials)
    {
        if (materials.TryGetValue((source, palette), out var cached)) return cached;
        var mat = new Material(source) { name = $"{source.name}_{Palettes[palette].Name}" };
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") is Texture atlas)
        {
            if (!textures.TryGetValue((atlas, palette), out var shifted))
                textures[(atlas, palette)] = shifted = HueShift(atlas, Palettes[palette]);
            mat.SetTexture("_BaseMap", shifted);
        }
        materials[(source, palette)] = mat;
        return mat;
    }

    static Texture2D HueShift(Texture atlas, Palette palette)
    {
        // The imported atlas isn't CPU-readable, so copy it through a render texture.
        var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(atlas, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, true) { name = atlas.name + "_" + palette.Name };
        tex.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (s < 0.22f || v < 0.18f) continue;   // windows, glass, grey trim, dark roofs keep their look
            Color shifted = Color.HSVToRGB(palette.Hue, Mathf.Lerp(s, palette.Saturation, 0.7f), v);
            shifted.a = c.a;
            px[i] = shifted;
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
