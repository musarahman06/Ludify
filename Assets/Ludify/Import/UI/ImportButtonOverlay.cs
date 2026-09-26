using System;
using System.Threading;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Small "Import lecture" button at the top of the screen, in every scene.
    /// Installs itself at startup, so no scene needs editing. Picks a file from the computer,
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

        /// <summary>Opens the file picker and imports the chosen file, same as clicking the button.</summary>
        public static void RequestImport()
        {
            if (_instance == null || _instance._busy || LessonFilePicker.IsOpen) return;
            LessonFilePicker.Show(_instance.Import);
        }

        static ImportButtonOverlay _instance;

        const float DesignHeight = 800f;
        const float MessageSeconds = 8f;

        LessonImporter _importer;
        CancellationTokenSource _cts;
        bool _busy;
        string _message;
        float _messageUntil;
        GUIStyle _button, _label;

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

        void Update() => _testPanelPresent = FindAnyObjectByType<ImportTestPanel>() != null;

        void OnDestroy() => _cts.Cancel();

        async void Import(string path)
        {
            _busy = true;
            try
            {
                // Reload config each time so a key added to ludify_secrets.json works without restarting.
                _importer = new LessonImporter();
                var progress = new Progress<string>(s => Show(s, float.PositiveInfinity));
                QuestionBank bank = await _importer.ImportAsync(path, progress, false, _cts.Token);
                Show($"Ready: {bank.Questions.Count} questions on \"{bank.Topic}\"", MessageSeconds);
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

        void OnGUI()
        {
            // The ImportTest scene has its own full panel; the file browser draws its own UI.
            if (!Enabled || LessonFilePicker.IsOpen || _testPanelPresent) return;

            if (_button == null)
            {
                _button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
                _label = new GUIStyle(GUI.skin.box) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }

            float scale = Screen.height / DesignHeight;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;

            const float buttonWidth = 180, buttonHeight = 34;
            var buttonRect = new Rect((width - buttonWidth) / 2, 10, buttonWidth, buttonHeight);
            GUI.enabled = !_busy;
            if (GUI.Button(buttonRect, _busy ? "Importing…" : "Import lecture", _button))
                LessonFilePicker.Show(Import);
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil)
            {
                float messageWidth = Mathf.Min(560, width - 20);
                float height = _label.CalcHeight(new GUIContent(_message), messageWidth) + 12;
                GUI.Box(new Rect((width - messageWidth) / 2, buttonRect.yMax + 6, messageWidth, height), _message, _label);
            }

            GUI.matrix = previous;
        }
    }
}
