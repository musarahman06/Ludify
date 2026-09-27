using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>One piece of class material the user imported: a file, pasted text or a pasted image.</summary>
    [Serializable]
    public sealed class LibraryItem
    {
        public string Id;
        public string Title;
        /// <summary>"file", "text" or "image".</summary>
        public string Kind;
        /// <summary>Copy kept in the library folder (so it survives the original moving).</summary>
        public string StoredFile;
        public DateTime AddedUtc;
        public string BundleId;
        /// <summary>Question bank made from this item alone (when it was imported).</summary>
        public string QuestionBankId;
    }

    /// <summary>A subject bundle: items grouped by the user; Gemini makes one question set from all of them.</summary>
    [Serializable]
    public sealed class SubjectBundle
    {
        public string Id;
        public string Name;
        public Subject Subject;
        public List<string> ItemIds = new List<string>();
        /// <summary>Question bank generated from the whole bundle (null until generated).</summary>
        public string QuestionBankId;
        public DateTime? GeneratedUtc;
        public int QuestionCount;
        /// <summary>Items changed since the last generation.</summary>
        public bool Stale = true;
    }

    /// <summary>
    /// The user's content library and subject bundles, saved in &lt;persistentDataPath&gt;/Library.
    /// The active bundle sets the UI theme and which questions games use.
    /// </summary>
    public static class LibraryStore
    {
        sealed class Index
        {
            public List<LibraryItem> Items = new List<LibraryItem>();
            public List<SubjectBundle> Bundles = new List<SubjectBundle>();
            public string ActiveBundleId;
        }

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        static Index _index;
        static string _folder;

        /// <summary>Raised (main thread) whenever items, bundles or the active bundle change.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _index = null; Changed = null; }

        public static string Folder => _folder ?? (_folder = Path.Combine(Application.persistentDataPath, "Library"));
        static string FilesFolder => Path.Combine(Folder, "files");
        static string IndexPath => Path.Combine(Folder, "library.json");

        static Index Data
        {
            get
            {
                if (_index != null) return _index;
                try { _index = File.Exists(IndexPath) ? JsonConvert.DeserializeObject<Index>(File.ReadAllText(IndexPath), Settings) : null; }
                catch (Exception e) { Debug.LogWarning("[Ludify.Library] Couldn't read library: " + e.Message); }
                return _index ?? (_index = new Index());
            }
        }

        public static IReadOnlyList<LibraryItem> Items => Data.Items;
        public static IReadOnlyList<SubjectBundle> Bundles => Data.Bundles;
        public static SubjectBundle ActiveBundle => Data.Bundles.FirstOrDefault(b => b.Id == Data.ActiveBundleId);

        public static LibraryItem Item(string id) => Data.Items.FirstOrDefault(i => i.Id == id);
        public static SubjectBundle Bundle(string id) => Data.Bundles.FirstOrDefault(b => b.Id == id);
        public static IEnumerable<LibraryItem> ItemsIn(SubjectBundle b) => b.ItemIds.Select(Item).Where(i => i != null);
        public static IEnumerable<LibraryItem> Unfiled => Data.Items.Where(i => Bundle(i.BundleId ?? "") == null);
        public static string PathOf(LibraryItem item) => Path.Combine(FilesFolder, item.StoredFile);

        static void Save()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(IndexPath, JsonConvert.SerializeObject(Data, Settings));
            QuestionPool.Invalidate();
            Changed?.Invoke();
        }

        static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);

        // ---- Adding material ----

        public static LibraryItem AddFile(string sourcePath)
        {
            string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            string kind = ext == ".png" || ext == ".jpg" || ext == ".jpeg" ? "image" : "file";
            var item = new LibraryItem { Id = NewId(), Title = Path.GetFileNameWithoutExtension(sourcePath), Kind = kind, AddedUtc = DateTime.UtcNow };
            item.StoredFile = item.Id + ext;
            Directory.CreateDirectory(FilesFolder);
            File.Copy(sourcePath, PathOf(item), true);
            Data.Items.Add(item);
            Save();
            return item;
        }

        public static LibraryItem AddText(string text, string title)
        {
            var item = new LibraryItem
            {
                Id = NewId(), Kind = "text", AddedUtc = DateTime.UtcNow,
                Title = string.IsNullOrWhiteSpace(title) ? FirstWords(text) : title.Trim(),
            };
            item.StoredFile = item.Id + ".txt";
            Directory.CreateDirectory(FilesFolder);
            File.WriteAllText(PathOf(item), text);
            Data.Items.Add(item);
            Save();
            return item;
        }

        public static LibraryItem AddImage(byte[] bytes, string extension, string title)
        {
            var item = new LibraryItem { Id = NewId(), Kind = "image", AddedUtc = DateTime.UtcNow, Title = string.IsNullOrWhiteSpace(title) ? "Pasted image" : title };
            item.StoredFile = item.Id + (extension ?? ".png");
            Directory.CreateDirectory(FilesFolder);
            File.WriteAllBytes(PathOf(item), bytes);
            Data.Items.Add(item);
            Save();
            return item;
        }

        static string FirstWords(string text)
        {
            string t = (text ?? "").Trim().Replace('\n', ' ');
            return t.Length <= 40 ? t : t.Substring(0, 40).TrimEnd() + "…";
        }

        // ---- Bundles ----

        public static SubjectBundle CreateBundle(string name, Subject subject)
        {
            var b = new SubjectBundle { Id = NewId(), Name = string.IsNullOrWhiteSpace(name) ? LudifyTheme.For(subject).Name : name.Trim(), Subject = subject };
            Data.Bundles.Add(b);
            Save();
            return b;
        }

        /// <summary>The bundle for a subject, created if there isn't one yet.</summary>
        public static SubjectBundle BundleFor(Subject subject) =>
            Data.Bundles.FirstOrDefault(b => b.Subject == subject) ?? CreateBundle(null, subject);

        /// <summary>Moves an item into a bundle (or back to the library when <paramref name="bundle"/> is null).</summary>
        public static void Move(LibraryItem item, SubjectBundle bundle)
        {
            SubjectBundle from = Bundle(item.BundleId ?? "");
            if (from == bundle) return;
            if (from != null) { from.ItemIds.Remove(item.Id); from.Stale = true; }
            item.BundleId = bundle?.Id;
            if (bundle != null && !bundle.ItemIds.Contains(item.Id)) { bundle.ItemIds.Add(item.Id); bundle.Stale = true; }
            Save();
        }

        /// <summary>After an import: remember the item's own questions and file it under its detected subject.</summary>
        public static void FileImported(LibraryItem item, QuestionBank bank)
        {
            if (item == null || bank == null) return;
            item.QuestionBankId = bank.Id;
            Subject subject = LudifyTheme.Guess(bank.Subject ?? bank.Topic);
            if (Bundle(item.BundleId ?? "") == null) Move(item, BundleFor(subject));
            else Save();
        }

        /// <summary>Moves a bundle to a new position in the list (the order the subjects screen shows).</summary>
        public static void MoveBundle(SubjectBundle b, int index)
        {
            int from = Data.Bundles.IndexOf(b);
            index = Mathf.Clamp(index, 0, Data.Bundles.Count - 1);
            if (from < 0 || from == index) return;
            Data.Bundles.RemoveAt(from);
            Data.Bundles.Insert(index, b);
            Save();
        }

        public static void Rename(SubjectBundle b, string name) { if (!string.IsNullOrWhiteSpace(name)) { b.Name = name.Trim(); Save(); } }

        public static void SetSubject(SubjectBundle b, Subject s)
        {
            b.Subject = s;
            if (Data.ActiveBundleId == b.Id) LudifyTheme.Set(s);
            Save();
        }

        public static void SetActive(SubjectBundle b)
        {
            Data.ActiveBundleId = b?.Id;
            LudifyTheme.Set(b?.Subject ?? Subject.General);
            Save();
        }

        public static void DeleteBundle(SubjectBundle b)
        {
            foreach (LibraryItem i in ItemsIn(b).ToList()) i.BundleId = null;
            if (b.QuestionBankId != null) QuestionBankStore.Delete(b.QuestionBankId);
            Data.Bundles.Remove(b);
            if (Data.ActiveBundleId == b.Id) { Data.ActiveBundleId = null; LudifyTheme.Set(Subject.General); }
            Save();
        }

        public static void DeleteItem(LibraryItem item)
        {
            SubjectBundle b = Bundle(item.BundleId ?? "");
            if (b != null) { b.ItemIds.Remove(item.Id); b.Stale = true; }
            try { if (File.Exists(PathOf(item))) File.Delete(PathOf(item)); } catch { }
            Data.Items.Remove(item);
            Save();
        }

        public static void RecordGenerated(SubjectBundle b, QuestionBank bank)
        {
            if (b.QuestionBankId != null && b.QuestionBankId != bank.Id) QuestionBankStore.Delete(b.QuestionBankId);
            b.QuestionBankId = bank.Id;
            b.QuestionCount = bank.Questions.Count;
            b.GeneratedUtc = DateTime.UtcNow;
            b.Stale = false;
            Save();
        }

        /// <summary>Question banks that belong to a bundle: its generated set, or else its items' own sets.</summary>
        public static List<string> BankIdsFor(SubjectBundle b)
        {
            if (b.QuestionBankId != null && !b.Stale) return new List<string> { b.QuestionBankId };
            var ids = ItemsIn(b).Select(i => i.QuestionBankId).Where(id => id != null).ToList();
            if (b.QuestionBankId != null) ids.Add(b.QuestionBankId);
            return ids;
        }
    }
}
