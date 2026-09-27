using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ludify.Import
{
    public sealed class ResearchNotes
    {
        public string Text;
        public List<SourceLink> Sources = new List<SourceLink>();
        public bool UsedWebSearch;
    }

    /// <summary>
    /// Step 1: read the lesson and use Google Search (grounding) to gather extra facts,
    /// examples and common misconceptions for writing better questions.
    /// </summary>
    public sealed class LessonResearcher
    {
        const string SystemPrompt =
            "You are a teaching assistant helping a teacher turn their class materials into practice questions. " +
            "Be accurate. Stay strictly on the topics covered in the class material.";

        const string Instructions =
            "Read the class material (file: \"{0}\"). Identify the subject and the specific topics it covers. " +
            "{1}\n\n" +
            "Write research notes in plain text with exactly these sections:\n" +
            "TOPIC: <one line>\n" +
            "KEY CONCEPTS:\n- <concept>: <one-line definition>\n" +
            "IMPORTANT FACTS:\n- <facts from the material, plus supporting facts and concrete examples>\n" +
            "COMMON MISCONCEPTIONS:\n- <mistakes students typically make on these topics>\n\n" +
            "Keep it under 800 words.";

        const string WebHint =
            "Use Google Search to find accurate supporting information on those topics: definitions, key facts, " +
            "real-world examples, and common student misconceptions.";

        const string KnowledgeHint =
            "Using your own knowledge, add accurate supporting information on those topics: definitions, key facts, " +
            "real-world examples, and common student misconceptions.";

        /// <summary>
        /// Google Search grounding needs a billing-enabled key (free keys get a quota of 0).
        /// Once it fails, skip it for the rest of the session.
        /// </summary>
        static bool s_webUnavailable;

        readonly GeminiClient _client;
        readonly bool _useWeb;

        public LessonResearcher(GeminiClient client, bool useWeb)
        {
            _client = client;
            _useWeb = useWeb;
        }

        public async Task<ResearchNotes> ResearchAsync(LessonContent lesson, CancellationToken ct)
        {
            if (_useWeb && !s_webUnavailable)
            {
                try { return await RunAsync(lesson, web: true, ct); }
                catch (GeminiException e) when (e.StatusCode == 429)
                {
                    s_webUnavailable = true;
                    Debug.LogWarning("[Ludify.Import] Google Search isn't available on this Gemini key (it needs billing enabled). " +
                                     "Researching with the model's own knowledge instead.");
                }
            }
            return await RunAsync(lesson, web: false, ct);
        }

        async Task<ResearchNotes> RunAsync(LessonContent lesson, bool web, CancellationToken ct)
        {
            string prompt = string.Format(Instructions, lesson.FileName, web ? WebHint : KnowledgeHint);
            GeminiResult result = await _client.GenerateAsync(new GeminiRequest
            {
                SystemInstruction = SystemPrompt,
                Text = LessonPrompt.Append(prompt, lesson),
                PdfBytes = lesson.PdfBytes,
                ImageBytes = lesson.ImageBytes,
                ImageMimeType = lesson.ImageMimeType,
                UseGoogleSearch = web,
                Temperature = 0.3f,
            }, ct);

            return new ResearchNotes { Text = result.Text?.Trim() ?? "", Sources = result.Sources, UsedWebSearch = web };
        }
    }

    internal static class LessonPrompt
    {
        /// <summary>Roughly 40k tokens. Plenty for a lecture while staying well inside free-tier limits.</summary>
        public const int MaxLessonChars = 160_000;

        /// <summary>Appends the lesson text (unless it's a PDF, which is attached separately).</summary>
        public static string Append(string instructions, LessonContent lesson)
        {
            if (lesson.IsPdf) return instructions + "\n\nThe class material is the attached PDF.";
            if (lesson.IsImage) return instructions + "\n\nThe class material is the attached image (e.g. a slide or notes).";

            string text = lesson.Text;
            if (text.Length > MaxLessonChars)
                text = text.Substring(0, MaxLessonChars) + "\n[...material truncated...]";
            return instructions + "\n\n=== CLASS MATERIAL START ===\n" + text + "\n=== CLASS MATERIAL END ===";
        }
    }
}
