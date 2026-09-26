using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ludify.Import.Editor
{
    /// <summary>Editor shortcuts for testing the import pipeline without entering Play mode.</summary>
    static class ImportMenu
    {
        const string Root = "Ludify/Import/";

        [MenuItem(Root + "Generate Questions From File…", priority = 0)]
        static void GenerateFromFile() => PickAndRun(forceRegenerate: false);

        [MenuItem(Root + "Regenerate Questions From File (ignore cache)…", priority = 1)]
        static void RegenerateFromFile() => PickAndRun(forceRegenerate: true);

        [MenuItem(Root + "Read File Text Only (no AI)…", priority = 20)]
        static void ReadOnly()
        {
            string path = Pick();
            if (path == null) return;
            try
            {
                LessonContent lesson = LessonReaderFactory.Read(path);
                if (lesson.IsPdf)
                    Debug.Log($"[Ludify.Import] {lesson.FileName}: PDF, {lesson.PdfBytes.Length / 1024} KB. It will be sent to Gemini as-is.");
                else
                    Debug.Log($"[Ludify.Import] {lesson.FileName}: {lesson.Text.Length} characters extracted:\n{lesson.Text}");
            }
            catch (ImportException e) { Debug.LogError("[Ludify.Import] " + e.Message); }
        }

        [MenuItem(Root + "Check Gemini Setup", priority = 40)]
        static async void CheckSetup()
        {
            LlmConfig config = LlmConfig.Load();
            if (!config.HasKey)
            {
                Debug.LogError("[Ludify.Import] " + LlmConfig.SetupHelp);
                return;
            }
            Debug.Log($"[Ludify.Import] Key found via {config.KeySource}. Model: {config.Model}. Checking with Gemini…");
            try
            {
                var models = await new GeminiClient(config.ApiKey, config.Model).ListModelsAsync();
                bool known = models.Contains(config.Model);
                string msg = $"[Ludify.Import] Key works. {models.Count} models available. \"{config.Model}\" " +
                             (known ? "is available." : "is NOT in the list. Pick one below and set \"geminiModel\" in ludify_secrets.json.") +
                             "\nFlash-Lite models:\n  " + string.Join("\n  ", models.Where(m => m.Contains("lite")));
                if (known) Debug.Log(msg); else Debug.LogWarning(msg);
            }
            catch (ImportException e) { Debug.LogError("[Ludify.Import] " + e.Message); }
        }

        [MenuItem(Root + "Open Question Bank Folder", priority = 41)]
        static void OpenFolder()
        {
            Directory.CreateDirectory(QuestionBankStore.Folder);
            EditorUtility.RevealInFinder(QuestionBankStore.Folder);
        }

        static async void PickAndRun(bool forceRegenerate)
        {
            string path = Pick();
            if (path == null) return;

            var progress = new Progress<string>(s => Debug.Log("[Ludify.Import] " + s));
            try
            {
                QuestionBank bank = await new LessonImporter().ImportAsync(path, progress, forceRegenerate);
                Debug.Log(Describe(bank));
            }
            catch (ImportException e) { Debug.LogError("[Ludify.Import] " + e.Message); }
            catch (Exception e) { Debug.LogException(e); }
        }

        static string Pick()
        {
            string start = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SampleLectures");
            if (!Directory.Exists(start)) start = "";
            string filter = string.Join(",", LessonReaderFactory.SupportedExtensions);
            string path = EditorUtility.OpenFilePanelWithFilters("Choose a lecture file", start,
                new[] { "Lecture files", filter, "All files", "*" });
            return string.IsNullOrEmpty(path) ? null : path;
        }

        static string Describe(QuestionBank bank)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Ludify.Import] \"{bank.Topic}\" from {bank.SourceFileName}: {bank.Questions.Count} questions, {bank.Sources.Count} web sources");
            sb.AppendLine(bank.Summary);
            sb.AppendLine("Key concepts: " + string.Join(", ", bank.KeyConcepts));
            foreach (McQuestion q in bank.Questions)
            {
                sb.AppendLine($"\n[{q.Id}] (d{q.Difficulty}, {q.Concept}) {q.Prompt}");
                for (int i = 0; i < q.Choices.Length; i++)
                    sb.AppendLine($"   {(i == q.CorrectIndex ? "*" : " ")} {(char)('A' + i)}. {q.Choices[i]}");
                sb.AppendLine("   → " + q.Explanation);
            }
            if (bank.Sources.Count > 0)
                sb.AppendLine("\nSources:\n  " + string.Join("\n  ", bank.Sources.Select(s => $"{s.Title}: {s.Url}")));
            return sb.ToString();
        }
    }
}
