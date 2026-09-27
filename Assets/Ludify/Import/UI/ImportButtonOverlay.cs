using System;
using System.Threading;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Small "Import lecture" button at the top of the screen, in every scene.
    /// Installs itself at startup, so no scene needs editing. Takes a file, pasted text or a pasted image,
    /// generates questions, and raises <see cref="LessonImported"/> for gameplay to use.
    /// </summary>
    public sealed class ImportButtonOverlay : MonoBehaviour
    {
        /// <summary>Set false (e.g. from gameplay code) to hide the button.</summary>
        public static bool Enabled = true;

        /// <summary>Raised on the main thread when a lesson has been imported (or loaded from cache).</summary>
        public static event Action<QuestionBank> LessonImported;

        /// <summary>Raised on the main thread when an import fails (message is user-facing).</summary>
        public static event Action<string> ImportFailed;

        /// <summary>True while a file is being imported.</summary>
        public static bool IsBusy => _instance != null && _instance._busy;

        /// <summary>Latest progress/result message, or null if none is showing.</summary>
        public static string StatusMessage =>
            _instance != null && Time.unscaledTime < _instance._messageUntil ? _instance._message : null;

        /// <summary>Opens the import choices (file, pasted text or pasted image), same as clicking the button.</summary>
        public static void RequestImport()
        {
            if (_instance == null || _instance._busy || LessonFilePicker.IsOpen) return;
            _instance.ShowChoices();
        }

        void ShowChoices() => ImportPanel.Show(new ImportPanel.Options
        {
            Title = "Import a lecture",
            Subtitle = "A file (PDF, PowerPoint, Word, text or image), a pasted screenshot, or pasted text. Gemini writes practice questions from it.",
            FileButton = "Choose a file…",
            AllowText = true,
            SubmitTextButton = "Generate questions",
            // Everything imported is also kept in the library, filed under its subject (Esc menu → Organize subjects).
            OnFile = () => LessonFilePicker.Show(path => Run(i => i.ImportAsync(path, Progress(), false, _cts.Token), () => LibraryStore.AddFile(path))),
            OnText = (text, title) => Run(i => i.ImportTextAsync(text, title, Progress(), false, _cts.Token), () => LibraryStore.AddText(text, title)),
            OnImage = image => Run(i => i.ImportImageAsync(image.Bytes, image.MimeType, image.SourceName, Progress(), false, _cts.Token),
                                   () => LibraryStore.AddImage(image.Bytes, image.Extension, image.SourceName)),
        });

        IProgress<string> Progress() => new Progress<string>(s => Show(s, float.PositiveInfinity));

        static ImportButtonOverlay _instance;

        const float DesignHeight = 800f;
        const float MessageSeconds = 8f;

        LessonImporter _importer;
        CancellationTokenSource _cts;
        bool _busy;
        string _message;
        float _messageUntil;
        UnityEngine.UI.Button _buttonUi;
        GameObject _messageBox, _root;
        TMPro.TextMeshProUGUI _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject(nameof(ImportButtonOverlay));
            DontDestroyOnLoad(go);
            go.AddComponent<ImportButtonOverlay>();
        }

        bool _testPanelPresent;

        void Awake()
        {
            _cts = new CancellationTokenSource();
            _instance = this;
        }

        void Start()
        {
            // Themed pill button at the top of the screen, with a message card under it.
            Canvas canvas = UiKit.CreateCanvas("ImportButtonCanvas", 30);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;
            _buttonUi = UiKit.Button("ImportLecture", canvas.transform, "Import lecture", 22, ShowChoices, UiKit.AccentColor, ThemeArt.Icon("import"));
            UiKit.Place((RectTransform)_buttonUi.transform, new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(270, 60));
            UnityEngine.UI.Image box = UiKit.Image("Message", canvas.transform, UiKit.PanelColor, UiKit.RoundedSprite);
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -82), new Vector2(720, 0));
            var fit = box.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            fit.padding = new RectOffset(22, 22, 14, 16);
            fit.childControlWidth = fit.childControlHeight = true;
            box.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            _messageText = UiKit.Text("Text", box.transform, "", 20);
            _messageBox = box.gameObject;
            _messageBox.SetActive(false);
        }

        void Update()
        {
            _testPanelPresent = FindAnyObjectByType<ImportTestPanel>() != null;
            if (_root == null) return;
            // Hidden when switched off, while another import screen is up, or in the developer test scene.
            bool show = Enabled && !LessonFilePicker.IsOpen && !_testPanelPresent;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (!show) return;
            _buttonUi.interactable = !_busy;
            UiKit.SetButtonLabel(_buttonUi, _busy ? "Importing…" : "Import lecture");
            bool message = !string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil;
            if (_messageBox.activeSelf != message) _messageBox.SetActive(message);
            if (message && _messageText.text != _message) _messageText.text = _message;
        }

        void OnDestroy() => _cts.Cancel();

        async void Run(Func<LessonImporter, System.Threading.Tasks.Task<QuestionBank>> import, Func<LibraryItem> store = null)
        {
            _busy = true;
            LibraryItem item = null;
            try { item = store?.Invoke(); }
            catch (Exception e) { Debug.LogWarning("[Ludify.Library] Couldn't store the import: " + e.Message); }
            try
            {
                // Reload config each time so a key added to ludify_secrets.json works without restarting.
                _importer = new LessonImporter();
                Show("Reading…", float.PositiveInfinity);
                QuestionBank bank = await import(_importer);
                Show($"Ready: {bank.Questions.Count} questions on \"{bank.Topic}\"", MessageSeconds);
                if (item != null)
                {
                    // File it under its subject and make that subject current (theme + which questions games use).
                    LibraryStore.FileImported(item, bank);
                    SubjectBundle bundle = LibraryStore.Bundle(item.BundleId ?? "");
                    if (bundle != null) LibraryStore.SetActive(bundle);
                }
                LessonImported?.Invoke(bank);
            }
            catch (OperationCanceledException) { }
            catch (ImportException e)
            {
                Show(e.Message, MessageSeconds * 2);
                ImportFailed?.Invoke(e.Message);
            }
            catch (Exception e)
            {
                Show("Import failed: " + e.Message, MessageSeconds * 2);
                Debug.LogException(e);
                ImportFailed?.Invoke("Import failed: " + e.Message);
            }
            finally { _busy = false; }
        }

        void Show(string message, float seconds)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + seconds;
        }

    }
}
