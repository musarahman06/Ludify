using Ludify.Map;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The clothing stall downtown: a counter under a striped awning, a "CLOTHES" sign, a clothes rail, a dressed-up
/// mannequin and Stella the shopkeeper (talk to her with E to open the <see cref="StoreView"/>). Marked on the maps
/// with a pink "$" you can fast travel to.
/// </summary>
public static class ClothingShop
{
    public static Npc Keeper { get; private set; }
    public static Vector3 Position { get; private set; }

    static readonly Color Pink = new Color(1f, 0.45f, 0.7f), Wood = new Color(0.55f, 0.38f, 0.24f), Cream = new Color(0.98f, 0.96f, 0.92f);
    static readonly Vector3 PreferredSpot = new Vector3(398f, 0f, 236f);   // middle of downtown
    static Material template;

    public static void Build(PlayerController player, Transform parent)
    {
        template = player.visualRoot.GetComponentInChildren<MeshRenderer>().sharedMaterial;
        if (!FindSpot(out Vector3 spot, out Quaternion facing))
        {
            Debug.LogWarning("[CityLife] No room for the clothing shop downtown.");
            return;
        }
        Position = spot;

        var root = new GameObject("ClothingShop").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(spot, facing);   // +Z = the customer side

        // Shopkeeper first, while the ground behind the counter is still on the NavMesh.
        Keeper = NpcFactory.Create(player, root.TransformPoint(new Vector3(0f, 0f, -0.5f)), root);
        Keeper.DisplayName = "Stella";
        Keeper.name = "NPC_Stella_Shopkeeper";
        Keeper.transform.rotation = facing;
        Keeper.MakeStationary();
        Keeper.SetIcon(Npc.Icon.Shop);

        // Counter, posts and awning.
        Part(root, "Counter", new Vector3(0f, 0.55f, 0.35f), new Vector3(3.2f, 1.1f, 0.8f), Wood, collider: true);
        Part(root, "CounterTop", new Vector3(0f, 1.12f, 0.35f), new Vector3(3.35f, 0.06f, 0.95f), Cream);
        foreach (float x in new[] { -1.65f, 1.65f })
        foreach (float z in new[] { -1.1f, 0.8f })
            Part(root, "Post", new Vector3(x, 1.4f, z), new Vector3(0.12f, 1.4f, 0.12f), Wood, PrimitiveType.Cylinder, collider: true);
        var awning = new GameObject("Awning").transform;
        awning.SetParent(root, false);
        awning.localPosition = new Vector3(0f, 2.85f, -0.15f);
        awning.localRotation = Quaternion.Euler(12f, 0f, 0f);   // front edge lower
        for (int i = 0; i < 6; i++)
            Part(awning, "Stripe", new Vector3(-1.5f + i * 0.6f, 0f, 0f), new Vector3(0.6f, 0.07f, 2.5f), i % 2 == 0 ? Pink : Cream);

        // Sign on the front of the awning.
        Part(root, "SignBoard", new Vector3(0f, 3.35f, 0.95f), new Vector3(2.7f, 0.6f, 0.1f), new Color(0.2f, 0.12f, 0.2f));
        var sign = new GameObject("SignText").AddComponent<TextMeshPro>();
        sign.transform.SetParent(root, false);
        sign.transform.localPosition = new Vector3(0f, 3.35f, 1.01f);
        sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // readable from the street side
        sign.text = "CLOTHES";
        sign.fontSize = 5f;
        sign.fontStyle = FontStyles.Bold;
        sign.alignment = TextAlignmentOptions.Center;
        sign.color = Pink;
        sign.rectTransform.sizeDelta = new Vector2(2.6f, 0.6f);

        // Clothes rail with a few shirts.
        Part(root, "Rail", new Vector3(-2.4f, 1.75f, 0.3f), new Vector3(0.05f, 0.7f, 0.05f), Wood, PrimitiveType.Cylinder);
        Part(root, "RailBar", new Vector3(-2.4f, 2.4f, 0.3f), new Vector3(0.05f, 0.6f, 0.05f), Wood, PrimitiveType.Cylinder, euler: new Vector3(90f, 0f, 0f));
        Color[] shirts = { new Color(0.89f, 0.34f, 0.29f), new Color(0.15f, 0.68f, 0.38f), new Color(0.95f, 0.79f, 0.3f), new Color(0.61f, 0.32f, 0.88f) };
        for (int i = 0; i < shirts.Length; i++)
            Part(root, "Shirt", new Vector3(-2.4f, 2.05f, -0.15f + i * 0.3f), new Vector3(0.45f, 0.6f, 0.08f), shirts[i], euler: new Vector3(0f, 90f, 0f));

        // Mannequin showing off a top hat and sunglasses.
        Part(root, "Pedestal", new Vector3(2.45f, 0.1f, 0.35f), new Vector3(0.9f, 0.2f, 0.9f), Cream, PrimitiveType.Cylinder, collider: true);
        var dummy = Object.Instantiate(player.visualRoot.gameObject, root, false);
        dummy.name = "Mannequin";
        foreach (var col in dummy.GetComponentsInChildren<Collider>()) Object.Destroy(col);
        Wardrobe.RemoveAccessories(dummy.transform);
        dummy.transform.localPosition = new Vector3(2.45f, 0.2f, 0.35f);
        dummy.transform.localRotation = Quaternion.identity;
        foreach (var r in dummy.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.enabled = true;
            if (r.name.Contains("Eye")) continue;
            r.sharedMaterial = NpcFactory.MaterialFor(template, r.name == "Torso" || r.name.Contains("Arm") ? new Color(0.95f, 0.45f, 0.65f) : new Color(0.93f, 0.91f, 0.88f));
        }
        Wardrobe.Apply(dummy.transform, Wardrobe.ById("hat_top"));
        Wardrobe.Apply(dummy.transform, Wardrobe.ById("glasses_sun"));

        // Keep residents from walking through the stall.
        var obstacle = root.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = new Vector3(0f, 1f, -0.1f);
        obstacle.size = new Vector3(6.2f, 2f, 2.6f);
        obstacle.carving = true;

        MapMarkers.Add(new FastTravelPoint
        {
            Name = "Clothing Store",
            Glyph = "$",
            Color = Pink,
            Position = root.TransformPoint(new Vector3(0f, 0f, 3f)),
            LookAt = spot,
        });
    }

    /// <summary>A street spot near the middle of downtown with room for the stall, facing the most open direction.</summary>
    static bool FindSpot(out Vector3 spot, out Quaternion facing)
    {
        for (float ring = 0f; ring <= 60f; ring += 4f)
        {
            int samples = ring == 0f ? 1 : Mathf.CeilToInt(ring * 0.8f);
            for (int i = 0; i < samples; i++)
            {
                float a = i * Mathf.PI * 2f / samples;
                Vector3? p = CityNav.Snap(PreferredSpot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring, 1.5f);
                if (!p.HasValue) continue;
                // Room for the stall plus a mannequin and a rail on either side.
                if (Physics.CheckBox(p.Value + Vector3.up * 1.8f, new Vector3(3.6f, 1.6f, 3.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                // Face along the most open direction so customers have space in front.
                float best = -1f;
                facing = Quaternion.identity;
                foreach (var dir in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    float free = Physics.Raycast(p.Value + Vector3.up, dir, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 30f;
                    if (free > best) { best = free; facing = Quaternion.LookRotation(dir); }
                }
                if (best < 15f) continue;   // not tucked into an alley: customers need to see it from the street
                spot = p.Value;
                return true;
            }
        }
        spot = default;
        facing = Quaternion.identity;
        return false;
    }

    static Transform Part(Transform parent, string name, Vector3 pos, Vector3 size, Color color,
                          PrimitiveType type = PrimitiveType.Cube, bool collider = false, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = NpcFactory.MaterialFor(template, color);
        return go.transform;
    }
}
