using System.Linq;
using Ludify.Gallery;
using Ludify.Import;
using Ludify.Map;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ludify.Menu
{
    /// <summary>
    /// The Esc menu (styled after the reference): Resume · Import files · Organize subjects · Settings · Help.
    /// Opens with Esc or the "Menu" button in the top-left corner, only when no other screen is using Esc;
    /// pauses the game (and hides the minimap) while open. Installs itself in any scene with a player (no scene
    /// edits). Reads (never edits) teammates' UI state to decide.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        const int CanvasOrder = 140;   // above HUDs/map/gallery, below import panels (150) and question prompts (200)

        public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.activeSelf;
        static PauseMenu _instance;

        GameObject _root, _main, _current, _menuButton;
        TextMeshProUGUI _subjectName, _subjectInfo;
        Image _subjectChip;
        bool _othersOpenLastFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => TryInstall();

        static void TryInstall()
        {
            if (FindAnyObjectByType<PauseMenu>() != null) return;
            if (FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null) return;
            new GameObject(nameof(PauseMenu)).AddComponent<PauseMenu>();
        }

        void Awake() => _instance = this;

        void Start()
        {
            SettingsScreen.ApplySaved();
            Canvas canvas = UiKit.CreateCanvas("MenuCanvas", CanvasOrder);
            canvas.transform.SetParent(transform, false);
            _root = canvas.gameObject;
            UiKit.Stretch(UiKit.Image("Dim", canvas.transform, new Color(0.12f, 0.1f, 0.08f, 0.45f), raycast: true).rectTransform);
            _main = BuildMain(canvas.transform);
            _root.SetActive(false);
            BuildMenuButton();
            LudifyTheme.Changed += RefreshSubjectCard;
            LibraryStore.Changed += RefreshSubjectCard;
        }

        void OnDestroy()
        {
            LudifyTheme.Changed -= RefreshSubjectCard;
            LibraryStore.Changed -= RefreshSubjectCard;
            if (IsOpen) ModalGuard.Pop();
        }

        // ---- Esc ----

        void Update()
        {
            Keyboard kb = Keyboard.current;
            bool others = OthersOpen();
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    if (!ImportPanel.IsShowing && !SimpleFileBrowser.FileBrowser.IsOpen && !CardMenu.CloseOpen())
                    {
                        if (_current != _main) ShowScreen(_main); else Close();
                    }
                }
                // Esc belongs to whatever else is open (or was, until this very frame).
                else if (!others && !_othersOpenLastFrame) Open();
            }
            _othersOpenLastFrame = others;

            // The corner button shows whenever Esc would open the menu.
            bool button = !IsOpen && !others && !LoadingScreen.IsLoading;
            if (_menuButton != null && _menuButton.activeSelf != button) _menuButton.SetActive(button);
        }

        /// <summary>"☰ Menu" pill in the top-left corner: the same as pressing Esc.</summary>
        void BuildMenuButton()
        {
            Canvas canvas = UiKit.CreateCanvas("MenuButtonCanvas", 35);
            canvas.transform.SetParent(transform, false);
            Button b = UiKit.Button("MenuButton", canvas.transform, "Menu  <size=70%><alpha=#AA>Esc</size>", 26,
                                    () => { if (!OthersOpen()) Open(); }, UiKit.ButtonColor, ThemeArt.Icon("menu"));
            UiKit.Place((RectTransform)b.transform, new Vector2(0, 1), new Vector2(24, -20), new Vector2(210, 64));
            _menuButton = b.gameObject;
        }

        /// <summary>Anything else on screen that uses Esc or shouldn't be interrupted by a pause.</summary>
        internal static bool OthersOpen()
        {
            if (IsOpen) return false;
            if (ModalGuard.IsOpen || ImportPanel.IsShowing || QuestionPrompt.IsOpen || SimpleFileBrowser.FileBrowser.IsOpen) return true;
            MapSystem map = FindAnyObjectByType<MapSystem>();
            if (map != null && map.IsFullMapOpen) return true;
            if (GallerySystem.ViewerOpen) return true;
            DialogueBox dialogue = FindAnyObjectByType<DialogueBox>();
            if (dialogue != null && dialogue.IsOpen) return true;
            StoreView store = FindAnyObjectByType<StoreView>();
            if (store != null && store.IsOpen) return true;
            LoadingScreen loading = FindAnyObjectByType<LoadingScreen>();
            if (loading != null && loading.isActiveAndEnabled) return true;
            TimeTrialManager trial = FindAnyObjectByType<TimeTrialManager>();
            if (trial != null && trial.CurrentState != TimeTrialManager.State.Idle && trial.CurrentState != TimeTrialManager.State.Finished) return true;
            return false;
        }

        public void Open()
        {
            if (IsOpen || _root == null) return;
            ModalGuard.Push();
            _root.SetActive(true);
            ShowScreen(_main);
            RefreshSubjectCard();
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (_current != null && _current != _main) Destroy(_current);
            _current = null;
            _root.SetActive(false);
            ModalGuard.Pop();
        }

        void ShowScreen(GameObject screen)
        {
            if (_current != null && _current != _main && _current != screen) Destroy(_current);
            _main.SetActive(screen == _main);
            _current = screen;
            if (screen != null) screen.SetActive(true);
        }

        public void BackToMain() => ShowScreen(_main);

        // ---- Main screen ----

        GameObject BuildMain(Transform canvas)
        {
            RectTransform screen = UiKit.Stretch(UiKit.Rect("Main", canvas));

            RectTransform panel = MenuWidgets.Panel(screen, "Menu", new Vector2(620, 0), new Vector2(0.5f, 0.5f), new Vector2(-170, 0));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            (string label, string icon, System.Action action)[] items =
            {
                ("Resume game", "play", Close),
                ("Import files", "import", () => { Close(); ImportButtonOverlay.RequestImport(); }),
                ("Organize subjects", "folders", () => ShowScreen(SubjectsScreen.Build(canvas, this))),
                ("Settings", "gear", () => ShowScreen(SettingsScreen.Build(canvas, this))),
                ("Help", "help", () => ShowScreen(HelpScreen.Build(canvas, this))),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var (label, icon, action) = items[i];
                Button b = UiKit.Button(label.Replace(" ", ""), panel, label, 30, () => action(), LudifyTheme.MenuColors[i], ThemeArt.Icon(icon));
                MenuWidgets.Size(b, height: 86);
            }

            // Side card: the current subject, like the reference's small floating cards.
            RectTransform card = MenuWidgets.Panel(screen, null, new Vector2(360, 0), new Vector2(0.5f, 0.5f), new Vector2(355, 150), decor: false);
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.Text("Heading", card, "Current subject", 22, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            Transform row = MenuWidgets.Row(card, 56);
            _subjectChip = UiKit.Image("Chip", row, UiKit.AccentColor, UiKit.CircleSprite);
            _subjectChip.preserveAspect = true;
            MenuWidgets.Size(_subjectChip, width: 56, flex: 0);
            _subjectName = UiKit.Text("Name", row, "", 30, TextAlignmentOptions.MidlineLeft);
            _subjectInfo = UiKit.Text("Info", card, "", 19, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            Button change = UiKit.Button("Change", card, "Change subject", 22, () => ShowScreen(SubjectsScreen.Build(canvas, this)), UiKit.AccentColor);
            MenuWidgets.Size(change, height: 60);
            return screen.gameObject;
        }

        void RefreshSubjectCard()
        {
            if (_subjectName == null) return;
            SubjectBundle active = LibraryStore.ActiveBundle;
            SubjectPalette p = LudifyTheme.Palette;
            _subjectChip.sprite = ThemeArt.Icon(ThemeArt.Symbols[p.Subject].FirstOrDefault(s => !ThemeArt.IsGlyph(s)) ?? "circle");
            _subjectChip.color = p.Accent;
            _subjectName.text = active != null ? active.Name : p.Name;
            _subjectInfo.text = active == null
                ? "No subject bundle chosen yet. Questions come from every lecture."
                : $"{LibraryStore.ItemsIn(active).Count()} item(s) · " + (active.QuestionCount > 0 && !active.Stale
                    ? $"{active.QuestionCount} practice questions"
                    : "questions from its lectures");
        }
    }
}
