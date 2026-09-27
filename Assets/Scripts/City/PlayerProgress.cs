using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Coins and helper stars earned from city quests, saved as JSON in
/// &lt;persistentDataPath&gt;/Progress/player_progress.json.
/// </summary>
public static class PlayerProgress
{
    [Serializable]
    sealed class Data
    {
        public int Coins;
        public int Stars;
        public int QuestsCompleted;
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
