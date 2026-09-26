using System;
using System.Collections.Generic;
using System.Linq;

namespace Ludify.Import
{
    /// <summary>
    /// What gameplay uses: draws questions from a bank in shuffled order without repeats,
    /// and brings missed questions back sooner.
    /// <code>
    /// var deck = new QuestionDeck(QuestionBankStore.LoadAll()[0]);
    /// McQuestion q = deck.Next();                 // show q.Prompt and q.Choices
    /// bool correct = deck.RecordAnswer(q, index); // e.g. speed boost if correct
    /// </code>
    /// </summary>
    public sealed class QuestionDeck
    {
        /// <summary>A missed question comes back after this many other questions.</summary>
        const int RetryGap = 3;

        readonly List<McQuestion> _all;
        readonly Random _random;
        readonly List<McQuestion> _drawPile = new List<McQuestion>();
        readonly List<(McQuestion Question, int DueIn)> _missed = new List<(McQuestion, int)>();

        public QuestionBank Bank { get; }
        public int Answered { get; private set; }
        public int Correct { get; private set; }
        public int Count => _all.Count;

        public QuestionDeck(QuestionBank bank, int? seed = null)
        {
            Bank = bank ?? throw new ArgumentNullException(nameof(bank));
            _all = bank.Questions?.ToList() ?? new List<McQuestion>();
            if (_all.Count == 0) throw new ArgumentException("Question bank has no questions.", nameof(bank));
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>Next question. Optionally restricted to a difficulty (falls back to any if none match).</summary>
        public McQuestion Next(int? difficulty = null)
        {
            for (int i = 0; i < _missed.Count; i++)
            {
                if (_missed[i].DueIn > 0) continue;
                McQuestion due = _missed[i].Question;
                _missed.RemoveAt(i);
                return due;
            }

            if (_drawPile.Count == 0) Refill();
            int index = difficulty.HasValue ? _drawPile.FindIndex(q => q.Difficulty == difficulty.Value) : -1;
            if (index < 0) index = 0;
            McQuestion next = _drawPile[index];
            _drawPile.RemoveAt(index);
            return next;
        }

        /// <summary>Records an answer and returns whether it was correct.</summary>
        public bool RecordAnswer(McQuestion question, int choiceIndex)
        {
            bool correct = question.IsCorrect(choiceIndex);
            Answered++;
            if (correct) Correct++;

            for (int i = 0; i < _missed.Count; i++)
                _missed[i] = (_missed[i].Question, _missed[i].DueIn - 1);
            if (!correct && !_missed.Any(m => m.Question == question))
                _missed.Add((question, RetryGap));
            return correct;
        }

        void Refill()
        {
            _drawPile.AddRange(_all.Where(q => !_missed.Any(m => m.Question == q)));
            if (_drawPile.Count == 0) _drawPile.AddRange(_all);
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }
    }
}
