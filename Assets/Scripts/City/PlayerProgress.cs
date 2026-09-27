using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Coins and helper stars earned from city quests and races, plus the clothes you own and wear, saved as JSON in
/// &lt;persistentDataPath&gt;/Progress/player_progress.json.
/// </summary>
public static class PlayerProgress
{
    // Saved with Newtonsoft JSON (not Unity serialization).
    sealed class Data
    {
        public int Coins;
        public int Stars;
        public int QuestsCompleted;
        public List<string> Owned = new List<string>();
        public Dictionary<string, string> Equipped = new Dictionary<string, string>();
    }

    static readonly (int stars, string title)[] Titles =
    {
        (0, "Newcomer"), (1, "Neighbor"), (3, "Helper"), (6, "Local Hero"), (10, "City Legend"),
    };

    static Data data;
    static string FilePath => Path.Combine(Application.persistentDataPath, "Progress", "player_progress.json");

    /// <summary>Raised after coins or stars change.</summary>
    public static event Action Changed;

    public static int Coins { get { Load(); return data.Coins; } }
    public static int Stars { get { Load(); return data.Stars; } }
    public static int QuestsCompleted { get { Load(); return data.QuestsCompleted; } }

    public static string Title
    {
        get
        {
            string title = Titles[0].title;
            foreach (var t in Titles) if (Stars >= t.stars) title = t.title;
            return title;
        }
    }

    /// <summary>Stars needed for the next title, or -1 at the top.</summary>
    public static int NextTitleAt
    {
        get
        {
            foreach (var t in Titles) if (t.stars > Stars) return t.stars;
            return -1;
        }
    }

    /// <summary>Coins from anything other than a quest (e.g. a correct lap question).</summary>
    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Load();
        data.Coins += amount;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Takes the coins if you have enough.</summary>
    public static bool TrySpend(int amount)
    {
        Load();
        if (amount < 0 || data.Coins < amount) return false;
        data.Coins -= amount;
        Save();
        Changed?.Invoke();
        return true;
    }

    public static bool Owns(string itemId) { Load(); return data.Owned.Contains(itemId); }

    public static void AddOwned(string itemId)
    {
        Load();
        if (data.Owned.Contains(itemId)) return;
        data.Owned.Add(itemId);
        Save();
    }

    /// <summary>Item id worn in a slot (e.g. "Hat"), or null for the starter item.</summary>
    public static string EquippedIn(string slot) { Load(); return data.Equipped.TryGetValue(slot, out var id) ? id : null; }

    public static void Equip(string slot, string itemId)
    {
        Load();
        data.Equipped[slot] = itemId;
        Save();
    }

    public static void AddQuestReward(int coins, int stars = 1)
    {
        Load();
        data.Coins += coins;
        data.Stars += stars;
        data.QuestsCompleted++;
        Save();
        Changed?.Invoke();
    }

    static void Load()
    {
        if (data != null) return;
        data = new Data();
        try
        {
            if (File.Exists(FilePath)) data = JsonConvert.DeserializeObject<Data>(File.ReadAllText(FilePath)) ?? data;
            data.Owned ??= new List<string>();
            data.Equipped ??= new Dictionary<string, string>();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerProgress] Could not read {FilePath}: {e.Message}");
        }
    }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerProgress] Could not save {FilePath}: {e.Message}");
        }
    }
}
