using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Developer test UI for the import pipeline (IMGUI, no scene setup needed):
    /// pick a lecture file at runtime → generate questions → take the quiz.
    /// Not the final teacher-facing UI.
    /// </summary>
    public sealed class ImportTestPanel : MonoBehaviour
    {
        const float DesignHeight = 800f;

        LessonImporter _importer;
        CancellationTokenSource _cts;
        List<QuestionBank> _banks = new List<QuestionBank>();
        QuestionDeck _deck;
        McQuestion _question;
        int _chosen = -1;
        bool _busy;
        bool _forceRegenerate;
        string _status = "";
        Vector2 _scroll;
        GUIStyle _wrap, _title, _big;

        void Awake()
        {
            _cts = new CancellationTokenSource();
            _importer = new LessonImporter();
            _banks = QuestionBankStore.LoadAll();
            _status = _importer.Config.HasKey
                ? $"Ready. Model: {_importer.Config.Model}. Saved banks: {_banks.Count}."
                : LlmConfig.SetupHelp;
        }

        void OnDestroy() => _cts?.Cancel();

        void OpenFileBrowser() => LessonFilePicker.Show(Import);

        async void Import(string path)
        {
            _busy = true;
            try
            {
                var progress = new Progress<string>(s => _status = s);
                QuestionBank bank = await _importer.ImportAsync(path, progress, _forceRegenerate, _cts.Token);
                _banks = QuestionBankStore.LoadAll();
                StartQuiz(bank);
            }
            catch (OperationCanceledException) { }
            catch (ImportException e) { _status = e.Message; }
            catch (Exception e)
            {
                _status = "Unexpected error: " + e.Message;
                Debug.LogException(e);
            }
            finally { _busy = false; }
        }

        void StartQuiz(QuestionBank bank)
        {
            _deck = new QuestionDeck(bank);
            NextQuestion();
        }

        void NextQuestion()
        {
            _question = _deck.Next();
            _chosen = -1;
        }

        void OnGUI()
        {
            if (LessonFilePicker.IsOpen) return;
            InitStyles();

            float scale = Screen.height / DesignHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;

            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(900, width - 40), DesignHeight - 40), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Ludify: Lecture Import Test", _title);
            GUILayout.Label(_status, _wrap);

            GUI.enabled = !_busy;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Import lecture file…", GUILayout.Height(40), GUILayout.Width(260))) OpenFileBrowser();
            _forceRegenerate = GUILayout.Toggle(_forceRegenerate, " Regenerate (ignore saved questions)");
            GUILayout.EndHorizontal();

            if (_deck == null) DrawBankList();
            else DrawQuiz();

            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawBankList()
        {
            GUILayout.Space(10);
            GUILayout.Label(_banks.Count == 0 ? "No saved question banks yet." : "Saved question banks:", _big);
            foreach (QuestionBank bank in _banks)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"{bank.Topic}  ({bank.Questions.Count} questions, from {bank.SourceFileName})", GUILayout.Height(32)))
                    StartQuiz(bank);
                if (GUILayout.Button("Delete", GUILayout.Width(80), GUILayout.Height(32)))
                {
                    QuestionBankStore.Delete(bank.Id);
                    _banks = QuestionBankStore.LoadAll();
                    GUILayout.EndHorizontal();
                    return;
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawQuiz()
        {
            QuestionBank bank = _deck.Bank;
            GUILayout.Space(10);
            GUILayout.Label(bank.Topic, _big);
            GUILayout.Label(bank.Summary, _wrap);
            GUILayout.Label($"Score: {_deck.Correct}/{_deck.Answered}   ·   Difficulty {_question.Difficulty}   ·   {_question.Concept}");
            GUILayout.Space(10);
            GUILayout.Label(_question.Prompt, _big);

            Color normal = GUI.backgroundColor;
            for (int i = 0; i < _question.Choices.Length; i++)
            {
                if (_chosen >= 0)
                    GUI.backgroundColor = i == _question.CorrectIndex ? Color.green : i == _chosen ? Color.red : normal;
                if (GUILayout.Button($"{(char)('A' + i)}.  {_question.Choices[i]}", GUILayout.Height(44)) && _chosen < 0)
                {
                    _chosen = i;
                    _deck.RecordAnswer(_question, i);
                }
                GUI.backgroundColor = normal;
            }

            if (_chosen >= 0)
            {
                GUILayout.Label((_question.IsCorrect(_chosen) ? "Correct! " : "Not quite. ") + _question.Explanation, _wrap);
                if (GUILayout.Button("Next question", GUILayout.Height(36))) NextQuestion();
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Back to question banks", GUILayout.Width(260))) _deck = null;

            if (bank.Sources.Count > 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("Web sources used:", _wrap);
                foreach (SourceLink s in bank.Sources)
                    if (GUILayout.Button(s.Title, GUI.skin.label)) Application.OpenURL(s.Url);
            }
        }

        void InitStyles()
        {
            if (_wrap != null) return;
            _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            _big = new GUIStyle(_wrap) { fontSize = 20, fontStyle = FontStyle.Bold };
            _title = new GUIStyle(_big) { fontSize = 26 };
        }
    }
}
