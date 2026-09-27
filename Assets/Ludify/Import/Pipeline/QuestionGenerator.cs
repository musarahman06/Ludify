using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ludify.Import
{
    public sealed class GeneratedQuestions
    {
        public string Topic;
        public string Summary;
        public List<string> KeyConcepts = new List<string>();
        public List<McQuestion> Questions = new List<McQuestion>();
    }

    /// <summary>
    /// Step 2: write multiple-choice questions as strict JSON (responseSchema), then validate.
    /// Kept separate from research because Google Search grounding and forced JSON output
    /// don't reliably combine in one request.
    /// </summary>
    public sealed class QuestionGenerator
    {
        const string SystemPrompt =
            "You write high-quality multiple-choice practice questions for students. " +
            "Questions are shown inside fast-paced video games, so they must be short and answerable in a few seconds.";

        const string Instructions =
            "Write {0} multiple-choice practice questions based on the class material and the research notes below.\n" +
            "Rules:\n" +
            "- Test mainly the content of the class material. Use the research notes for depth, examples and plausible wrong answers. " +
            "Never contradict the class material.\n" +
            "- prompt: at most 120 characters. One clear question, no multi-part questions.\n" +
            "- choices: exactly 4, each at most 40 characters, exactly one correct. No \"all of the above\" or \"none of the above\".\n" +
            "- Wrong choices must be plausible; use the common misconceptions.\n" +
            "- correctIndex: 0-based index of the correct choice.\n" +
            "- difficulty: 1 = recall, 2 = understanding, 3 = application. Aim for about 40% 1s, 40% 2s, 20% 3s.\n" +
            "- explanation: one short sentence saying why the answer is correct.\n" +
            "- concept: the short name of the key concept being tested.\n" +
            "- Cover all key concepts. No duplicate or near-duplicate questions.\n" +
            "Also return: topic (short title), summary (2-3 sentences for students), keyConcepts (short names).\n\n" +
            "=== RESEARCH NOTES ===\n{1}\n=== END RESEARCH NOTES ===";

        static readonly JObject Schema = JObject.Parse(@"{
          'type': 'OBJECT',
          'properties': {
            'topic': { 'type': 'STRING' },
            'summary': { 'type': 'STRING' },
            'keyConcepts': { 'type': 'ARRAY', 'items': { 'type': 'STRING' } },
            'questions': {
              'type': 'ARRAY',
              'items': {
                'type': 'OBJECT',
                'properties': {
                  'prompt': { 'type': 'STRING' },
                  'choices': { 'type': 'ARRAY', 'items': { 'type': 'STRING' } },
                  'correctIndex': { 'type': 'INTEGER' },
                  'explanation': { 'type': 'STRING' },
                  'difficulty': { 'type': 'INTEGER' },
                  'concept': { 'type': 'STRING' }
                },
                'required': ['prompt', 'choices', 'correctIndex', 'explanation', 'difficulty', 'concept'],
                'propertyOrdering': ['prompt', 'choices', 'correctIndex', 'explanation', 'difficulty', 'concept']
              }
            }
          },
          'required': ['topic', 'summary', 'keyConcepts', 'questions'],
          'propertyOrdering': ['topic', 'summary', 'keyConcepts', 'questions']
        }");

        readonly GeminiClient _client;
        readonly System.Random _random = new System.Random();

        public QuestionGenerator(GeminiClient client) => _client = client;

        public async Task<GeneratedQuestions> GenerateAsync(LessonContent lesson, ResearchNotes research, int count, CancellationToken ct)
        {
            GeneratedQuestions result = await GenerateOnceAsync(lesson, research, count, ct);

            // One retry if the model returned broken JSON or too few usable questions.
            if (result == null || result.Questions.Count < count / 2)
            {
                Debug.LogWarning($"[Ludify.Import] Only {result?.Questions.Count ?? 0}/{count} usable questions, retrying once.");
                GeneratedQuestions retry = await GenerateOnceAsync(lesson, research, count, ct);
                if (result == null) result = retry;
                else if (retry != null)
                    result.Questions = QuestionValidator.Clean(result.Questions.Concat(retry.Questions), _random);
            }

            if (result == null || result.Questions.Count == 0)
                throw new ImportException("Gemini didn't produce usable questions for this file. Please try again.");
            return result;
        }

        async Task<GeneratedQuestions> GenerateOnceAsync(LessonContent lesson, ResearchNotes research, int count, CancellationToken ct)
        {
            string notes = string.IsNullOrWhiteSpace(research?.Text) ? "(none)" : research.Text;
            GeminiResult raw = await _client.GenerateAsync(new GeminiRequest
            {
                SystemInstruction = SystemPrompt,
                Text = LessonPrompt.Append(string.Format(Instructions, count, notes), lesson),
                PdfBytes = lesson.PdfBytes,
                ImageBytes = lesson.ImageBytes,
                ImageMimeType = lesson.ImageMimeType,
                ResponseSchema = Schema,
                Temperature = 0.7f,
            }, ct);

            GeneratedQuestions parsed;
            try { parsed = JsonConvert.DeserializeObject<GeneratedQuestions>(raw.Text); }
            catch (JsonException e)
            {
                Debug.LogWarning($"[Ludify.Import] Couldn't parse questions JSON ({raw.FinishReason}): {e.Message}");
                return null;
            }
            if (parsed == null) return null;

            var rejected = new List<string>();
            parsed.Questions = QuestionValidator.Clean(parsed.Questions ?? new List<McQuestion>(), _random, rejected);
            if (rejected.Count > 0)
                Debug.Log($"[Ludify.Import] Dropped {rejected.Count} question(s):\n  " + string.Join("\n  ", rejected));
            parsed.KeyConcepts = parsed.KeyConcepts ?? new List<string>();
            return parsed;
        }
    }
}
