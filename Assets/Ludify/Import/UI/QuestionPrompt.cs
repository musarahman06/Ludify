using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ludify.Import
{
    public enum QuestionResult { Correct, Cancelled, NoQuestions }

    /// <summary>
    /// Modal practice-question gate for gameplay: keeps asking until the player answers one
    /// correctly (→ Correct) or gives up (→ Cancelled). Returns NoQuestions immediately if no
    /// lecture has been imported. Works while Time.timeScale is 0.
    /// <code>
    /// if (await QuestionPrompt.AskAsync("Answer to fast travel", "Fast travel") != QuestionResult.Cancelled) Travel();
    /// </code>
    /// </summary>
    public static class QuestionPrompt
    {
        public static bool IsOpen => PromptView.Current != null;

        /// <param name="title">Why the question is being asked.</param>
        /// <param name="continueLabel">Button shown after a correct answer.</param>
        public static Task<QuestionResult> AskAsync(string title, string continueLabel = "Continue")
        {
            QuestionDeck deck = QuestionPool.Deck;
            if (deck == null) return Task.FromResult(QuestionResult.NoQuestions);
            if (PromptView.Current != null) return Task.FromResult(QuestionResult.Cancelled);

            Canvas canvas = UiKit.CreateCanvas("QuestionPrompt", 200);
            return canvas.gameObject.AddComponent<PromptView>().Show(deck, title, continueLabel);
        }

        sealed class PromptView : MonoBehaviour
        {
            public static PromptView Current;

            readonly TaskCompletionSource<QuestionResult> _done = new TaskCompletionSource<QuestionResult>();
            readonly List<Button> _answers = new List<Button>();
            QuestionDeck _deck;
            McQuestion _question;
            string _continueLabel;
            TextMeshProUGUI _prompt, _feedback, _meta;
            Transform _actions;

            public Task<QuestionResult> Show(QuestionDeck deck, string title, string continueLabel)
            {
                Current = this;
                _deck = deck;
                _continueLabel = continueLabel;
                Build(title);
                NextQuestion();
                return _done.Task;
            }

            void Build(string title)
            {
                UiKit.Stretch(UiKit.Image("Dim", transform, new Color(0, 0, 0, 0.6f), raycast: true).rectTransform);

                Image panel = UiKit.Image("Panel", transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
                panel.type = Image.Type.Sliced;
                UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 0));
                var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(40, 40, 32, 32);
                layout.spacing = 14;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                UiKit.Text("Title", panel.transform, title, 30, TextAlignmentOptions.Center, UiKit.AccentColor).fontStyle = FontStyles.Bold;
                _meta = UiKit.Text("Meta", panel.transform, "", 18, TextAlignmentOptions.Center, new Color(0.7f, 0.75f, 0.8f));
                _prompt = UiKit.Text("Question", panel.transform, "", 34);
                _prompt.fontStyle = FontStyles.Bold;

                for (int i = 0; i < 4; i++)
                {
                    int index = i;
                    Button b = UiKit.Button("Answer" + i, panel.transform, "", 26, () => Answer(index));
                    b.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
                    _answers.Add(b);
                }

                _feedback = UiKit.Text("Feedback", panel.transform, "", 24);

                _actions = UiKit.Rect("Actions", panel.transform);
                var row = _actions.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = 16;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = false;
                _actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            }

            void NextQuestion()
            {
                _question = _deck.Next();
                _prompt.text = _question.Prompt;
                _meta.text = $"{_deck.Bank.Topic}   ·   Score {_deck.Correct}/{_deck.Answered}";
                for (int i = 0; i < _answers.Count; i++)
                {
                    Button b = _answers[i];
                    b.interactable = true;
                    ((Image)b.targetGraphic).color = UiKit.ButtonColor;
                    UiKit.SetButtonLabel(b, $"{(char)('A' + i)}.  {_question.Choices[i]}");
                }
                _feedback.gameObject.SetActive(false);
                SetActions(("Cancel", () => Finish(QuestionResult.Cancelled), UiKit.ButtonColor));
            }

            void Answer(int index)
            {
                bool correct = _deck.RecordAnswer(_question, index);
                for (int i = 0; i < _answers.Count; i++)
                {
                    Button b = _answers[i];
                    b.interactable = false;
                    var colors = b.colors;
                    colors.disabledColor = Color.white; // keep the result colours bright
                    b.colors = colors;
                    if (i == _question.CorrectIndex) ((Image)b.targetGraphic).color = UiKit.CorrectColor;
                    else if (i == index) ((Image)b.targetGraphic).color = UiKit.WrongColor;
                }

                _feedback.gameObject.SetActive(true);
                if (correct)
                {
                    _feedback.text = $"<b>Correct!</b> {_question.Explanation}";
                    SetActions((_continueLabel, () => Finish(QuestionResult.Correct), UiKit.CorrectColor));
                }
                else
                {
                    _feedback.text = $"<b>Not quite.</b> The answer is <b>{_question.CorrectChoice}</b>. {_question.Explanation}";
                    SetActions(("Try another question", NextQuestion, UiKit.AccentColor),
                               ("Cancel", () => Finish(QuestionResult.Cancelled), UiKit.ButtonColor));
                }
            }

            void SetActions(params (string Label, UnityEngine.Events.UnityAction OnClick, Color Color)[] actions)
            {
                foreach (Transform child in _actions) Destroy(child.gameObject);
                foreach (var a in actions)
                {
                    Button b = UiKit.Button(a.Label, _actions, a.Label, 24, a.OnClick, a.Color);
                    b.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
                }
            }

            void Update()
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    Finish(QuestionResult.Cancelled);
            }

            void Finish(QuestionResult result)
            {
                if (Current == this) Current = null;
                _done.TrySetResult(result);
                Destroy(gameObject);
            }

            void OnDestroy()
            {
                if (Current == this) Current = null;
                _done.TrySetResult(QuestionResult.Cancelled);
            }
        }
    }
}
