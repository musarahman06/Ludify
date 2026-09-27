using UnityEngine;

/// <summary>Words for city quests: pet names, items, what people say. Kept separate so it's easy to add more.</summary>
public static class QuestTemplates
{
    public enum Kind { LostPet, LostItem, Pages, Delivery }

    public static readonly string[] CatNames = { "Whiskers", "Mochi", "Luna", "Tiger", "Pumpkin", "Oreo", "Biscuit", "Nala" };
    public static readonly string[] DogNames = { "Buddy", "Max", "Daisy", "Rocky", "Pepper", "Cooper", "Bella", "Scout" };
    public static readonly string[] Items = { "car keys", "phone", "backpack", "teddy bear" };

    public static readonly string[] SmallTalk =
    {
        "Lovely day for a walk, isn't it?",
        "Have you been to the race track across the river? I hear it's fast!",
        "The tall towers downtown are my favourite part of the city.",
        "I like how every building here is a different color.",
        "If you ever get lost, check the map. Press M!",
        "People around here are always losing things. Keep an eye out!",
        "I'm just heading to the shops.",
    };

    public static int Reward(Kind kind)
    {
        switch (kind)
        {
            case Kind.LostPet: return 40;
            case Kind.LostItem: return 30;
            case Kind.Pages: return 50;
            default: return 25;
        }
    }

    public static string Noun(Kind kind, bool dog, string coat, string item) =>
        kind == Kind.LostPet ? $"{coat} {(dog ? "dog" : "cat")}"
        : kind == Kind.LostItem ? ItemWithArticle(item)
        : "some papers";

    static string ItemWithArticle(string item) => item == "car keys" ? "some car keys" : "a " + item;

    public static string Intro(Kind kind, string petName, bool dog, string coat, string item, string recipient)
    {
        switch (kind)
        {
            case Kind.LostPet:
                return $"Oh no, can you help me? My {coat} {(dog ? "dog" : "cat")} {petName} ran off somewhere in the city! " +
                       $"{(dog ? "He" : "She")}'s small, so look carefully. Maybe people around town have seen {(dog ? "him" : "her")}?";
            case Kind.LostItem:
                return $"Excuse me, can you help? I dropped my {item} somewhere in the city and I can't find {(item == "car keys" ? "them" : "it")} anywhere!";
            case Kind.Pages:
                return "The wind just blew my notes all over the place! Could you find my 5 pages? I need them for class.";
            default:
                return $"Could you do me a favour? This parcel needs to go to {recipient}, but I'm not sure where {recipient} is right now. Ask around!";
        }
    }

    public static string Title(Kind kind, string petName, bool dog, string coat, string item, string recipient)
    {
        switch (kind)
        {
            case Kind.LostPet: return $"Find {petName} the {coat} {(dog ? "dog" : "cat")}";
            case Kind.LostItem: return $"Find the lost {item}";
            case Kind.Pages: return "Collect 5 lost pages";
            default: return $"Deliver the parcel to {recipient}";
        }
    }

    /// <summary>Three levels of hint, from vague to precise, built from where the target really is.</summary>
    public static string Hint(Kind kind, int level, string noun, string recipient, Vector3 target, Vector3 hinter)
    {
        string area = CityArea.DescribeArea(target);
        string color = CityColorizer.NearestBuildingColor(target);
        string near = color != null ? $"next to a {color} building" : "near some buildings";
        string direction = CityArea.DescribeDirection(hinter, target);

        if (kind == Kind.Delivery)
        {
            switch (level)
            {
                case 0: return $"{recipient}? They usually hang around {area}.";
                case 1: return $"I just saw {recipient} walking past a {color ?? "big"} building.";
                default: return $"{recipient} went {direction}.";
            }
        }
        string it = kind == Kind.LostPet ? "It" : kind == Kind.Pages ? "They" : "It";
        switch (level)
        {
            case 0: return kind == Kind.Pages ? $"I saw papers blowing around {area}." : $"I think I saw {Article(noun)} {area}.";
            case 1:
                return kind == Kind.LostPet ? $"There was a little {noun} hiding {near}. Look for paw prints!"
                    : kind == Kind.Pages ? $"Some pages landed {near}. They sparkle when you get close."
                    : $"I spotted {Article(noun)} {near}. It sparkles when you get close.";
            default: return kind == Kind.LostPet ? $"{it} went {direction}." : $"{it} {(kind == Kind.Pages ? "are" : "is")} {direction}.";
        }
    }

    static string Article(string noun) =>
        noun.StartsWith("some ") || noun.StartsWith("a ") ? noun : ("aeiou".IndexOf(noun[0]) >= 0 ? "an " : "a ") + noun;
}
