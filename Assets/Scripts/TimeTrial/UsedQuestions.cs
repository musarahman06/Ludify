using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Remembers which questions of each imported lecture have already been asked, so the time trial never reuses one.
/// Saved as JSON in &lt;persistentDataPath&gt;/TimeTrials/used_questions.json (question bank id → question ids).
/// </summary>
public static class UsedQuestions
{
    static Dictionary<string, HashSet<string>> used;
    static string FilePath => Path.Combine(Application.persistentDataPath, "TimeTrials", "used_questions.json");

    public static IReadOnlyCollection<string> For(string bankId)
    {
        Load();
        return bankId != null && used.TryGetValue(bankId, out var set) ? set : (IReadOnlyCollection<string>)Array.Empty<string>();
    }

    public static void MarkUsed(string bankId, string questionId)
    {
        if (bankId == null || questionId == null) return;
        Load();
        if (!used.TryGetValue(bankId, out var set)) used[bankId] = set = new HashSet<string>();
        if (set.Add(questionId)) Save();
    }

    static void Load()
    {
        if (used != null) return;
        used = new Dictionary<string, HashSet<string>>();
        try
        {
            if (File.Exists(FilePath))
                used = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(File.ReadAllText(FilePath)) ?? used;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UsedQuestions] Could not read {FilePath}: {e.Message}");
        }
    }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(used, Formatting.Indented));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UsedQuestions] Could not save {FilePath}: {e.Message}");
        }
    }
}
