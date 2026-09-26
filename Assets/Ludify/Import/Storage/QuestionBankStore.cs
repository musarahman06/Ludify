using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Saves question banks as JSON in &lt;persistentDataPath&gt;/QuestionBanks/&lt;id&gt;.json.
    /// Works identically in the Editor and in Mac/Windows builds.
    /// </summary>
    public static class QuestionBankStore
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        static string _folder;

        /// <summary>First access must be on the main thread (Application.persistentDataPath).</summary>
        public static string Folder => _folder ?? (_folder = Path.Combine(Application.persistentDataPath, "QuestionBanks"));

        public static string PathFor(string id) => Path.Combine(Folder, id + ".json");

        public static bool TryLoad(string id, out QuestionBank bank)
        {
            bank = null;
            string path = PathFor(id);
            if (!File.Exists(path)) return false;
            bank = Deserialize(path);
            return bank != null;
        }

        public static void Save(QuestionBank bank)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(bank.Id), JsonConvert.SerializeObject(bank, Settings));
            QuestionPool.Invalidate();
        }

        /// <summary>All saved banks, newest first.</summary>
        public static List<QuestionBank> LoadAll()
        {
            if (!Directory.Exists(Folder)) return new List<QuestionBank>();
            return Directory.GetFiles(Folder, "*.json")
                .Select(Deserialize)
                .Where(b => b != null)
                .OrderByDescending(b => b.CreatedUtc)
                .ToList();
        }

        public static void Delete(string id)
        {
            string path = PathFor(id);
            if (File.Exists(path)) File.Delete(path);
            QuestionPool.Invalidate();
        }

        public static string ToJson(QuestionBank bank) => JsonConvert.SerializeObject(bank, Settings);

        static QuestionBank Deserialize(string path)
        {
            try { return JsonConvert.DeserializeObject<QuestionBank>(File.ReadAllText(path), Settings); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Ludify.Import] Skipping unreadable question bank {path}: {e.Message}");
                return null;
            }
        }
    }
}
