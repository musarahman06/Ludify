using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clothes for the blocky character: the store catalog, what you own/wear (saved in <see cref="PlayerProgress"/>),
/// and dressing a character. Colors swap the materials of the matching body parts (same names as
/// <see cref="NpcFactory"/> uses); hats, glasses, stripes, shorts and sneaker soles are small low-poly pieces named
/// "Accessory_&lt;slot&gt;_…" so the previous item in a slot can be taken off.
/// </summary>
public static class Wardrobe
{
    public enum Slot { Shirt, Hat, Glasses, Pants, Shoes }

    public enum Style
    {
        Plain, Striped,                                         // shirts
        NoHat, Cap, Beanie, TopHat, Cowboy, PartyHat, Crown,    // hats
        NoGlasses, Round, Sunglasses, Star, ThreeD,             // glasses
        Shorts,                                                 // pants
        Sneakers,                                               // shoes
    }

    public sealed class Item
    {
        public string Id, Name;
        public Slot Slot;
        public int Price;
        /// <summary>Main color; null keeps the character's original color (starter items).</summary>
        public Color? Color;
        public Color Accent = UnityEngine.Color.white;
        public Style Style;
        public bool IsStarter => Price == 0;
    }

    public static readonly Slot[] Slots = { Slot.Shirt, Slot.Hat, Slot.Glasses, Slot.Pants, Slot.Shoes };

    public static readonly List<Item> Catalog = new List<Item>
    {
        // Shirts
        new Item { Id = "shirt_starter", Name = "Classic", Slot = Slot.Shirt, Price = 0 },
        new Item { Id = "shirt_red", Name = "Cherry red", Slot = Slot.Shirt, Price = 15, Color = Hex("#E4574A") },
        new Item { Id = "shirt_green", Name = "Leaf green", Slot = Slot.Shirt, Price = 15, Color = Hex("#27AE60") },
        new Item { Id = "shirt_yellow", Name = "Sunny yellow", Slot = Slot.Shirt, Price = 15, Color = Hex("#F2C94C") },
        new Item { Id = "shirt_purple", Name = "Grape", Slot = Slot.Shirt, Price = 20, Color = Hex("#9B51E0") },
        new Item { Id = "shirt_black", Name = "Midnight", Slot = Slot.Shirt, Price = 20, Color = Hex("#2B2F3A") },
        new Item { Id = "shirt_sailor", Name = "Sailor stripes", Slot = Slot.Shirt, Price = 35, Color = Hex("#F4F4F0"), Accent = Hex("#1F3A5F"), Style = Style.Striped },
        new Item { Id = "shirt_candy", Name = "Candy stripes", Slot = Slot.Shirt, Price = 40, Color = Hex("#FFFFFF"), Accent = Hex("#E4574A"), Style = Style.Striped },

        // Hats
        new Item { Id = "hat_none", Name = "No hat", Slot = Slot.Hat, Price = 0, Style = Style.NoHat },
        new Item { Id = "hat_cap_red", Name = "Red cap", Slot = Slot.Hat, Price = 30, Color = Hex("#D63C3C"), Style = Style.Cap },
        new Item { Id = "hat_cap_blue", Name = "Blue cap", Slot = Slot.Hat, Price = 30, Color = Hex("#2F80ED"), Style = Style.Cap },
        new Item { Id = "hat_beanie", Name = "Cozy beanie", Slot = Slot.Hat, Price = 40, Color = Hex("#F2994A"), Accent = Hex("#FFF4E0"), Style = Style.Beanie },
        new Item { Id = "hat_party", Name = "Party hat", Slot = Slot.Hat, Price = 50, Color = Hex("#FF8FB1"), Accent = Hex("#F2C94C"), Style = Style.PartyHat },
        new Item { Id = "hat_cowboy", Name = "Cowboy hat", Slot = Slot.Hat, Price = 60, Color = Hex("#8B5A2B"), Accent = Hex("#5A3A1A"), Style = Style.Cowboy },
        new Item { Id = "hat_top", Name = "Top hat", Slot = Slot.Hat, Price = 90, Color = Hex("#1E1E24"), Accent = Hex("#C0392B"), Style = Style.TopHat },
        new Item { Id = "hat_crown", Name = "Gold crown", Slot = Slot.Hat, Price = 150, Color = Hex("#F5C542"), Accent = Hex("#E0457B"), Style = Style.Crown },

        // Glasses
        new Item { Id = "glasses_none", Name = "No glasses", Slot = Slot.Glasses, Price = 0, Style = Style.NoGlasses },
        new Item { Id = "glasses_round", Name = "Round specs", Slot = Slot.Glasses, Price = 25, Color = Hex("#2B2B2B"), Accent = Hex("#BFE6FF"), Style = Style.Round },
        new Item { Id = "glasses_sun", Name = "Sunglasses", Slot = Slot.Glasses, Price = 40, Color = Hex("#111114"), Style = Style.Sunglasses },
        new Item { Id = "glasses_3d", Name = "3D glasses", Slot = Slot.Glasses, Price = 50, Color = Hex("#F4F4F4"), Style = Style.ThreeD },
        new Item { Id = "glasses_star", Name = "Star glasses", Slot = Slot.Glasses, Price = 60, Color = Hex("#FF5FA2"), Accent = Hex("#FFE066"), Style = Style.Star },

        // Pants
        new Item { Id = "pants_starter", Name = "Classic", Slot = Slot.Pants, Price = 0 },
        new Item { Id = "pants_jeans", Name = "Blue jeans", Slot = Slot.Pants, Price = 15, Color = Hex("#3B5998") },
        new Item { Id = "pants_khaki", Name = "Khakis", Slot = Slot.Pants, Price = 15, Color = Hex("#C9B79C") },
        new Item { Id = "pants_red", Name = "Red trousers", Slot = Slot.Pants, Price = 25, Color = Hex("#A93226") },
        new Item { Id = "pants_shorts_denim", Name = "Denim shorts", Slot = Slot.Pants, Price = 30, Color = Hex("#4A6FA5"), Style = Style.Shorts },
        new Item { Id = "pants_shorts_green", Name = "Green shorts", Slot = Slot.Pants, Price = 30, Color = Hex("#5B7F3A"), Style = Style.Shorts },

        // Shoes
        new Item { Id = "shoes_starter", Name = "Classic", Slot = Slot.Shoes, Price = 0 },
        new Item { Id = "shoes_red", Name = "Red kicks", Slot = Slot.Shoes, Price = 15, Color = Hex("#C0392B") },
        new Item { Id = "shoes_white", Name = "White kicks", Slot = Slot.Shoes, Price = 20, Color = Hex("#F2F2F2") },
        new Item { Id = "shoes_sneaker_blue", Name = "Blue sneakers", Slot = Slot.Shoes, Price = 35, Color = Hex("#2F80ED"), Accent = Hex("#FFFFFF"), Style = Style.Sneakers },
        new Item { Id = "shoes_sneaker_pink", Name = "Pink sneakers", Slot = Slot.Shoes, Price = 35, Color = Hex("#FF8FB1"), Accent = Hex("#FFFFFF"), Style = Style.Sneakers },
        new Item { Id = "shoes_gold", Name = "Gold shoes", Slot = Slot.Shoes, Price = 45, Color = Hex("#F5C542") },
    };

    public static IEnumerable<Item> InSlot(Slot slot)
    {
        foreach (var item in Catalog) if (item.Slot == slot) yield return item;
    }

    public static Item Starter(Slot slot)
    {
        foreach (var item in InSlot(slot)) if (item.IsStarter) return item;
        return null;
    }

    public static Item ById(string id) => Catalog.Find(i => i.Id == id);

    public static bool Owns(Item item) => item.IsStarter || PlayerProgress.Owns(item.Id);

    /// <summary>What the player is wearing in a slot (the starter item if nothing was bought).</summary>
    public static Item Equipped(Slot slot) => ById(PlayerProgress.EquippedIn(slot.ToString()) ?? "") ?? Starter(slot);

    public static void ApplyEquipped(Transform visualRoot)
    {
        foreach (var slot in Slots) Apply(visualRoot, Equipped(slot));
    }

    /// <summary>Takes off every accessory (e.g. on a clone that shouldn't wear the player's outfit).</summary>
    public static void RemoveAccessories(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name.StartsWith("Accessory_")) Object.DestroyImmediate(t.gameObject);
    }

    /// <summary>The color a store card shows for an item (starter items: the character's own color).</summary>
    public static Color SwatchColor(Item item, Transform visualRoot)
    {
        if (item.Color.HasValue) return item.Color.Value;
        if (item.Style == Style.NoHat || item.Style == Style.NoGlasses) return new Color(0.45f, 0.48f, 0.55f);
        var state = State(visualRoot);
        string part = item.Slot == Slot.Shirt ? "Torso" : item.Slot == Slot.Pants ? "LeftLegMesh" : "LeftShoeMesh";
        foreach (var kv in state.Materials)
            if (kv.Key != null && kv.Key.name == part) return kv.Value.color;
        return Color.grey;
    }

    // ------------------------------------------------------------------ dressing

    /// <summary>Original materials and leg shapes, so a slot can go back to the starter look.</summary>
    sealed class Originals : MonoBehaviour
    {
        public readonly Dictionary<Renderer, Material> Materials = new Dictionary<Renderer, Material>();
        public readonly Dictionary<Transform, (Vector3 pos, Vector3 scale)> Shapes = new Dictionary<Transform, (Vector3, Vector3)>();
        public Material Skin;
    }

    static Originals State(Transform root)
    {
        var state = root.GetComponent<Originals>();
        if (state != null && state.Materials.Count > 0) return state;   // (a clone copies the component but not its data)
        if (state == null) state = root.gameObject.AddComponent<Originals>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name.StartsWith("Accessory_")) continue;
            state.Materials[r] = r.sharedMaterial;
            if (r.name.Contains("LegMesh")) state.Shapes[r.transform] = (r.transform.localPosition, r.transform.localScale);
            if (r.name == "Head") state.Skin = r.sharedMaterial;
        }
        return state;
    }

    static bool InSlotPart(string part, Slot slot) =>
        slot == Slot.Shirt ? part == "Torso" || part.Contains("ArmMesh")
        : slot == Slot.Pants ? part.Contains("LegMesh")
        : slot == Slot.Shoes ? part.Contains("ShoeMesh")
        : slot == Slot.Hat && part == "Hair";

    public static void Apply(Transform root, Item item)
    {
        if (root == null || item == null) return;
        var state = State(root);
        Slot slot = item.Slot;

        // Take off what was worn in this slot.
        string prefix = $"Accessory_{slot}_";
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name.StartsWith(prefix)) Object.Destroy(t.gameObject);
        foreach (var kv in state.Materials)
        {
            if (kv.Key == null || !InSlotPart(kv.Key.name, slot)) continue;
            kv.Key.sharedMaterial = kv.Value;
            kv.Key.enabled = true;
        }
        if (slot == Slot.Pants)
            foreach (var kv in state.Shapes) { kv.Key.localPosition = kv.Value.pos; kv.Key.localScale = kv.Value.scale; }

        // Put on the new item.
        Material template = state.Materials.Count > 0 ? new List<Material>(state.Materials.Values)[0] : null;
        if (item.Color.HasValue && slot != Slot.Hat && slot != Slot.Glasses)
            foreach (var kv in state.Materials)
                if (kv.Key != null && InSlotPart(kv.Key.name, slot)) kv.Key.sharedMaterial = NpcFactory.MaterialFor(kv.Value, item.Color.Value);

        Color main = item.Color ?? Color.white, accent = item.Accent;
        Material Mat(Color c) => NpcFactory.MaterialFor(template, c);
        Transform Piece(string name, Transform parent, Vector3 pos, Vector3 size, Color c, PrimitiveType type = PrimitiveType.Cube, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = prefix + name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go.transform;
        }
        void Hair(bool visible)
        {
            foreach (var kv in state.Materials) if (kv.Key != null && kv.Key.name == "Hair") kv.Key.enabled = visible;
        }

        // Glasses arms running back along the sides of the head to the ears.
        void GlassArms(Color c)
        {
            foreach (float x in new[] { -0.215f, 0.215f })
                Piece("Arm", root, new Vector3(x, 1.815f, 0.06f), new Vector3(0.02f, 0.02f, 0.34f), c);
        }

        switch (item.Style)
        {
            case Style.Striped:
                foreach (float y in new[] { 1.02f, 1.2f, 1.38f })
                    Piece("Stripe", root, new Vector3(0f, y, 0f), new Vector3(0.635f, 0.07f, 0.375f), accent);
                break;

            // Hats sit on the hair (head top 1.98, hair top 2.05); hair is hidden under hats that cover it.
            case Style.Cap:
                Piece("Crown", root, new Vector3(0f, 2.1f, -0.02f), new Vector3(0.47f, 0.14f, 0.45f), main);
                Piece("Brim", root, new Vector3(0f, 2.04f, 0.3f), new Vector3(0.44f, 0.035f, 0.24f), main * 0.85f);
                Piece("Button", root, new Vector3(0f, 2.185f, -0.02f), new Vector3(0.07f, 0.03f, 0.07f), main * 0.85f);
                break;
            case Style.Beanie:
                Hair(false);
                Piece("Cap", root, new Vector3(0f, 2.06f, -0.02f), new Vector3(0.47f, 0.24f, 0.46f), main);
                Piece("Cuff", root, new Vector3(0f, 1.95f, -0.02f), new Vector3(0.48f, 0.07f, 0.47f), main * 0.85f);
                Piece("Pompom", root, new Vector3(0f, 2.23f, -0.02f), new Vector3(0.14f, 0.14f, 0.14f), accent, PrimitiveType.Sphere);
                break;
            case Style.TopHat:
                Piece("Brim", root, new Vector3(0f, 2.07f, -0.02f), new Vector3(0.62f, 0.02f, 0.62f), main, PrimitiveType.Cylinder);
                Piece("Tube", root, new Vector3(0f, 2.3f, -0.02f), new Vector3(0.38f, 0.22f, 0.38f), main, PrimitiveType.Cylinder);
                Piece("Band", root, new Vector3(0f, 2.14f, -0.02f), new Vector3(0.39f, 0.04f, 0.39f), accent, PrimitiveType.Cylinder);
                break;
            case Style.Cowboy:
                Piece("Brim", root, new Vector3(0f, 2.07f, -0.02f), new Vector3(0.82f, 0.035f, 0.72f), main);
                Piece("Crown", root, new Vector3(0f, 2.19f, -0.02f), new Vector3(0.42f, 0.22f, 0.4f), main);
                Piece("Band", root, new Vector3(0f, 2.11f, -0.02f), new Vector3(0.43f, 0.05f, 0.41f), accent);
                break;
            case Style.PartyHat:
                // Stacked, shrinking tiers make a low-poly cone.
                for (int i = 0; i < 4; i++)
                {
                    float w = 0.34f - i * 0.075f;
                    Piece("Tier", root, new Vector3(0f, 2.1f + i * 0.09f, -0.02f), new Vector3(w, 0.05f, w), i % 2 == 0 ? main : accent, PrimitiveType.Cylinder);
                }
                Piece("Pompom", root, new Vector3(0f, 2.46f, -0.02f), new Vector3(0.1f, 0.1f, 0.1f), accent, PrimitiveType.Sphere);
                break;
            case Style.Crown:
                Piece("Band", root, new Vector3(0f, 2.1f, -0.02f), new Vector3(0.46f, 0.1f, 0.44f), main);
                foreach (var (x, z) in new[] { (-0.19f, 0.18f), (0.19f, 0.18f), (-0.19f, -0.22f), (0.19f, -0.22f), (0f, 0.18f), (0f, -0.22f) })
                    Piece("Point", root, new Vector3(x, 2.2f, z), new Vector3(0.08f, 0.12f, 0.08f), main, PrimitiveType.Cube, new Vector3(0f, 45f, 0f));
                Piece("Gem", root, new Vector3(0f, 2.1f, 0.205f), new Vector3(0.07f, 0.07f, 0.03f), accent);
                break;

            // Glasses on the face (front at z 0.20, eyes at y 1.80, x ±0.10).
            case Style.Round:
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    Piece("Rim", root, new Vector3(x, 1.8f, 0.225f), new Vector3(0.15f, 0.012f, 0.15f), main, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
                    Piece("Lens", root, new Vector3(x, 1.8f, 0.232f), new Vector3(0.115f, 0.01f, 0.115f), accent, PrimitiveType.Cylinder, new Vector3(90f, 0f, 0f));
                }
                Piece("Bridge", root, new Vector3(0f, 1.81f, 0.23f), new Vector3(0.06f, 0.018f, 0.018f), main);
                GlassArms(main);
                break;
            case Style.Sunglasses:
                foreach (float x in new[] { -0.1f, 0.1f })
                    Piece("Lens", root, new Vector3(x, 1.805f, 0.225f), new Vector3(0.15f, 0.085f, 0.025f), main);
                Piece("Bridge", root, new Vector3(0f, 1.82f, 0.225f), new Vector3(0.08f, 0.025f, 0.02f), main);
                GlassArms(main);
                break;
            case Style.ThreeD:
                Piece("Frame", root, new Vector3(0f, 1.805f, 0.222f), new Vector3(0.4f, 0.11f, 0.02f), main);
                Piece("LensL", root, new Vector3(-0.1f, 1.8f, 0.235f), new Vector3(0.13f, 0.07f, 0.01f), new Color(0.9f, 0.15f, 0.15f));
                Piece("LensR", root, new Vector3(0.1f, 1.8f, 0.235f), new Vector3(0.13f, 0.07f, 0.01f), new Color(0.15f, 0.55f, 0.95f));
                GlassArms(main);
                break;
            case Style.Star:
                foreach (float x in new[] { -0.11f, 0.11f })
                {
                    Piece("Star", root, new Vector3(x, 1.8f, 0.225f), new Vector3(0.13f, 0.13f, 0.02f), main);
                    Piece("Star", root, new Vector3(x, 1.8f, 0.226f), new Vector3(0.13f, 0.13f, 0.02f), main, PrimitiveType.Cube, new Vector3(0f, 0f, 45f));
                    Piece("Lens", root, new Vector3(x, 1.8f, 0.24f), new Vector3(0.08f, 0.08f, 0.01f), accent);
                }
                GlassArms(main);
                break;

            case Style.Shorts:
                // Pants stop above the knee; skin below.
                foreach (var kv in state.Shapes)
                {
                    var leg = kv.Key;
                    leg.localPosition = kv.Value.pos + new Vector3(0f, kv.Value.scale.y * 0.25f, 0f);
                    leg.localScale = new Vector3(kv.Value.scale.x + 0.01f, kv.Value.scale.y * 0.5f, kv.Value.scale.z + 0.01f);
                    var shin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shin.name = prefix + "Shin";
                    Object.Destroy(shin.GetComponent<Collider>());
                    shin.transform.SetParent(leg.parent, false);
                    shin.transform.localPosition = kv.Value.pos - new Vector3(0f, kv.Value.scale.y * 0.25f, 0f);
                    shin.transform.localScale = new Vector3(kv.Value.scale.x * 0.85f, kv.Value.scale.y * 0.5f, kv.Value.scale.z * 0.85f);
                    shin.GetComponent<MeshRenderer>().sharedMaterial = state.Skin != null ? state.Skin : Mat(new Color(0.93f, 0.78f, 0.65f));
                }
                break;
            case Style.Sneakers:
                foreach (var kv in state.Materials)
                {
                    if (kv.Key == null || !kv.Key.name.Contains("ShoeMesh")) continue;
                    var shoe = kv.Key.transform;
                    Piece("Sole", shoe.parent, shoe.localPosition + new Vector3(0f, -shoe.localScale.y * 0.45f, 0f),
                          new Vector3(shoe.localScale.x + 0.02f, 0.04f, shoe.localScale.z + 0.02f), accent);
                    Piece("Stripe", shoe.parent, shoe.localPosition + new Vector3(0f, 0.01f, 0f),
                          new Vector3(shoe.localScale.x + 0.01f, 0.03f, shoe.localScale.z * 0.5f), accent);
                }
                break;
        }
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
