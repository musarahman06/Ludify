using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds city residents by cloning the player's blocky character (head, hair, torso, arms/legs on pivots) and
/// dressing each one in a random outfit.
/// </summary>
public static class NpcFactory
{
    static readonly string[] FirstNames =
    {
        "Maria", "Sam", "Priya", "Leo", "Aisha", "Tom", "Mei", "Carlos", "Nora", "Jamal", "Olivia", "Kenji",
        "Fatima", "Ben", "Sofia", "Omar", "Grace", "Diego", "Hana", "Ethan", "Zara", "Luca", "Ruby", "Arjun",
    };

    static readonly Color[] Shirts =
    {
        Hex("#E4574A"), Hex("#2F80ED"), Hex("#27AE60"), Hex("#F2C94C"), Hex("#9B51E0"), Hex("#F2994A"),
        Hex("#56CCF2"), Hex("#EB5757"), Hex("#6FCF97"), Hex("#BB6BD9"), Hex("#FFFFFF"), Hex("#333A45"),
        Hex("#FF8FB1"), Hex("#1ABC9C"),
    };
    static readonly Color[] Pants = { Hex("#2D3142"), Hex("#4F5D75"), Hex("#1F3A5F"), Hex("#6B4E3D"), Hex("#3E4A3D"), Hex("#8C8C8C"), Hex("#C9B79C") };
    static readonly Color[] Skins = { Hex("#F6D5BC"), Hex("#EDC3A0"), Hex("#D9A27E"), Hex("#B97E57"), Hex("#8D5B3E"), Hex("#5E3B28") };
    static readonly Color[] Hair = { Hex("#2B1B12"), Hex("#4B2E1E"), Hex("#8A5A33"), Hex("#D9B26F"), Hex("#1A1A1A"), Hex("#A33B2B"), Hex("#9AA0A6"), Hex("#E8E3D8") };
    static readonly Color[] Shoes = { Hex("#1E1E1E"), Hex("#F2F2F2"), Hex("#6B4E3D"), Hex("#C0392B") };

    static readonly Dictionary<Color, Material> MaterialCache = new Dictionary<Color, Material>();
    static int nameIndex;

    public static Npc Create(PlayerController player, Vector3 position, Transform parent)
    {
        var root = new GameObject("NPC");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        // Clone the player's look.
        var visual = Object.Instantiate(player.visualRoot.gameObject, root.transform, false);
        visual.name = "VisualRoot";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        foreach (var col in visual.GetComponentsInChildren<Collider>()) Object.Destroy(col);
        Wardrobe.RemoveAccessories(visual.transform);   // residents don't copy the player's hats/glasses
        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;

        Material template = player.visualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        Color shirt = Pick(Shirts), pants = Pick(Pants), skin = Pick(Skins), hair = Pick(Hair), shoes = Pick(Shoes);
        foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.gameObject.name;
            Color? c = n == "Torso" || n.Contains("ArmMesh") ? shirt
                : n.Contains("LegMesh") ? pants
                : n.Contains("ShoeMesh") ? shoes
                : n == "Head" || n.Contains("HandMesh") ? skin
                : n == "Hair" ? hair
                : (Color?)null;                                  // eyes keep their material
            if (c.HasValue) r.sharedMaterial = MaterialFor(template, c.Value);
        }

        // Slight height variety.
        float s = Random.Range(0.9f, 1.06f);
        visual.transform.localScale = new Vector3(s, s, s);

        var iconAnchor = new GameObject("IconAnchor").transform;
        iconAnchor.SetParent(root.transform, false);
        iconAnchor.localPosition = new Vector3(0f, 2.9f * s, 0f);

        // Add the agent while inactive, so one placed off the city NavMesh (e.g. a job giver on the farm) can be
        // switched off before it tries to bind and logs a warning.
        root.SetActive(false);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 2f;
        agent.speed = Random.Range(1.5f, 2.3f);
        agent.acceleration = 6f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.3f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(30, 70);

        var npc = root.AddComponent<Npc>();
        if (!NavMesh.SamplePosition(position, out _, 1f, NavMesh.AllAreas)) agent.enabled = false;
        root.SetActive(true);

        npc.DisplayName = FirstNames[nameIndex++ % FirstNames.Length];
        root.name = "NPC_" + npc.DisplayName;
        npc.Init(visual.transform, iconAnchor);   // after activating: the head icon's TextMeshPro needs an active object
        return npc;
    }

    public static Material MaterialFor(Material template, Color color)
    {
        if (MaterialCache.TryGetValue(color, out var m) && m != null) return m;
        m = new Material(template) { name = "Npc_" + ColorUtility.ToHtmlStringRGB(color) };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        MaterialCache[color] = m;
        return m;
    }

    static Color Pick(Color[] options) => options[Random.Range(0, options.Length)];

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
