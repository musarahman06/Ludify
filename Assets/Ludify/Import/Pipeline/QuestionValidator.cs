using System;
using System.Collections.Generic;
using System.Linq;

namespace Ludify.Import
{
    /// <summary>
    /// Drops malformed LLM questions and shuffles answer order (LLMs over-favour putting
    /// the correct answer first, which students learn to exploit).
    /// </summary>
    public static class QuestionValidator
    {
        public const int ChoiceCount = 4;
        public const int MaxPromptLength = 200;  // asked for ≤120; allow slack before rejecting
        public const int MaxChoiceLength = 80;   // asked for ≤40

        public static List<McQuestion> Clean(IEnumerable<McQuestion> questions, Random random, List<string> rejectReasons = null)
        {
            var result = new List<McQuestion>();
            var seenPrompts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (McQuestion q in questions)
            {
                string reason = Check(q);
                if (reason == null && !seenPrompts.Add(Normalize(q.Prompt))) reason = "duplicate question";
                if (reason != null)
                {
                    rejectReasons?.Add($"{reason}: {q?.Prompt}");
                    continue;
                }

                q.Prompt = q.Prompt.Trim();
                q.Choices = q.Choices.Select(c => c.Trim()).ToArray();
                q.Difficulty = Math.Max(1, Math.Min(3, q.Difficulty));
                q.Explanation = q.Explanation?.Trim() ?? "";
                q.Concept = q.Concept?.Trim() ?? "";
                ShuffleChoices(q, random);
                result.Add(q);
            }

            for (int i = 0; i < result.Count; i++) result[i].Id = "q" + (i + 1);
            return result;
        }

        static string Check(McQuestion q)
        {
            if (q == null || string.IsNullOrWhiteSpace(q.Prompt)) return "empty prompt";
            if (q.Prompt.Length > MaxPromptLength) return "prompt too long";
            if (q.Choices == null || q.Choices.Length != ChoiceCount) return "not exactly 4 choices";
            if (q.Choices.Any(string.IsNullOrWhiteSpace)) return "empty choice";
            if (q.Choices.Any(c => c.Length > MaxChoiceLength)) return "choice too long";
            if (q.Choices.Select(Normalize).Distinct().Count() != ChoiceCount) return "duplicate choices";
            if (q.CorrectIndex < 0 || q.CorrectIndex >= ChoiceCount) return "correct answer index out of range";
            string lowered = string.Join("|", q.Choices).ToLowerInvariant();
            if (lowered.Contains("all of the above") || lowered.Contains("none of the above")) return "uses all/none of the above";
            return null;
        }

        static void ShuffleChoices(McQuestion q, Random random)
        {
            string correct = q.Choices[q.CorrectIndex];
            for (int i = q.Choices.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (q.Choices[i], q.Choices[j]) = (q.Choices[j], q.Choices[i]);
            }
            q.CorrectIndex = Array.IndexOf(q.Choices, correct);
        }

        static string Normalize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
