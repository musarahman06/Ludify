using System;
using System.Collections.Generic;
using System.Linq;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ludify.Menu
{
    /// <summary>
    /// "Organize subjects": the content library (every imported file, pasted text and screenshot) on the
    /// left, themed subject bundles on the right. Drag items between them; each bundle can have Gemini
    /// write one question set from all its content, and the current bundle themes the game.
    /// </summary>
    public sealed class SubjectsScreen : MonoBehaviour
    {
        PauseMenu _menu;
        RectTransform _library, _grid;
        TextMeshProUGUI _status;
        Canvas _canvas;

        // Survive screen rebuilds while Gemini works.
        static readonly Dictionary<string, string> Busy = new Dictionary<string, string>();
        static readonly HashSet<string> ConfirmDelete = new HashSet<string>();
        static string _renaming;

        public static GameObject Build(Transform canvas, PauseMenu menu)
        {
            RectTransform panel = MenuWidgets.Panel(canvas, "Organize subjects", new Vector2(1640, 940), new Vector2(0.5f, 0.5f), Vector2.zero);
            var screen = panel.gameObject.AddComponent<SubjectsScreen>();
            screen._menu = menu;
            screen._canvas = canvas.GetComponentInParent<Canvas>();
            screen.BuildLayout(panel);
            return panel.gameObject;
        }

        void BuildLayout(RectTransform panel)
        {
            Transform body = MenuWidgets.Row(panel, 680, 24);
            body.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            // Library column
            RectTransform left = UiKit.Rect("LibraryColumn", body);
            MenuWidgets.Size(left, width: 440, flex: 0);
            var lv = left.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 8;
            lv.childControlWidth = lv.childControlHeight = true;
            lv.childForceExpandHeight = false;
            UiKit.Text("Heading", left, "Library", 30, TextAlignmentOptions.Left);
            UiKit.Text("Hint", left, "Everything you've imported. Drag items onto a subject.", 19, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            _library = MenuWidgets.ScrollList(left, 560, out ScrollRect libScroll);
            DropZone.Add(libScroll.gameObject, null);
            Button import = UiKit.Button("Import", left, "Import more", 22, () => { _menu.Close(); ImportButtonOverlay.RequestImport(); },
                                         UiKit.AccentColor, ThemeArt.Icon("import"));
            MenuWidgets.Size(import, height: 62);

            // Bundles grid
            Image frame = UiKit.Image("Bundles", body, new Color(0, 0, 0, 0), raycast: true);
            MenuWidgets.Size(frame, flex: 1);
            var sr = frame.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.scrollSensitivity = 30;
            sr.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", frame.transform));
            viewport.gameObject.AddComponent<RectMask2D>();
            _grid = UiKit.Rect("Grid", viewport);
            _grid.anchorMin = new Vector2(0, 1);
            _grid.anchorMax = new Vector2(1, 1);
            _grid.pivot = new Vector2(0.5f, 1);
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(540, 330);
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = _grid;

            Transform footer = MenuWidgets.Row(panel, 66);
            _status = UiKit.Text("Status", footer, "", 21, TextAlignmentOptions.MidlineLeft, UiKit.MutedTextColor);
            MenuWidgets.Size(_status, flex: 1);
            Button back = UiKit.Button("Back", footer, "Back", 26, _menu.BackToMain, UiKit.ButtonColor, ThemeArt.Icon("close"));
            MenuWidgets.Size(back, width: 240, flex: 0);

            Refresh();
        }

        void OnEnable() => LibraryStore.Changed += Refresh;
        void OnDisable() => LibraryStore.Changed -= Refresh;

        void Refresh()
        {
            if (_library == null) return;
            foreach (Transform c in _library) Destroy(c.gameObject);
            foreach (Transform c in _grid) Destroy(c.gameObject);

            var unfiled = LibraryStore.Unfiled.OrderByDescending(i => i.AddedUtc).ToList();
            if (unfiled.Count == 0)
                UiKit.Text("Empty", _library, LibraryStore.Items.Count == 0
                    ? "Nothing yet. Use Import files to add lecture notes, slides, text or screenshots."
                    : "All items are in subjects.", 19, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            foreach (LibraryItem item in unfiled) Chip(_library, item);

            foreach (SubjectBundle b in LibraryStore.Bundles) Card(b);
            NewBundleCard();
            _status.text = $"{LibraryStore.Items.Count} item(s) in your library · {LibraryStore.Bundles.Count} subject(s)";
        }

        // ---- Items ----

        void Chip(Transform parent, LibraryItem item)
        {
            Image chip = UiKit.Image("Item", parent, UiKit.PanelColor, ThemeArt.Button, raycast: true);
            chip.type = Image.Type.Sliced;
            MenuWidgets.Size(chip, height: 58);
            var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 8, 6, 14);
            h.spacing = 10;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            string icon = item.Kind == "text" ? "scroll" : item.Kind == "image" ? "palette" : "book";
            Image ic = UiKit.Image("Icon", chip.transform, UiKit.AccentColor, ThemeArt.Icon(icon));
            ic.preserveAspect = true;
            MenuWidgets.Size(ic, width: 34, flex: 0);
            TextMeshProUGUI t = UiKit.Text("Title", chip.transform, item.Title, 20, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            MenuWidgets.Size(t, flex: 1);
            Button del = UiKit.Button("Delete", chip.transform, null, 0, () =>
            {
                if (ConfirmDelete.Remove(item.Id)) LibraryStore.DeleteItem(item);
                else { ConfirmDelete.Add(item.Id); _status.text = $"Click the bin again to delete \"{item.Title}\" for good."; }
            }, ConfirmDelete.Contains(item.Id) ? UiKit.WrongColor : UiKit.ButtonColor, ThemeArt.Icon("trash"));
            MenuWidgets.Size(del, width: 40, flex: 0);
            chip.gameObject.AddComponent<ItemDrag>().Init(item, _canvas, chip);
        }

        // ---- Bundles ----

        void Card(SubjectBundle b)
        {
            SubjectPalette pal = LudifyTheme.For(b.Subject);
            bool active = LibraryStore.ActiveBundle == b;
            Image card = UiKit.Image("Bundle", _grid, Color.Lerp(pal.Panel, pal.Accent, 0.1f), UiKit.RoundedSprite, raycast: true);
            if (active) card.gameObject.AddComponent<Outline>().effectColor = pal.Accent;
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(18, 18, 14, 16);
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            SubjectDecor.Add(card.rectTransform, 7, 0.16f, b.Subject);
            DropZone.Add(card.gameObject, b);

            // Header: subject icon, name (or rename box), subject switcher.
            Transform head = MenuWidgets.Row(card.transform, 50, 10);
            head.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            Image icon = UiKit.Image("Icon", head, pal.Accent, ThemeArt.Icon(ThemeArt.Symbols[b.Subject].FirstOrDefault(s => !ThemeArt.IsGlyph(s)) ?? "circle"));
            icon.preserveAspect = true;
            MenuWidgets.Size(icon, width: 44, flex: 0);
            if (_renaming == b.Id)
            {
                TMP_InputField field = UiKit.InputField("Name", head, "Bundle name", 22, multiline: false);
                field.text = b.Name;
                MenuWidgets.Size(field, flex: 1);
                field.onEndEdit.AddListener(v2 => { _renaming = null; LibraryStore.Rename(b, v2); Refresh(); });
                field.ActivateInputField();
            }
            else
            {
                TextMeshProUGUI name = UiKit.Text("Name", head, (active ? "★ " : "") + b.Name, 28, TextAlignmentOptions.MidlineLeft, pal.AccentDark);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                MenuWidgets.Size(name, flex: 1);
            }
            var subjects = LudifyTheme.All.ToList();
            Button subj = MenuWidgets.Cycle(head, subjects.Select(s => LudifyTheme.For(s).Name).ToArray(), subjects.IndexOf(b.Subject),
                i => LibraryStore.SetSubject(b, subjects[i]), pal.Accent);
            MenuWidgets.Size(subj, width: 210, flex: 0);

            // Items in this bundle (also a drop target).
            RectTransform list = MenuWidgets.ScrollList(card.transform, 130, out ScrollRect listScroll, 6);
            DropZone.Add(listScroll.gameObject, b);
            var items = LibraryStore.ItemsIn(b).ToList();
            if (items.Count == 0) UiKit.Text("Empty", list, "Drag lectures here", 18, TextAlignmentOptions.Center, UiKit.MutedTextColor);
            foreach (LibraryItem item in items) Chip(list, item);

            // Status + actions
            string status = Busy.TryGetValue(b.Id, out string s) ? s
                : b.QuestionCount > 0 && !b.Stale ? $"{b.QuestionCount} practice questions · made {Ago(b.GeneratedUtc)}"
                : b.QuestionCount > 0 ? "Items changed since the questions were made"
                : "No bundle questions yet";
            UiKit.Text("Status", card.transform, status, 18, TextAlignmentOptions.Left, UiKit.MutedTextColor);

            Transform actions = MenuWidgets.Row(card.transform, 54, 8);
            Button gen = UiKit.Button("Generate", actions, b.QuestionCount > 0 && !b.Stale ? "Regenerate" : "Generate questions", 19,
                                      () => Generate(b), pal.Accent, ThemeArt.Icon("sparkle"));
            gen.interactable = !Busy.ContainsKey(b.Id) && items.Count > 0;
            MenuWidgets.Size(gen, flex: 2);
            Button use = UiKit.Button("Use", actions, active ? "Current" : "Make current", 19, () => LibraryStore.SetActive(b),
                                      active ? UiKit.CorrectColor : UiKit.ButtonColor, active ? ThemeArt.Icon("check") : null);
            use.interactable = !active;
            MenuWidgets.Size(use, flex: 1.4f);
            Button rename = UiKit.Button("Rename", actions, null, 0, () => { _renaming = b.Id; Refresh(); }, UiKit.ButtonColor, ThemeArt.Icon("quill"));
            MenuWidgets.Size(rename, width: 54, flex: 0);
            Button del = UiKit.Button("Delete", actions, null, 0, () =>
            {
                if (ConfirmDelete.Remove(b.Id)) LibraryStore.DeleteBundle(b);
                else { ConfirmDelete.Add(b.Id); _status.text = $"Click the bin again to delete \"{b.Name}\" (its items go back to the library)."; Refresh(); }
            }, ConfirmDelete.Contains(b.Id) ? UiKit.WrongColor : UiKit.ButtonColor, ThemeArt.Icon("trash"));
            MenuWidgets.Size(del, width: 54, flex: 0);
        }

        void NewBundleCard()
        {
            Image card = UiKit.Image("NewBundle", _grid, UiKit.InsetColor, UiKit.RoundedSprite, raycast: true);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(40, 40, 70, 70);
            v.spacing = 14;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            UiKit.Text("Text", card.transform, "Group lectures into a new subject", 22, TextAlignmentOptions.Center, UiKit.MutedTextColor);
            Button add = UiKit.Button("Add", card.transform, "New subject bundle", 24, () =>
            {
                SubjectBundle b = LibraryStore.CreateBundle("New subject", Subject.General);
                _renaming = b.Id;
                Refresh();
            }, UiKit.AccentColor, ThemeArt.Icon("plus"));
            MenuWidgets.Size(add, height: 70);
        }

        async void Generate(SubjectBundle b)
        {
            if (Busy.ContainsKey(b.Id)) return;
            Busy[b.Id] = "Gemini is reading the bundle…";
            Refresh();
            try
            {
                var progress = new Progress<string>(msg => { Busy[b.Id] = msg; if (this != null) Refresh(); });
                QuestionBank bank = await new LessonImporter().ImportBundleAsync(b, progress);
                if (this != null) _status.text = $"\"{b.Name}\": {bank.Questions.Count} questions from {b.ItemIds.Count} item(s).";
            }
            catch (ImportException e) { if (this != null) _status.text = e.Message; }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) _status.text = "Couldn't generate: " + e.Message;
            }
            finally
            {
                Busy.Remove(b.Id);
                if (this != null) Refresh();
            }
        }

        static string Ago(DateTime? utc)
        {
            if (utc == null) return "never";
            TimeSpan t = DateTime.UtcNow - utc.Value;
            if (t.TotalMinutes < 1) return "just now";
            if (t.TotalHours < 1) return $"{(int)t.TotalMinutes} min ago";
            if (t.TotalDays < 1) return $"{(int)t.TotalHours} h ago";
            return $"{(int)t.TotalDays} d ago";
        }
    }

    /// <summary>Where library items can be dropped: a bundle, or the library itself (null).</summary>
    public sealed class DropZone : MonoBehaviour
    {
        public SubjectBundle Bundle;
        public static void Add(GameObject go, SubjectBundle bundle) => go.AddComponent<DropZone>().Bundle = bundle;
    }

    /// <summary>Drag a library item chip onto a bundle (or back to the library).</summary>
    public sealed class ItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        LibraryItem _item;
        Canvas _canvas;
        Graphic _source;
        RectTransform _ghost;

        public void Init(LibraryItem item, Canvas canvas, Graphic source)
        {
            _item = item;
            _canvas = canvas;
            _source = source;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _ghost = (RectTransform)Instantiate(gameObject, _canvas.transform).transform;
            Destroy(_ghost.GetComponent<ItemDrag>());
            var group = _ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.85f;
            _ghost.sizeDelta = ((RectTransform)transform).rect.size;
            _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(0.5f, 0.5f);
            _ghost.localRotation = Quaternion.Euler(0, 0, 3);
            _source.canvasRenderer.SetAlpha(0.35f);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, e.position, null, out Vector2 p);
            _ghost.anchoredPosition = p;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            _source.canvasRenderer.SetAlpha(1f);
            GameObject over = e.pointerCurrentRaycast.gameObject;
            DropZone zone = over != null ? over.GetComponentInParent<DropZone>() : null;
            if (zone != null) LibraryStore.Move(_item, zone.Bundle);
        }
    }
}
