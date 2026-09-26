using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Personal bests for the time trial, saved as JSON in &lt;persistentDataPath&gt;/TimeTrials/records.json.
/// Kept overall and per question bank (so each imported lecture has its own leaderboard).
/// </summary>
public static class TimeTrialRecords
{
    public const string OverallKey = "overall";

    /// <summary>The circuit is ~800 m, so anything faster than this can't be a genuine lap.</summary>
    public const float MinLapSeconds = 5f;

    [Serializable]
    public sealed class Record
    {
        /// <summary>Best race total in seconds, including penalties (0 = none yet). Race records are kept per lap count.</summary>
        public float BestTotal;
        public string BestTotalDate;
        public float BestLap;
        public string BestLapDate;
        public float LastTotal;
        public int LastCorrect;
        public int LastAsked;
    }

    static Dictionary<string, Record> records;
    static string Folder => Path.Combine(Application.persistentDataPath, "TimeTrials");
    static string FilePath => Path.Combine(Folder, "records.json");

    public static Record Get(string key)
    {
        Load();
        return records.TryGetValue(key ?? OverallKey, out var r) ? r : new Record();
    }

    /// <summary>Records a lap; returns true if it beat the saved best lap.</summary>
    public static bool SubmitLap(string key, float lapSeconds)
    {
        if (lapSeconds < MinLapSeconds) return false;   // not a real lap (e.g. a teleport)
        bool overallBest = SubmitLapTo(OverallKey, lapSeconds);
        bool keyBest = key != null && SubmitLapTo(key, lapSeconds);
        Save();
        return key != null ? keyBest : overallBest;
    }

    /// <summary>Key for race totals, which are only comparable between races of the same length.</summary>
    public static string RaceKey(string key, int laps) => $"{key ?? OverallKey}#{laps}laps";

    /// <summary>Records a finished race; returns true if the total beat the saved best for that race length.</summary>
    public static bool SubmitRace(string key, int laps, float totalSeconds, int correct, int asked)
    {
        bool overallBest = SubmitRaceTo(RaceKey(null, laps), totalSeconds, correct, asked);
        bool keyBest = key != null && SubmitRaceTo(RaceKey(key, laps), totalSeconds, correct, asked);
        Save();
        return key != null ? keyBest : overallBest;
    }

    static bool SubmitLapTo(string key, float lap)
    {
        var r = GetOrCreate(key);
        if (r.BestLap > 0f && lap >= r.BestLap) return false;
        r.BestLap = lap;
        r.BestLapDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return true;
    }

    static bool SubmitRaceTo(string key, float total, int correct, int asked)
    {
        var r = GetOrCreate(key);
        r.LastTotal = total;
        r.LastCorrect = correct;
        r.LastAsked = asked;
        if (r.BestTotal > 0f && total >= r.BestTotal) return false;
        r.BestTotal = total;
        r.BestTotalDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        return true;
    }

    static Record GetOrCreate(string key)
    {
        Load();
        if (!records.TryGetValue(key, out var r)) records[key] = r = new Record();
        return r;
    }

    static void Load()
    {
        if (records != null) return;
        records = new Dictionary<string, Record>();
        try
        {
            if (File.Exists(FilePath))
                records = JsonConvert.DeserializeObject<Dictionary<string, Record>>(File.ReadAllText(FilePath)) ?? records;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TimeTrialRecords] Could not read {FilePath}: {e.Message}");
        }
    }

    static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(records, Formatting.Indented));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TimeTrialRecords] Could not save {FilePath}: {e.Message}");
        }
    }

    public static string Format(float seconds)
    {
        if (seconds <= 0f) return "--:--.---";
        int minutes = (int)(seconds / 60f);
        return $"{minutes}:{seconds - minutes * 60f:00.000}";
    }
}
