using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ludify.Import;
using Ludify.Map;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ludify.Gallery
{
    /// <summary>
    /// The outdoor art gallery: installs itself in CityMap at runtime (no scene edits), lets anyone
    /// walk up to a pedestal and press I to add an image (Gemini turns it into a 3D exhibit), and
    /// saves exhibits so they reload without API calls.
    /// </summary>
    public sealed class GallerySystem : MonoBehaviour
    {
        const float InteractRange = 4.5f;
        const float MessageSeconds = 7f;

        GalleryArea.Layout _layout;
        List<ExhibitRecord> _records = new List<ExhibitRecord>();
        PlayerController _player;
        Pedestal _nearby, _busy;
        ExhibitViewer _inspect;
        TextMeshProUGUI _prompt;
        GameObject _promptBox;
        string _message;
        float _messageUntil;
        FastTravelPoint _marker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

        static void TryInstall()
        {
            if (FindAnyObjectByType<GallerySystem>() != null) return;
            if (GameObject.Find("InnerOutskirtsBuildings") == null) return;   // not CityMap
            if (FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null) return;

            var go = new GameObject("_Gallery");
            var gallery = go.AddComponent<GallerySystem>();
            // Build right away so, if we run before City Life, its colors/NavMesh never see the old buildings.
            gallery._layout = GalleryArea.Build(go.transform);
        }

        IEnumerator Start()
        {
            _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            yield return null;               // let every other start-up script run first
            GalleryArea.FixUpCityLife();

            BuildUi();
            _inspect = gameObject.AddComponent<ExhibitViewer>();
            _inspect.Init(this);
            RestoreExhibits();

            _marker = new FastTravelPoint
            {
                Name = "Art Gallery", Glyph = "A", Color = new Color(0.65f, 0.4f, 0.9f),
                Position = _layout.Entrance, Facing = _layout.EntranceFacing,
            };
            MapMarkers.Add(_marker);
            Debug.Log($"[Gallery] {_layout.Pedestals.Count} pedestals, {_records.Count} saved exhibits.");
        }

        void OnDestroy()
        {
            if (_marker != null) MapMarkers.Remove(_marker);
        }

        void RestoreExhibits()
        {
            _records = ExhibitStore.Load();
            var used = new HashSet<Pedestal>();
            foreach (ExhibitRecord record in _records.ToList())
            {
                Pedestal pedestal = _layout.Pedestals
                    .Where(p => !used.Contains(p))
                    .OrderBy(p => (new Vector2(p.transform.position.x, p.transform.position.z) - new Vector2(record.X, record.Z)).sqrMagnitude)
                    .FirstOrDefault();
                if (pedestal == null) continue;
                used.Add(pedestal);
                try { pedestal.SetExhibit(record, ExhibitStore.LoadImage(record.ImageFile)); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void Update()
        {
            _nearby = FindNearbyPedestal();
            UpdatePrompt();

            Keyboard kb = Keyboard.current;
            if (kb == null || _nearby == null || Blocked()) return;
            bool paste = (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed) && kb.vKey.wasPressedThisFrame;
            if (paste && _nearby.IsEmpty) PasteImage(_nearby);
            else if (kb.iKey.wasPressedThisFrame)
            {
                if (_nearby.IsEmpty) AddImage(_nearby);
                else _inspect.Begin(_nearby);
            }
        }

        bool Blocked()
        {
            MapSystem map = FindAnyObjectByType<MapSystem>();
            return _inspect.IsActive || _busy != null || QuestionPrompt.IsOpen || LessonFilePicker.IsOpen
                   || (map != null && map.IsFullMapOpen);
        }

        Pedestal FindNearbyPedestal()
        {
            if (_player == null || !_player.gameObject.activeInHierarchy || _layout == null) return null;
            Vector3 p = _player.transform.position;
            Pedestal best = null;
            float bestDist = InteractRange + 1.45f;            // measured from the centre; pedestal is ~2.9 m wide
            foreach (Pedestal pedestal in _layout.Pedestals)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pedestal.transform.position.x, pedestal.transform.position.z));
                if (d < bestDist) { bestDist = d; best = pedestal; }
            }
            return best;
        }

        // ---- Adding an image ----

        /// <summary>Asks how to add the image: a file, or paste one from the clipboard.</summary>
        public void AddImage(Pedestal pedestal)
        {
            if (_busy != null) return;
            ImportPanel.Show(new ImportPanel.Options
            {
                Title = "Add an image to this pedestal",
                Subtitle = "Gemini turns it into an interactive 3D model you can take apart. Choose how:",
                FileButton = "Choose an image file…",
                OnFile = () => LessonFilePicker.ShowImages(path => Import(pedestal, path)),
                OnImage = image => ImportPasted(pedestal, image),
                Choices = ModeLabels,
                ChoiceIndex = (int)CurrentMode,
                ChoiceHint = "Concept: Gemini recognises the subject and builds the best 3D model of it (plus Wikipedia facts). " +
                             "Traced: copies the drawing. Compare: builds both on neighbouring pedestals (2 AI calls).",
                OnChoice = i => CurrentMode = (BuildMode)i,
            });
        }

        async void PasteImage(Pedestal pedestal)
        {
            if (_busy != null) return;
            Show("Reading the clipboard…");
            try { ImportPasted(pedestal, await ImagePaste.ReadAsync()); }
            catch (ImportException e) { Show(e.Message, MessageSeconds * 1.5f); }
        }

        void ImportPasted(Pedestal pedestal, PastedImage image)
        {
            string temp = image.SaveTemp();
            Import(pedestal, temp, image.SourceName, deleteAfter: true);
        }

        async void Import(Pedestal pedestal, string path, string displayName = null, bool deleteAfter = false)
        {
            try { await ImportCore(pedestal, path, displayName ?? Path.GetFileNameWithoutExtension(path)); }
            finally { if (deleteAfter && File.Exists(path)) File.Delete(path); }
        }

        // ---- Build modes (Concept / Traced / Compare both) ----

        /// <summary>True while an exhibit is being viewed up close (it uses Esc).</summary>
        public static bool ViewerOpen
        {
            get
            {
                GallerySystem g = FindAnyObjectByType<GallerySystem>();
                return g != null && g._inspect != null && g._inspect.IsActive;
            }
        }

        public enum BuildMode { Concept, Traced, Compare }
        const string ModePref = "Ludify.Gallery.BuildMode";
        static readonly string[] ModeLabels = { "Concept model", "Traced from image", "Compare both" };

        public static BuildMode CurrentMode
        {
            get
            {
                try { return (BuildMode)Mathf.Clamp(PlayerPrefs.GetInt(ModePref, 0), 0, 2); }
                catch { return BuildMode.Concept; }
            }
            set { try { PlayerPrefs.SetInt(ModePref, (int)value); PlayerPrefs.Save(); } catch { } }
        }

        async System.Threading.Tasks.Task ImportCore(Pedestal pedestal, string path, string displayName)
        {
            BuildMode mode = CurrentMode;
            if (mode != BuildMode.Compare)
            {
                await BuildOn(pedestal, path, displayName, mode == BuildMode.Traced ? ModelMode.Traced : ModelMode.Concept);
                return;
            }

            // Compare: concept version here, traced version on the nearest free pedestal, so you can walk between them.
            Pedestal twin = _layout.Pedestals
                .Where(p => p != pedestal && p.IsEmpty)
                .OrderBy(p => (p.transform.position - pedestal.transform.position).sqrMagnitude)
                .FirstOrDefault();
            await BuildOn(pedestal, path, displayName, ModelMode.Concept);
            if (this == null) return;
            if (twin == null) { Show("No free pedestal nearby for the traced version.", MessageSeconds * 1.5f); return; }
            await BuildOn(twin, path, displayName, ModelMode.Traced);
            if (this != null) Show("Concept model is here; the traced version is on the nearest pedestal. Compare them!", MessageSeconds * 1.5f);
        }

        async System.Threading.Tasks.Task BuildOn(Pedestal pedestal, string path, string displayName, ModelMode mode)
        {
            _busy = pedestal;
            var progress = new Progress<string>(s => { pedestal.SetStatus(s); Show(s); });
            SceneModel model;
            try
            {
                model = await new ImageModelGenerator().GenerateAsync(path, progress, false, default, mode);
            }
            catch (ImportException e) when (File.Exists(path) && ImageModelGenerator.MimeType(path) != null)
            {
                // Gemini unavailable (no key, daily limit…): still hang the picture, just without a 3D model.
                Show(e.Message + " Showing the picture instead.", MessageSeconds * 1.5f);
                model = new SceneModel { Title = displayName, DisplayMode = "image", Explanation = "", Mode = mode == ModelMode.Concept ? "concept" : "traced" };
            }
            catch (Exception e)
            {
                Show(e is ImportException ? e.Message : "Couldn't add that image: " + e.Message);
                if (!(e is ImportException)) Debug.LogException(e);
                pedestal.SetStatus(null);
                _busy = null;
                return;
            }

            if (this == null || pedestal == null) return;   // scene changed while waiting
            try
            {
                var record = new ExhibitRecord
                {
                    X = pedestal.transform.position.x, Z = pedestal.transform.position.z,
                    ImageFile = ExhibitStore.CopyImage(path), Model = model,
                };
                _records.RemoveAll(r => r == pedestal.Record);
                _records.Add(record);
                ExhibitStore.Save(_records);
                pedestal.SetStatus(null);
                pedestal.SetExhibit(record, ExhibitStore.LoadImage(record.ImageFile));
                Show(model.IsModel ? $"\"{model.Title}\" is on display. Press I to inspect it." : $"\"{model.Title}\" is on display as a picture.");
            }
            catch (Exception e)
            {
                Show("Couldn't save the exhibit: " + e.Message);
                Debug.LogException(e);
            }
            finally { _busy = null; }
        }

        /// <summary>Re-runs Gemini on the exhibit's image (1 call) to add per-part details, then rebuilds it.</summary>
        public async void RefreshDetails(Pedestal pedestal)
        {
            if (pedestal?.Record == null || _busy != null) return;
            string image = ExhibitStore.ImagePath(pedestal.Record.ImageFile);
            if (image == null) { Show("The original image is missing, so details can't be refreshed."); return; }
            _busy = pedestal;
            bool wasViewing = _inspect.IsActive;
            if (wasViewing) _inspect.End();
            Show("Asking Gemini for part details…", float.PositiveInfinity);
            try
            {
                ModelMode mode = pedestal.Record.Model?.Mode == "traced" ? ModelMode.Traced : ModelMode.Concept;
                SceneModel model = await new ImageModelGenerator().GenerateAsync(image, null, true, default, mode);
                if (this == null || pedestal == null) return;
                pedestal.Record.Model = model;
                ExhibitStore.Save(_records);
                pedestal.SetExhibit(pedestal.Record, ExhibitStore.LoadImage(pedestal.Record.ImageFile));
                Show($"Updated \"{model.Title}\".");
                if (wasViewing) _inspect.Begin(pedestal);
            }
            catch (ImportException e) { Show(e.Message, MessageSeconds * 1.5f); }
            finally { _busy = null; }
        }

        public void Remove(Pedestal pedestal)
        {
            if (pedestal.Record == null) return;
            ExhibitStore.DeleteImage(pedestal.Record.ImageFile);
            _records.Remove(pedestal.Record);
            ExhibitStore.Save(_records);
            pedestal.ShowEmpty();
            Show("Exhibit removed.");
        }

        // ---- HUD prompt ----

        void BuildUi()
        {
            Canvas canvas = UiKit.CreateCanvas("GalleryCanvas", 45);
            canvas.transform.SetParent(transform, false);
            Image box = UiKit.Image("Prompt", canvas.transform, new Color(0, 0, 0, 0.6f), UiKit.RoundedSprite);
            box.type = Image.Type.Sliced;
            UiKit.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(760, 64));
            _prompt = UiKit.Text("Text", box.transform, "", 24);
            UiKit.Stretch(_prompt.rectTransform, 10);
            _promptBox = box.gameObject;
            _promptBox.SetActive(false);
        }

        public void Show(string message, float seconds = MessageSeconds)
        {
            _message = message;
            _messageUntil = Time.unscaledTime + seconds;
        }

        void UpdatePrompt()
        {
            if (_promptBox == null) return;
            string text = null;
            if (!string.IsNullOrEmpty(_message) && Time.unscaledTime < _messageUntil) text = _message;
            else if (_busy != null) text = "Building the exhibit…";
            else if (_nearby != null && !_inspect.IsActive)
                text = _nearby.IsEmpty ? "Press <b>I</b> to add an image  ·  <b>Ctrl+V</b> to paste one" : "Press <b>I</b> to inspect this exhibit";
            _promptBox.SetActive(text != null && !_inspect.IsActive && !ImportPanel.IsShowing && !LessonFilePicker.IsOpen);
            if (text != null) _prompt.text = text;
        }
    }
}
