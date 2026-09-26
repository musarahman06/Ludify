using System;
using System.Collections.Generic;

namespace Ludify.Import
{
    /// <summary>A set of practice questions generated from one imported lesson file.</summary>
    [Serializable]
    public sealed class QuestionBank
    {
        public int SchemaVersion = 1;

        /// <summary>Cache key: hash of the file contents + generation settings.</summary>
        public string Id;
        public string SourceFileName;
        public DateTime CreatedUtc;
        public string Model;

        public string Topic;
        public string Summary;
        public List<string> KeyConcepts = new List<string>();

        /// <summary>Web pages the research step used (from Google Search grounding).</summary>
        public List<SourceLink> Sources = new List<SourceLink>();
        public List<McQuestion> Questions = new List<McQuestion>();
    }

    /// <summary>A multiple-choice question: exactly 4 choices, one correct.</summary>
    [Serializable]
    public sealed class McQuestion
    {
        public string Id;
        public string Prompt;
        public string[] Choices;
        public int CorrectIndex;
        public string Explanation;

        /// <summary>1 = recall, 2 = understanding, 3 = application.</summary>
        public int Difficulty;
        public string Concept;

        [Newtonsoft.Json.JsonIgnore]
        public string CorrectChoice => Choices[CorrectIndex];

        public bool IsCorrect(int choiceIndex) => choiceIndex == CorrectIndex;
    }

    [Serializable]
    public sealed class SourceLink
    {
        public string Title;
        public string Url;
    }
}
