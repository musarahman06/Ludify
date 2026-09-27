using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ludify.Import
{
    /// <summary>
    /// "How do you want to add it?" panel shared by the lecture import and the art gallery:
    /// choose a file, paste an image from the clipboard (Ctrl/Cmd+V also works), and optionally
    /// paste text. The game is paused while it's open.
    /// </summary>
    public sealed class ImportPanel : MonoBehaviour
    {
        public sealed class Options
        {
            public string Title;
            public string Subtitle;
            public string FileButton = "Choose a file…";
            /// <summary>Show the paste-text box (lectures).</summary>
            public bool AllowText;
            public string TextPlaceholder = "Paste lecture notes, slide text or a reading here…";
            public string SubmitTextButton = "Use this text";
            public Action OnFile;
            public Action<PastedImage> OnImage;
            /// <summary>(text, optional title)</summary>
            public Action<string, string> OnText;
            /// <summary>Optional one-of-N setting shown as a row of buttons (e.g. how to build the model).</summary>
            public string[] Choices;
            public int ChoiceIndex;
            public string ChoiceHint;
            public Action<int> OnChoice;
        }

        public static bool IsShowing => _current != null;
        static ImportPanel _current;

        Options _options;
        TMP_InputField _text, _titleField;
        TextMeshProUGUI _status;
        Button _pasteButton;
        bool _reading, _closed;

        public static void Show(Options options)
        {
            if (_current != null) return;
            Canvas canvas = UiKit.CreateCanvas("ImportPanel", 150);
            _current = canvas.gameObject.AddComponent<ImportPanel>();
            _current._options = options;
            _current.Build();
            ModalGuard.Push();
        }

        void Build()
        {
            UiKit.Stretch(UiKit.Image("Dim", transform, new Color(0, 0, 0, 0.55f), raycast: true).rectTransform);
            Image panel = UiKit.Image("Panel", transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            panel.type = Image.Type.Sliced;
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 0));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 30, 30);
            layout.spacing = 14;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Text("Title", panel.transform, _options.Title, 32, TextAlignmentOptions.Center, UiKit.AccentColor).fontStyle = FontStyles.Bold;
            if (!string.IsNullOrEmpty(_options.Subtitle))
                UiKit.Text("Subtitle", panel.transform, _options.Subtitle, 19, TextAlignmentOptions.Center, new Color(0.75f, 0.8f, 0.88f));

            if (_options.Choices != null && _options.Choices.Length > 0)
            {
                Transform modeRow = Row(panel.transform, 46);
                for (int i = 0; i < _options.Choices.Length; i++)
                {
                    int index = i;
                    _choiceButtons.Add(UiKit.Button("Choice" + i, modeRow, _options.Choices[i], 19, () => Choose(index), UiKit.ButtonColor));
                }
                if (!string.IsNullOrEmpty(_options.ChoiceHint))
                    UiKit.Text("ChoiceHint", panel.transform, _options.ChoiceHint, 16, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.55f));
                Choose(_options.ChoiceIndex);
            }

            Transform choices = Row(panel.transform, 58);
            UiKit.Button("File", choices, _options.FileButton, 22, ChooseFile, UiKit.ButtonColor);
            if (_options.OnImage != null)
                _pasteButton = UiKit.Button("Paste", choices, "Paste image  (Ctrl+V)", 22, PasteImage, UiKit.ButtonColor);

            if (_options.OnImage != null)
                UiKit.Text("PasteHelp", panel.transform,
                    "Paste works with the Snipping Tool (Win+Shift+S), Mac screenshots (Cmd+Ctrl+Shift+4), " +
                    "or right-click an image in your browser → Copy image. Copied image links work too.",
                    16, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.55f));

            if (_options.AllowText)
            {
                UiKit.Text("Or", panel.transform, "— or paste the text —", 19, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.7f));
                _text = UiKit.InputField("PasteText", panel.transform, _options.TextPlaceholder, 20, multiline: true);
                _text.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
                _titleField = UiKit.InputField("TitleField", panel.transform, "Title (optional), e.g. \"Week 3: Photosynthesis\"", 20, multiline: false);
                _titleField.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            }

            _status = UiKit.Text("Status", panel.transform, "", 19, TextAlignmentOptions.Center, new Color(1f, 0.75f, 0.5f));
            _status.gameObject.SetActive(false);

            Transform actions = Row(panel.transform, 54);
            if (_options.AllowText) UiKit.Button("Submit", actions, _options.SubmitTextButton, 22, SubmitText, UiKit.CorrectColor);
            UiKit.Button("Cancel", actions, "Cancel", 22, Close);
        }

        readonly System.Collections.Generic.List<Button> _choiceButtons = new System.Collections.Generic.List<Button>();

        void Choose(int index)
        {
            _options.ChoiceIndex = index;
            for (int i = 0; i < _choiceButtons.Count; i++)
                ((Image)_choiceButtons[i].targetGraphic).color = i == index ? UiKit.AccentColor : UiKit.ButtonColor;
            _options.OnChoice?.Invoke(index);
        }

        static Transform Row(Transform parent, float height)
        {
            Transform row = UiKit.Rect("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || _reading) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }

            // Ctrl/Cmd+V outside the text boxes pastes an image (inside them it pastes text, as usual).
            bool typing = (_text != null && _text.isFocused) || (_titleField != null && _titleField.isFocused);
            bool modifier = kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
            if (!typing && modifier && kb.vKey.wasPressedThisFrame && _options.OnImage != null) PasteImage();
        }

        void ChooseFile()
        {
            Action onFile = _options.OnFile;
            Close();
            onFile?.Invoke();
        }

        async void PasteImage()
        {
            if (_reading) return;
            _reading = true;
            SetStatus("Reading the clipboard…");
            if (_pasteButton != null) _pasteButton.interactable = false;
            try
            {
                PastedImage image = await ImagePaste.ReadAsync();
                if (_closed) return;
                Action<PastedImage> onImage = _options.OnImage;
                Close();
                onImage?.Invoke(image);
            }
            catch (ImportException e) { if (!_closed) SetStatus(e.Message); }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (!_closed) SetStatus("Couldn't paste: " + e.Message);
            }
            finally
            {
                _reading = false;
                if (!_closed && _pasteButton != null) _pasteButton.interactable = true;
            }
        }

        void SubmitText()
        {
            string text = _text != null ? _text.text : "";
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 40)
            {
                SetStatus("Paste at least a few sentences of the lecture first.");
                return;
            }
            Action<string, string> onText = _options.OnText;
            string title = _titleField != null ? _titleField.text : null;
            Close();
            onText?.Invoke(text, title);
        }

        void SetStatus(string message)
        {
            _status.text = message;
            _status.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        void Close()
        {
            if (_closed) return;
            _closed = true;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            ModalGuard.Pop();
        }
    }
}
