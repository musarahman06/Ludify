using Ludify.Gallery;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Menu
{
    /// <summary>Settings (saved in PlayerPrefs, applied at start): sound, graphics, camera, questions, gallery, Gemini key.</summary>
    public static class SettingsScreen
    {
        const string VolumeKey = "Ludify.Volume", SensitivityKey = "Ludify.CameraSensitivity",
                     QualityKey = "Ludify.Quality", FullscreenKey = "Ludify.Fullscreen";

        static readonly int[] QuestionCounts = { 10, 15, 20, 25, 30 };

        /// <summary>Applies saved settings (called when the menu installs).</summary>
        public static void ApplySaved()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(PlayerPrefs.GetInt(QualityKey), true);
            if (PlayerPrefs.HasKey(FullscreenKey) && !Application.isEditor) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
            ApplySensitivity(PlayerPrefs.GetFloat(SensitivityKey, 0.5f));
        }

        /// <summary>0..1 slider → the camera's drag sensitivity (0.05–0.35; the middle is the game's default 0.15).</summary>
        static void ApplySensitivity(float t)
        {
            OrbitCamera cam = Object.FindAnyObjectByType<OrbitCamera>(FindObjectsInactive.Include);
            if (cam != null) cam.mouseSensitivity = t <= 0.5f ? Mathf.Lerp(0.05f, 0.15f, t * 2f) : Mathf.Lerp(0.15f, 0.35f, (t - 0.5f) * 2f);
        }

        public static GameObject Build(Transform canvas, PauseMenu menu)
        {
            RectTransform panel = MenuWidgets.Panel(canvas, "Settings", new Vector2(980, 0), new Vector2(0.5f, 0.5f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MenuWidgets.Slider(MenuWidgets.SettingRow(panel, "Sound volume"), PlayerPrefs.GetFloat(VolumeKey, 1f), v =>
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(VolumeKey, v);
            });

            MenuWidgets.Slider(MenuWidgets.SettingRow(panel, "Camera sensitivity", "How fast dragging turns the camera"),
                PlayerPrefs.GetFloat(SensitivityKey, 0.5f), v =>
                {
                    ApplySensitivity(v);
                    PlayerPrefs.SetFloat(SensitivityKey, v);
                });

            string[] levels = QualitySettings.names;
            MenuWidgets.Cycle(MenuWidgets.SettingRow(panel, "Graphics quality", "Lower is smoother on older laptops"), levels,
                QualitySettings.GetQualityLevel(), i =>
                {
                    QualitySettings.SetQualityLevel(i, true);
                    PlayerPrefs.SetInt(QualityKey, i);
                });

            MenuWidgets.Cycle(MenuWidgets.SettingRow(panel, "Fullscreen"), new[] { "Off", "On" }, Screen.fullScreen ? 1 : 0, i =>
            {
                Screen.fullScreen = i == 1;
                PlayerPrefs.SetInt(FullscreenKey, i);
            });

            int perFile = LlmConfig.Load().QuestionsPerFile;
            int qIndex = System.Array.IndexOf(QuestionCounts, perFile);
            MenuWidgets.Cycle(MenuWidgets.SettingRow(panel, "Questions per lecture", "How many Gemini writes for each import"),
                System.Array.ConvertAll(QuestionCounts, n => n.ToString()), qIndex < 0 ? 2 : qIndex,
                i => PlayerPrefs.SetInt(LlmConfig.QuestionsPerFilePref, QuestionCounts[i]));

            MenuWidgets.Cycle(MenuWidgets.SettingRow(panel, "Gallery build mode", "How images become 3D exhibits"),
                new[] { "Concept model", "Traced from image", "Compare both" }, (int)GallerySystem.CurrentMode,
                i => GallerySystem.CurrentMode = (GallerySystem.BuildMode)i);

            // Gemini key status (never shows the key itself).
            Transform keyRow = MenuWidgets.SettingRow(panel, "Gemini API key");
            LlmConfig config = LlmConfig.Load();
            TextMeshProUGUI status = UiKit.Text("Status", keyRow, config.HasKey ? "Found" : "Missing: see HANDOFF.md", 22,
                                                TextAlignmentOptions.MidlineLeft, config.HasKey ? UiKit.CorrectColor : UiKit.WrongColor);
            MenuWidgets.Size(status, flex: 1);
            Button check = null;
            check = UiKit.Button("Check", keyRow, "Check key", 22, async () =>
            {
                check.interactable = false;
                status.text = "Checking…";
                try
                {
                    LlmConfig c = LlmConfig.Load();
                    if (!c.HasKey) { status.text = "Missing: see HANDOFF.md"; return; }
                    var models = await new GeminiClient(c.ApiKey, c.Model).ListModelsAsync();
                    status.text = models.Contains(c.Model) ? $"Works ({c.Model})" : $"Works, but \"{c.Model}\" isn't available";
                    status.color = UiKit.CorrectColor;
                }
                catch (ImportException e) { status.text = e.Message; status.color = UiKit.WrongColor; }
                finally { if (check != null) check.interactable = true; }
            }, UiKit.ButtonColor);
            MenuWidgets.Size(check, width: 200, flex: 0);

            Button back = UiKit.Button("Back", panel, "Back", 26, () => { PlayerPrefs.Save(); menu.BackToMain(); }, UiKit.ButtonColor, ThemeArt.Icon("close"));
            MenuWidgets.Size(back, height: 70);
            return panel.gameObject;
        }
    }

    /// <summary>Controls cheat sheet and short how-tos.</summary>
    public static class HelpScreen
    {
        public static GameObject Build(Transform canvas, PauseMenu menu)
        {
            RectTransform panel = MenuWidgets.Panel(canvas, "Help", new Vector2(1180, 0), new Vector2(0.5f, 0.5f), Vector2.zero);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform columns = MenuWidgets.Row(panel, 440, 40);
            columns.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            (string[] keys, string what)[] left =
            {
                (new[] { "W", "A", "S", "D" }, "Walk / drive"), (new[] { "Shift" }, "Sprint"), (new[] { "Space" }, "Jump / handbrake"),
                (new[] { "Drag" }, "Look around"), (new[] { "X" }, "Get in / out of a car"), (new[] { "E" }, "Talk / pick up"),
            };
            (string[] keys, string what)[] right =
            {
                (new[] { "M" }, "Map & fast travel"), (new[] { "I" }, "Gallery: add / inspect"), (new[] { "Ctrl", "V" }, "Paste an image"),
                (new[] { "B" }, "Shop"), (new[] { "1", "2", "3", "4" }, "Answer questions"), (new[] { "Esc" }, "This menu / close"),
            };
            foreach (var list in new[] { left, right })
            {
                RectTransform col = UiKit.Rect("Column", columns);
                var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 14;
                v.childControlWidth = v.childControlHeight = true;
                v.childForceExpandHeight = false;
                foreach (var (keys, what) in list)
                {
                    Transform row = MenuWidgets.Row(col, 56, 8);
                    row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                    foreach (string k in keys) MenuWidgets.KeyCap(row, k);
                    TextMeshProUGUI t = UiKit.Text("What", row, what, 24, TextAlignmentOptions.MidlineLeft);
                    MenuWidgets.Size(t, flex: 1);
                }
            }

            UiKit.Text("Tips", panel,
                "<b>Import files</b> to turn lecture notes, slides or screenshots into practice questions.\n" +
                "<b>Organize subjects</b> to group your material into subject bundles; Gemini can write one question set per bundle, " +
                "and the current subject colours the whole game.\n" +
                "<b>Art gallery</b> (map marker A): put images on pedestals to explore them as 3D models.",
                21, TextAlignmentOptions.TopLeft);

            Button back = UiKit.Button("Back", panel, "Back", 26, menu.BackToMain, UiKit.ButtonColor, ThemeArt.Icon("close"));
            MenuWidgets.Size(back, height: 70);
            return panel.gameObject;
        }
    }
}
