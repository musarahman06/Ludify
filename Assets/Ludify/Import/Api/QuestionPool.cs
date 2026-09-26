using System.Collections.Generic;
using System.Linq;

namespace Ludify.Import
{
    /// <summary>
    /// One shared deck containing the questions from every saved question bank, so games
    /// (fast travel, racing, …) draw from the same pool without repeating questions.
    /// Rebuilt automatically when banks are saved or deleted.
    /// </summary>
    public static class QuestionPool
    {
        static QuestionDeck _deck;
        static bool _loaded;

        public static bool HasQuestions => Deck != null;

        /// <summary>Null when no lecture has been imported yet.</summary>
        public static QuestionDeck Deck
        {
            get
            {
                if (!_loaded) Load();
                return _deck;
            }
        }

        /// <summary>Called by <see cref="QuestionBankStore"/> when banks change.</summary>
        public static void Invalidate() => _loaded = false;

        static void Load()
        {
            _loaded = true;
            List<QuestionBank> banks = QuestionBankStore.LoadAll();
            var questions = banks.SelectMany(b => b.Questions ?? new List<McQuestion>()).ToList();
            if (questions.Count == 0)
            {
                _deck = null;
                return;
            }

            var combined = new QuestionBank
            {
                Id = "pool",
                Topic = string.Join(", ", banks.Select(b => b.Topic).Where(t => !string.IsNullOrEmpty(t)).Distinct()),
                Questions = questions,
            };
            _deck = new QuestionDeck(combined);
        }
    }
}
