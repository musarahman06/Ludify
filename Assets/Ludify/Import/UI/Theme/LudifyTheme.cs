using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ludify.Import
{
    public enum Subject { General, Science, Math, History, Language, Geography, ComputerScience, Art, Music }

    /// <summary>Colours for one subject. Panels stay the reference cream; the accent and symbols change.</summary>
    public sealed class SubjectPalette
    {
        public Subject Subject;
        public string Name;
        public Color Accent;
        /// <summary>Sandy cream panel background (warm grey-beige, not white).</summary>
        public Color Panel = new Color(0.89f, 0.85f, 0.77f, 1f);
        /// <summary>Main text on panels (warm dark brown, like the reference).</summary>
        public Color Text = new Color(0.33f, 0.25f, 0.19f, 1f);
        public Color MutedText = new Color(0.52f, 0.44f, 0.37f, 1f);
        /// <summary>Neutral button (the reference's charcoal "settings" button).</summary>
        public Color Neutral = new Color(0.36f, 0.35f, 0.34f, 1f);
        /// <summary>Recessed areas such as input boxes and list backgrounds.</summary>
        public Color Inset = new Color(0.81f, 0.76f, 0.67f, 1f);
        /// <summary>Text on dark or coloured backgrounds: a soft cream instead of pure white.</summary>
        public Color LightText = new Color(0.98f, 0.95f, 0.89f, 1f);

        public Color AccentDark => Color.Lerp(Accent, Color.black, 0.22f);
        public Color AccentLight => Color.Lerp(Accent, Color.white, 0.55f);
    }

    /// <summary>
    /// The game-wide UI theme: the reference's cream panels and chunky buttons, coloured by the current
    /// subject (science = green, math = blue, …) with matching symbols. <see cref="UiKit"/> reads it, so
    /// every screen built with UiKit follows it; <see cref="Changed"/> recolours them live.
    /// </summary>
    public static class LudifyTheme
    {
        const string Pref = "Ludify.Theme.Subject";

        /// <summary>The reference image's button colours, used for multi-button menus.</summary>
        public static readonly Color[] MenuColors =
        {
            new Color(0.36f, 0.73f, 0.46f), new Color(0.91f, 0.39f, 0.33f), new Color(0.95f, 0.73f, 0.21f),
            new Color(0.36f, 0.35f, 0.34f), new Color(0.29f, 0.56f, 0.88f),
        };

        static readonly Dictionary<Subject, SubjectPalette> Palettes = new Dictionary<Subject, SubjectPalette>
        {
            [Subject.General] = new SubjectPalette { Subject = Subject.General, Name = "General", Accent = new Color(0.29f, 0.56f, 0.88f) },
            [Subject.Science] = new SubjectPalette { Subject = Subject.Science, Name = "Science", Accent = new Color(0.29f, 0.68f, 0.41f) },
            [Subject.Math] = new SubjectPalette { Subject = Subject.Math, Name = "Math", Accent = new Color(0.26f, 0.5f, 0.88f) },
            [Subject.History] = new SubjectPalette { Subject = Subject.History, Name = "History", Accent = new Color(0.78f, 0.53f, 0.2f) },
            [Subject.Language] = new SubjectPalette { Subject = Subject.Language, Name = "Language", Accent = new Color(0.9f, 0.4f, 0.37f) },
            [Subject.Geography] = new SubjectPalette { Subject = Subject.Geography, Name = "Geography", Accent = new Color(0.14f, 0.6f, 0.58f) },
            [Subject.ComputerScience] = new SubjectPalette { Subject = Subject.ComputerScience, Name = "Computer Science", Accent = new Color(0.49f, 0.34f, 0.77f) },
            [Subject.Art] = new SubjectPalette { Subject = Subject.Art, Name = "Art", Accent = new Color(0.9f, 0.4f, 0.64f) },
            [Subject.Music] = new SubjectPalette { Subject = Subject.Music, Name = "Music", Accent = new Color(0.35f, 0.41f, 0.76f) },
        };

        static Subject? _current;

        /// <summary>Raised on the main thread when the subject (and so the colours) change.</summary>
        public static event Action Changed;

        public static Subject Current
        {
            get
            {
                if (_current == null)
                {
                    try { _current = (Subject)Mathf.Clamp(PlayerPrefs.GetInt(Pref, 0), 0, Palettes.Count - 1); }
                    catch { _current = Subject.General; }
                }
                return _current.Value;
            }
        }

        public static SubjectPalette Palette => Palettes[Current];
        public static SubjectPalette For(Subject s) => Palettes[s];
        public static IEnumerable<Subject> All => Palettes.Keys;

        public static void Set(Subject subject)
        {
            if (_current == subject) return;
            _current = subject;
            try { PlayerPrefs.SetInt(Pref, (int)subject); PlayerPrefs.Save(); } catch { }
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _current = null;
            Changed = null;
        }

        /// <summary>Best-guess subject from free text (a lecture's topic, Gemini's "subject" field…).</summary>
        public static Subject Guess(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Subject.General;
            string t = " " + text.ToLowerInvariant() + " ";
            bool Has(params string[] words) { foreach (string w in words) if (t.Contains(w)) return true; return false; }
            if (Has("math", "algebra", "geometry", "calculus", "fraction", "equation", "arithmetic", "statistic", "trigonometr", "number"))
                return Subject.Math;
            if (Has("computer", "programming", "coding", "algorithm", "software", " code", "python", "java", "data structure"))
                return Subject.ComputerScience;
            if (Has("science", "biology", "chemistry", "physics", "cell", "atom", "molecule", "circuit", "electric", "energy",
                    "planet", "solar", "ecosystem", "photosynthesis", "gene", "dna", "force", "gear", "mechanism", "astronomy"))
                return Subject.Science;
            if (Has("history", "war", "empire", "ancient", "revolution", "civilization", "dynasty", "century", "historical"))
                return Subject.History;
            if (Has("geography", "continent", "climate", "river", "mountain", "country", "map", "population", "water cycle"))
                return Subject.Geography;
            if (Has("english", "grammar", "literature", "poem", "poetry", "novel", "writing", "language", "comma", "vocabulary", "spanish", "french"))
                return Subject.Language;
            if (Has(" art", "painting", "drawing", "sculpture", "artist", "color theory"))
                return Subject.Art;
            if (Has("music", "rhythm", "melody", "chord", "instrument", "song"))
                return Subject.Music;
            return Subject.General;
        }
    }
}
