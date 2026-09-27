using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple low-poly props for the suburb/farm jobs, built from primitives (no assets): mailboxes, mower, rake,
/// leaves, grass tufts, soil plots, well, bins, crates and bobbing markers. Also ground and clear-spot helpers.
/// </summary>
public static class JobProps
{
    static Material template;

    public static void Init(PlayerController player)
    {
        template = player.visualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
    }

    public static Material Mat(Color c) => NpcFactory.MaterialFor(template, c);

    public static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f), DarkWood = new Color(0.36f, 0.24f, 0.14f),
        Metal = new Color(0.55f, 0.58f, 0.62f), Red = new Color(0.85f, 0.2f, 0.18f), Soil = new Color(0.36f, 0.24f, 0.15f),
        WetSoil = new Color(0.22f, 0.15f, 0.1f), Leaf = new Color(0.35f, 0.72f, 0.3f), Gold = new Color(1f, 0.82f, 0.25f),
        Grass = new Color(0.36f, 0.7f, 0.28f), CutGrass = new Color(0.5f, 0.74f, 0.35f);

    /// <summary>A primitive with collider removed. Position/scale are local to <paramref name="parent"/>.</summary>
    public static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale,
                                 Color color, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = localScale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Mat(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    public static Transform Group(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(position, rotation);
        return t;
    }

    // ------------------------------------------------------------------ ground & placement

    /// <summary>Ground height under a point: the highest non-dynamic collider, else the terrain.</summary>
    public static Vector3 Ground(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (h.collider is CharacterController || h.collider.attachedRigidbody != null) continue;
            if (h.point.y > best && h.point.y < p.y + 6f) best = h.point.y;
        }
        if (float.IsNegativeInfinity(best))
        {
            var t = Terrain.activeTerrain;
            best = t != null ? t.SampleHeight(p) + t.GetPosition().y : p.y;
        }
        return new Vector3(p.x, best, p.z);
    }

    /// <summary>Nearest spot (spiralling out from <paramref name="near"/>) where a box of this size is free of
    /// colliders and of the given renderers (e.g. crops, which have no colliders).</summary>
    public static Vector3 ClearSpot(Vector3 near, Vector3 halfSize, IList<Bounds> avoid, float maxRadius = 60f)
    {
        for (float r = 0f; r <= maxRadius; r += 2f)
        {
            int steps = r == 0f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 3f);
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                var p = Ground(near + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                var center = p + Vector3.up * (halfSize.y + 0.15f);
                if (Physics.CheckBox(center, halfSize, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var box = new Bounds(center, halfSize * 2f);
                bool blocked = false;
                if (avoid != null)
                    foreach (var b in avoid)
                        if (b.Intersects(box)) { blocked = true; break; }
                if (!blocked) return p;
            }
        }
        return Ground(near);
    }

    public static List<Bounds> RendererBounds(string groupName)
    {
        var list = new List<Bounds>();
        var g = GameObject.Find(groupName);
        if (g == null) return list;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) list.Add(r.bounds);
        return list;
    }

    public static Bounds BoundsOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ------------------------------------------------------------------ props

    /// <summary>Bobbing, spinning gold diamond that marks a target.</summary>
    public static Transform Marker(Transform parent, Vector3 position, Color color, float height = 2.2f)
    {
        var g = Group(parent, "Marker", position, Quaternion.identity);
        Part(g, "Diamond", PrimitiveType.Cube, new Vector3(0f, height, 0f), new Vector3(0.35f, 0.35f, 0.35f), color, new Vector3(45f, 0f, 45f));
        g.gameObject.AddComponent<Bobber>().amplitude = 0.25f;
        return g;
    }

    public static Transform Mailbox(Transform parent, Vector3 position, Quaternion facing, out Transform flag)
    {
        var g = Group(parent, "Mailbox", position, facing);
        Part(g, "Post", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f), DarkWood);
        Part(g, "Box", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.38f, 0.32f, 0.55f), new Color(0.2f, 0.35f, 0.75f));
        flag = Part(g, "Flag", PrimitiveType.Cube, new Vector3(0.22f, 1.25f, -0.05f), new Vector3(0.04f, 0.25f, 0.08f), Red);
        flag.localRotation = Quaternion.Euler(90f, 0f, 0f);   // down = waiting for a paper
        Part(g, "Glow", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.6f, 0.01f, 1.6f), Gold);
        return g;
    }

    public static Transform Paper(Transform parent, Vector3 position)
    {
        var p = Part(parent, "Newspaper", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.12f, 0.18f, 0.12f), new Color(0.93f, 0.93f, 0.88f), new Vector3(0f, 0f, 90f));
        p.position = position;
        return p;
    }

    public static Transform Mower(Transform parent)
    {
        var g = new GameObject("Mower").transform;
        g.SetParent(parent, false);
        g.localPosition = new Vector3(0f, 0f, 1.1f);
        Part(g, "Deck", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.7f, 0.25f, 0.8f), Red);
        Part(g, "Engine", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0.05f), new Vector3(0.35f, 0.12f, 0.35f), new Color(0.15f, 0.15f, 0.15f));
        foreach (var x in new[] { -0.36f, 0.36f })
            foreach (var z in new[] { -0.32f, 0.32f })
                Part(g, "Wheel", PrimitiveType.Cylinder, new Vector3(x, 0.12f, z), new Vector3(0.22f, 0.04f, 0.22f), new Color(0.1f, 0.1f, 0.1f), new Vector3(0f, 0f, 90f));
        Part(g, "HandleL", PrimitiveType.Cube, new Vector3(-0.25f, 0.7f, -0.55f), new Vector3(0.04f, 0.04f, 0.8f), Metal, new Vector3(-40f, 0f, 0f));
        Part(g, "HandleR", PrimitiveType.Cube, new Vector3(0.25f, 0.7f, -0.55f), new Vector3(0.04f, 0.04f, 0.8f), Metal, new Vector3(-40f, 0f, 0f));
        Part(g, "Bar", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.83f), new Vector3(0.54f, 0.05f, 0.05f), new Color(0.1f, 0.1f, 0.1f));
        return g;
    }

    public static Transform Rake(Transform parent)
    {
        var g = new GameObject("Rake").transform;
        g.SetParent(parent, false);
        g.localPosition = new Vector3(0.35f, 0f, 0.6f);
        Part(g, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0.35f), new Vector3(0.05f, 0.75f, 0.05f), Wood, new Vector3(55f, 0f, 0f));
        Part(g, "Head", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.95f), new Vector3(0.6f, 0.05f, 0.08f), Metal);
        for (int i = 0; i < 7; i++)
            Part(g, "Tine", PrimitiveType.Cube, new Vector3(-0.27f + i * 0.09f, 0.03f, 1.0f), new Vector3(0.02f, 0.08f, 0.02f), Metal);
        return g;
    }

    static readonly Color[] LeafColors = { new Color(0.9f, 0.45f, 0.1f), new Color(0.85f, 0.2f, 0.1f), new Color(0.95f, 0.75f, 0.15f), new Color(0.6f, 0.3f, 0.1f) };

    public static Transform LeafCluster(Transform parent, Vector3 position, System.Random rng)
    {
        var g = Group(parent, "Leaves", position, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
        int n = 4 + rng.Next(3);
        for (int i = 0; i < n; i++)
        {
            float x = ((float)rng.NextDouble() - 0.5f) * 0.9f, z = ((float)rng.NextDouble() - 0.5f) * 0.9f;
            Part(g, "Leaf", PrimitiveType.Cube, new Vector3(x, 0.03f + i * 0.01f, z), new Vector3(0.28f, 0.02f, 0.2f),
                 LeafColors[rng.Next(LeafColors.Length)], new Vector3(0f, (float)rng.NextDouble() * 180f, 0f));
        }
        return g;
    }

    public static Transform GrassTuft(Transform parent, Vector3 position, float height)
    {
        var t = Part(parent, "Tuft", PrimitiveType.Cube, Vector3.zero, new Vector3(0.95f, height, 0.95f), Grass);
        t.position = position + Vector3.up * (height * 0.5f);
        return t;
    }

    public static Transform CompostBin(Transform parent, Vector3 position)
    {
        var g = Group(parent, "CompostBin", position, Quaternion.identity);
        Part(g, "Bin", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 1.2f), new Color(0.2f, 0.45f, 0.25f));
        Part(g, "Lid", PrimitiveType.Cube, new Vector3(0f, 1.03f, 0f), new Vector3(1.3f, 0.08f, 1.3f), new Color(0.15f, 0.3f, 0.18f));
        return g;
    }

    public static Transform Crate(Transform parent, Vector3 position)
    {
        var g = Group(parent, "HarvestCrate", position, Quaternion.identity);
        Part(g, "Crate", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 1f), Wood);
        Part(g, "Rim", PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f), new Vector3(1.45f, 0.06f, 1.05f), DarkWood);
        return g;
    }

    public static Transform Well(Transform parent, Vector3 position)
    {
        var g = Group(parent, "Well", position, Quaternion.identity);
        Part(g, "Wall", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.45f, 1.4f), new Color(0.6f, 0.58f, 0.55f));
        Part(g, "Water", PrimitiveType.Cylinder, new Vector3(0f, 0.86f, 0f), new Vector3(1.15f, 0.02f, 1.15f), new Color(0.25f, 0.5f, 0.85f));
        Part(g, "PostL", PrimitiveType.Cube, new Vector3(-0.65f, 1.3f, 0f), new Vector3(0.1f, 1.2f, 0.1f), Wood);
        Part(g, "PostR", PrimitiveType.Cube, new Vector3(0.65f, 1.3f, 0f), new Vector3(0.1f, 1.2f, 0.1f), Wood);
        Part(g, "Roof", PrimitiveType.Cube, new Vector3(0f, 1.95f, 0f), new Vector3(1.6f, 0.1f, 0.9f), Red);
        return g;
    }

    public static Transform Plot(Transform parent, Vector3 position)
    {
        var g = Group(parent, "Plot", position, Quaternion.identity);
        Part(g, "Soil", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.16f, 1.5f), Soil);
        return g;
    }
}

/// <summary>Gentle bob and spin for markers.</summary>
public class Bobber : MonoBehaviour
{
    public float amplitude = 0.2f, speed = 2f, spin = 90f;
    Vector3 basePos;
    float phase;

    void Start() { basePos = transform.localPosition; phase = Random.value * 10f; }

    void Update()
    {
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin((Time.time + phase) * speed) * amplitude);
        transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
    }
}
