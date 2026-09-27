using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Public entry point: lesson file → research (with web search) → multiple-choice question bank.
    /// Results are cached by file contents, so importing the same file again costs no API calls.
    /// Call from the main thread.
    /// </summary>
    public sealed class LessonImporter
    {
        /// <summary>Bump when prompts/schema change so old cached banks get regenerated.</summary>
        const string PipelineVersion = "mcq-v1";

        readonly LlmConfig _config;

        public LessonImporter(LlmConfig config = null) => _config = config ?? LlmConfig.Load();

        public LlmConfig Config => _config;

        /// <param name="progress">Short status lines for the UI.</param>
        /// <param name="forceRegenerate">Ignore the cache and call the LLM again.</param>
        public async Task<QuestionBank> ImportAsync(string path, IProgress<string> progress = null,
                                                    bool forceRegenerate = false, CancellationToken ct = default)
        {
            progress?.Report($"Reading {Path.GetFileName(path)}…");
            _ = QuestionBankStore.Folder; // resolve persistentDataPath on the main thread before going async

            // File reading/hashing is pure .NET, so do it off the main thread to keep the game responsive.
            var (lesson, id) = await Task.Run(() =>
            {
                LessonContent content = LessonReaderFactory.Read(path);
                return (content, ComputeId(path));
            }, ct);

            return await ImportContentAsync(lesson, id, progress, forceRegenerate, ct);
        }

        /// <summary>Lecture notes pasted as text (e.g. copied from a PDF, Word doc or slides).</summary>
        public Task<QuestionBank> ImportTextAsync(string text, string title = null, IProgress<string> progress = null,
                                                  bool forceRegenerate = false, CancellationToken ct = default)
        {
            text = text?.Trim() ?? "";
            if (text.Length < 40)
                throw new ImportException("Paste a bit more of the lecture (at least a few sentences) so there's something to ask about.");
            _ = QuestionBankStore.Folder;
            var lesson = new LessonContent { FileName = string.IsNullOrWhiteSpace(title) ? "Pasted notes" : title.Trim(), Text = text };
            return ImportContentAsync(lesson, ComputeId(Encoding.UTF8.GetBytes(text)), progress, forceRegenerate, ct);
        }

        /// <summary>A pasted picture of lecture material (e.g. a Snipping Tool screenshot of a slide).</summary>
        public Task<QuestionBank> ImportImageAsync(byte[] image, string mimeType, string title = null, IProgress<string> progress = null,
                                                   bool forceRegenerate = false, CancellationToken ct = default)
        {
            if (image == null || image.Length == 0) throw new ImportException("There's no image to import.");
            _ = QuestionBankStore.Folder;
            var lesson = new LessonContent
            {
                FileName = string.IsNullOrWhiteSpace(title) ? "Pasted image" : title.Trim(),
                ImageBytes = image, ImageMimeType = mimeType ?? "image/png",
            };
            return ImportContentAsync(lesson, ComputeId(image), progress, forceRegenerate, ct);
        }

        async Task<QuestionBank> ImportContentAsync(LessonContent lesson, string id, IProgress<string> progress,
                                                    bool forceRegenerate, CancellationToken ct)
        {
            if (!forceRegenerate && QuestionBankStore.TryLoad(id, out QuestionBank cached))
            {
                progress?.Report($"Loaded {cached.Questions.Count} saved questions (no AI calls needed).");
                return cached;
            }

            if (!_config.HasKey) throw new ImportException(LlmConfig.SetupHelp);
            var client = new GeminiClient(_config.ApiKey, _config.Model);

            progress?.Report("Researching the topic…");
            ResearchNotes research = await new LessonResearcher(client, _config.UseWebResearch).ResearchAsync(lesson, ct);

            progress?.Report($"Writing {_config.QuestionsPerFile} practice questions…");
            GeneratedQuestions generated = await new QuestionGenerator(client)
                .GenerateAsync(lesson, research, _config.QuestionsPerFile, ct);

            var bank = new QuestionBank
            {
                Id = id,
                SourceFileName = lesson.FileName,
                CreatedUtc = DateTime.UtcNow,
                Model = _config.Model,
                Topic = generated.Topic,
                Summary = generated.Summary,
                KeyConcepts = generated.KeyConcepts,
                Sources = research.Sources,
                Questions = generated.Questions,
            };
            QuestionBankStore.Save(bank);
            Debug.Log($"[Ludify.Import] Saved {bank.Questions.Count} questions on \"{bank.Topic}\" to {QuestionBankStore.PathFor(id)}");

            progress?.Report($"Done: {bank.Questions.Count} questions on \"{bank.Topic}\".");
            return bank;
        }

        string ComputeId(string path) => ComputeId(File.ReadAllBytes(path));

        string ComputeId(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] fileHash = sha.ComputeHash(data);
                string settings = $"|{PipelineVersion}|{_config.Model}|{_config.QuestionsPerFile}|{_config.UseWebResearch}";
                byte[] combined = sha.ComputeHash(Combine(fileHash, Encoding.UTF8.GetBytes(settings)));
                return BitConverter.ToString(combined, 0, 12).Replace("-", "").ToLowerInvariant();
            }
        }

        static byte[] Combine(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
