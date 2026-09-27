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
    /// "Organize subjects": sort material that's already been imported into subject bundles. Unsorted items sit
    /// on the left and subject cards on the right. Drag items onto a card (or add them from the card's menu), and
    /// drag cards by their top bar to reorder them. Each card can have Gemini write one question set from all its
    /// content, and the current card themes the game. Importing new material is in the Esc menu, not here.
    /// </summary>
    public sealed class SubjectsScreen : MonoBehaviour
    {
        const float CardHeight = 340;

        PauseMenu _menu;
        RectTransform _library, _grid;
        ScrollRect _gridScroll;
        TextMeshProUGUI _status;
        Canvas _canvas;
        readonly List<RectTransform> _cards = new List<RectTransform>();
        bool _cardDragging;

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
            Transform body = MenuWidgets.Row(panel, 700, 26);
            body.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            // Unsorted items
            RectTransform left = Column("UnsortedColumn", body);
            MenuWidgets.Size(left, width: 420, flex: 0);
            UiKit.Text("Heading", left, "Unsorted", 30, TextAlignmentOptions.Left);
            UiKit.Text("Hint", left, "Imported items that aren't in a subject yet. Drag them onto a subject card.", 19,
                       TextAlignmentOptions.Left, UiKit.MutedTextColor);
            _library = MenuWidgets.ScrollList(left, 560, out ScrollRect libScroll);
            libScroll.GetComponent<LayoutElement>().flexibleHeight = 1;
            DropZone.Add(libScroll.gameObject, null);

            // Subject cards
            RectTransform right = Column("SubjectsColumn", body);
            MenuWidgets.Size(right, flex: 1);
            UiKit.Text("Heading", right, "Subjects", 30, TextAlignmentOptions.Left);
            UiKit.Text("Hint", right, "Drag a card by its top bar to reorder. Use a card's menu to add items, recolour, rename or delete it.", 19,
                       TextAlignmentOptions.Left, UiKit.MutedTextColor);
            Image frame = UiKit.Image("Cards", right, new Color(0, 0, 0, 0), raycast: true);
            frame.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            _gridScroll = frame.gameObject.AddComponent<ScrollRect>();
            _gridScroll.horizontal = false;
            _gridScroll.scrollSensitivity = 30;
            _gridScroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", frame.transform));
            viewport.gameObject.AddComponent<RectMask2D>();
            _grid = UiKit.Rect("Grid", viewport);
            _grid.anchorMin = new Vector2(0, 1);
            _grid.anchorMax = new Vector2(1, 1);
            _grid.pivot = new Vector2(0.5f, 1);
            _grid.sizeDelta = Vector2.zero;   // exactly the viewport's width (a new rect is 100 wide, which pushed cards off the left edge)
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.cellSize = new Vector2(500, CardHeight);
            _grid.gameObject.AddComponent<FitGridColumns>().CellHeight = CardHeight;
            _grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _gridScroll.viewport = viewport;
            _gridScroll.content = _grid;

            Transform footer = MenuWidgets.Row(panel, 66);
            _status = UiKit.Text("Status", footer, "", 21, TextAlignmentOptions.MidlineLeft, UiKit.MutedTextColor);
            MenuWidgets.Size(_status, flex: 1);
            Button back = UiKit.Button("Back", footer, "Back", 26, _menu.BackToMain, UiKit.ButtonColor, ThemeArt.Icon("back"));
            MenuWidgets.Size(back, width: 240, flex: 0);

            Refresh();
        }

        static RectTransform Column(string name, Transform parent)
        {
            RectTransform col = UiKit.Rect(name, parent);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            return col;
        }

        void OnEnable() => LibraryStore.Changed += Refresh;

        void OnDisable()
        {
            LibraryStore.Changed -= Refresh;
            CardMenu.CloseOpen();
        }

        void Refresh()
        {
            if (_library == null) return;
            if (_cardDragging) return;   // don't pull the card out from under the pointer; EndCardDrag refreshes
            Clear(_library);
            Clear(_grid);
            _cards.Clear();

            var unfiled = LibraryStore.Unfiled.OrderByDescending(i => i.AddedUtc).ToList();
            if (unfiled.Count == 0)
                UiKit.Text("Empty", _library, LibraryStore.Items.Count == 0
                    ? "Nothing imported yet. Import lectures, slides, text or screenshots from the menu (Esc → Import files), then sort them here."
                    : "All sorted! Every item is in a subject.", 19, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            foreach (LibraryItem item in unfiled) Chip(_library, item, inBundle: false);

            foreach (SubjectBundle b in LibraryStore.Bundles) _cards.Add(Card(b));
            NewBundleTile();
            _status.text = $"{LibraryStore.Items.Count} item(s) · {LibraryStore.Bundles.Count} subject(s)" +
                           (unfiled.Count > 0 ? $" · {unfiled.Count} unsorted" : "");
        }

        /// <summary>Detach before destroying, so layouts and drag maths never see the old rows.</summary>
        static void Clear(Transform t)
        {
            foreach (Transform c in t.Cast<Transform>().ToList())
            {
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }
        }

        // ---- Items ----

        void Chip(Transform parent, LibraryItem item, bool inBundle)
        {
            Image chip = UiKit.Image("Item", parent, UiKit.PanelColor, ThemeArt.Button, raycast: true);
            chip.type = Image.Type.Sliced;
            MenuWidgets.Size(chip, height: 56);
            var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 8, 6, 14);
            h.spacing = 10;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            Image ic = UiKit.Image("Icon", chip.transform, UiKit.AccentColor, ThemeArt.Icon(KindIcon(item)));
            ic.preserveAspect = true;
            MenuWidgets.Size(ic, width: 32, flex: 0);
            TextMeshProUGUI t = UiKit.Text("Title", chip.transform, item.Title, 20, TextAlignmentOptions.MidlineLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            MenuWidgets.Size(t, flex: 1);

            Button action;
            if (inBundle)
                // In a subject: take it back out (to Unsorted). Deleting for good happens from Unsorted.
                action = UiKit.Button("Remove", chip.transform, null, 0, () => LibraryStore.Move(item, null), UiKit.ButtonColor, ThemeArt.Icon("close"));
            else
                action = UiKit.Button("Delete", chip.transform, null, 0, () =>
                {
                    if (ConfirmDelete.Remove(item.Id)) LibraryStore.DeleteItem(item);
                    else { ConfirmDelete.Add(item.Id); Refresh(); _status.text = $"Click the bin again to delete \"{item.Title}\" for good."; }
                }, ConfirmDelete.Contains(item.Id) ? UiKit.WrongColor : UiKit.ButtonColor, ThemeArt.Icon("trash"));
            MenuWidgets.Size(action, width: 40, flex: 0);
            chip.gameObject.AddComponent<ItemDrag>().Init(item, _canvas, chip);
        }

        internal static string KindIcon(LibraryItem item) => item.Kind == "text" ? "scroll" : item.Kind == "image" ? "palette" : "book";

        // ---- Subject cards ----

        RectTransform Card(SubjectBundle b)
        {
            SubjectPalette pal = LudifyTheme.For(b.Subject);
            bool active = LibraryStore.ActiveBundle == b;
            Image card = UiKit.Image("Bundle", _grid, Color.Lerp(pal.Panel, pal.Accent, 0.12f), UiKit.RoundedSprite, raycast: true);
            if (active)
            {
                var outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = pal.Accent;
                outline.effectDistance = new Vector2(4, -4);
            }
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 16);
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            SubjectDecor.Add(card.rectTransform, 7, 0.16f, b.Subject);
            DropZone.Add(card.gameObject, b);

            // Top bar (drag handle): grip, subject icon, name (or rename box), card menu.
            Image head = UiKit.Image("TopBar", card.transform, new Color(0, 0, 0, 0), raycast: true);
            MenuWidgets.Size(head, height: 50);
            var hl = head.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            Image grip = UiKit.Image("Grip", head.transform, new Color(pal.AccentDark.r, pal.AccentDark.g, pal.AccentDark.b, 0.55f), ThemeArt.Icon("grip"));
            grip.preserveAspect = true;
            MenuWidgets.Size(grip, width: 22, flex: 0);
            Image icon = UiKit.Image("Icon", head.transform, pal.Accent, ThemeArt.Icon(SubjectIcon(b.Subject)));
            icon.preserveAspect = true;
            MenuWidgets.Size(icon, width: 40, flex: 0);
            if (_renaming == b.Id)
            {
                TMP_InputField field = UiKit.InputField("Name", head.transform, "Subject name", 22, multiline: false);
                field.text = b.Name;
                MenuWidgets.Size(field, flex: 1);
                field.onEndEdit.AddListener(v2 => { _renaming = null; LibraryStore.Rename(b, v2); Refresh(); });
                field.ActivateInputField();
            }
            else
            {
                TextMeshProUGUI name = UiKit.Text("Name", head.transform, b.Name, 28, TextAlignmentOptions.MidlineLeft, pal.AccentDark);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                MenuWidgets.Size(name, flex: 1);
            }
            Button more = null;
            more = UiKit.Button("CardMenu", head.transform, null, 0, () => CardMenu.Open(this, _canvas.transform, b, (RectTransform)more.transform),
                                UiKit.ButtonColor, ThemeArt.Icon("more"));
            MenuWidgets.Size(more, width: 52, flex: 0);
            head.gameObject.AddComponent<BundleDrag>().Init(this, b, card.rectTransform);

            // Items in this subject (also a drop target).
            RectTransform list = MenuWidgets.ScrollList(card.transform, 120, out ScrollRect listScroll, 6);
            listScroll.GetComponent<LayoutElement>().flexibleHeight = 1;
            DropZone.Add(listScroll.gameObject, b);
            var items = LibraryStore.ItemsIn(b).ToList();
            if (items.Count == 0)
                UiKit.Text("Empty", list, "Drag items here, or open the card menu → Add items", 18, TextAlignmentOptions.Center, UiKit.MutedTextColor);
            foreach (LibraryItem item in items) Chip(list, item, inBundle: true);

            // Status + actions
            bool busy = Busy.TryGetValue(b.Id, out string s);
            bool fresh = b.QuestionCount > 0 && !b.Stale;
            string status = busy ? s
                : fresh ? $"{b.QuestionCount} practice questions · made {Ago(b.GeneratedUtc)}"
                : b.QuestionCount > 0 ? "Items changed since the questions were made"
                : "No subject questions yet";
            TextMeshProUGUI st = UiKit.Text("Status", card.transform, status, 18, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            st.textWrappingMode = TextWrappingModes.NoWrap;
            st.overflowMode = TextOverflowModes.Ellipsis;

            Transform actions = MenuWidgets.Row(card.transform, 54, 10);
            Button gen = UiKit.Button("Generate", actions, busy ? "Working…" : fresh ? "Regenerate" : "Generate", 20,
                                      () => Generate(b), pal.Accent, ThemeArt.Icon("sparkle"));
            gen.interactable = !busy && items.Count > 0;
            OneLine(gen);
            Button use = UiKit.Button("Use", actions, active ? "Current" : "Make current", 20, () => LibraryStore.SetActive(b),
                                      active ? UiKit.CorrectColor : UiKit.ButtonColor, ThemeArt.Icon(active ? "check" : "play"));
            use.interactable = !active;
            OneLine(use);
            return card.rectTransform;
        }

        /// <summary>Keeps a button label on one line, shrinking it a little if it has to.</summary>
        internal static void OneLine(Button b)
        {
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = 14;
        }

        internal static string SubjectIcon(Subject s) => ThemeArt.Symbols[s].FirstOrDefault(x => !ThemeArt.IsGlyph(x)) ?? "circle";

        void NewBundleTile()
        {
            Image tile = UiKit.Image("NewSubject", _grid, UiKit.InsetColor, UiKit.RoundedSprite, raycast: true);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile;
            button.onClick.AddListener(() =>
            {
                SubjectBundle b = LibraryStore.CreateBundle("New subject", Subject.General);
                StartRename(b);
            });
            var v = tile.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(30, 30, 30, 30);
            v.spacing = 10;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            Image plus = UiKit.Image("Plus", tile.transform, UiKit.AccentColor, ThemeArt.Icon("plus"));
            plus.preserveAspect = true;
            MenuWidgets.Size(plus, height: 70);
            UiKit.Text("Text", tile.transform, "New subject", 28, TextAlignmentOptions.Center);
            UiKit.Text("Hint", tile.transform, "Click, or drop an item here to start a subject with it", 19, TextAlignmentOptions.Center, UiKit.MutedTextColor);
            tile.gameObject.AddComponent<DropZone>().CreatesBundle = true;
        }

        public void StartRename(SubjectBundle b)
        {
            _renaming = b.Id;
            Refresh();
        }

        public void RequestDelete(SubjectBundle b)
        {
            if (ConfirmDelete.Remove(b.Id)) { LibraryStore.DeleteBundle(b); return; }
            ConfirmDelete.Add(b.Id);
            _status.text = $"Choose Delete again to remove \"{b.Name}\" (its items go back to Unsorted).";
        }

        public static bool IsConfirmingDelete(SubjectBundle b) => ConfirmDelete.Contains(b.Id);

        // ---- Reordering cards ----

        internal void BeginCardDrag() => _cardDragging = true;

        /// <summary>Grid position (sibling index) of the card under a screen point, or -1.</summary>
        internal int CardIndexAt(Vector2 screen)
        {
            foreach (RectTransform c in _cards)
                if (c != null && RectTransformUtility.RectangleContainsScreenPoint(c, screen, null)) return c.GetSiblingIndex();
            return -1;
        }

        /// <summary>Scrolls the card list while a card is held near its top or bottom edge.</summary>
        internal void AutoScroll(Vector2 screen)
        {
            var view = (RectTransform)_gridScroll.viewport;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, null, out Vector2 p)) return;
            Rect r = view.rect;
            float edge = 70, speed = 1.5f * Time.unscaledDeltaTime;
            if (p.y > r.yMax - edge) _gridScroll.verticalNormalizedPosition = Mathf.Clamp01(_gridScroll.verticalNormalizedPosition + speed);
            else if (p.y < r.yMin + edge) _gridScroll.verticalNormalizedPosition = Mathf.Clamp01(_gridScroll.verticalNormalizedPosition - speed);
        }

        internal void EndCardDrag(SubjectBundle b, int index)
        {
            _cardDragging = false;
            if (LibraryStore.Bundles.ToList().IndexOf(b) != index) LibraryStore.MoveBundle(b, index);   // saves → Refresh
            else Refresh();
        }

        // ---- Questions ----

        async void Generate(SubjectBundle b)
        {
            if (Busy.ContainsKey(b.Id)) return;
            Busy[b.Id] = "Gemini is reading the subject…";
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

    /// <summary>
    /// A subject card's pop-up menu: add items from the library, change the subject (colour and symbols),
    /// rename, delete. Clicking outside closes it; so does Esc (see <see cref="PauseMenu"/>).
    /// </summary>
    public sealed class CardMenu : MonoBehaviour
    {
        const float Width = 440;

        static CardMenu _open;

        SubjectsScreen _screen;
        SubjectBundle _bundle;
        RectTransform _panel;

        public static bool CloseOpen()
        {
            if (_open == null) return false;
            _open.Close();
            return true;
        }

        public static void Open(SubjectsScreen screen, Transform canvas, SubjectBundle b, RectTransform anchor)
        {
            CloseOpen();
            Image blocker = UiKit.Image("CardMenu", canvas, new Color(0, 0, 0, 0), raycast: true);
            UiKit.Stretch(blocker.rectTransform);
            var outside = blocker.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            var menu = blocker.gameObject.AddComponent<CardMenu>();
            outside.onClick.AddListener(menu.Close);
            menu._screen = screen;
            menu._bundle = b;

            Image panel = UiKit.Image("Popup", blocker.transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            panel.gameObject.AddComponent<ClickSink>();
            menu._panel = panel.rectTransform;
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 16, 18);
            v.spacing = 8;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Hang below the ⋯ button, or above it when it's low on the screen.
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            bool below = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y > Screen.height * 0.5f;
            RectTransform rt = menu._panel;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(1, below ? 1 : 0);
            rt.sizeDelta = new Vector2(Width, 0);
            rt.position = below ? corners[3] : corners[2];
            rt.anchoredPosition += new Vector2(0, below ? -8 : 8);

            _open = menu;
            menu.ShowMain();
        }

        public void Close()
        {
            if (_open == this) _open = null;
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy() { if (_open == this) _open = null; }

        void Clear()
        {
            foreach (Transform c in _panel.Cast<Transform>().ToList())
            {
                c.SetParent(null, false);
                Destroy(c.gameObject);
            }
        }

        Button Item(string label, string icon, Action onClick, Color? color = null)
        {
            Button b = UiKit.Button(label, _panel, label, 22, () => onClick(), color ?? UiKit.ButtonColor, ThemeArt.Icon(icon));
            MenuWidgets.Size(b, height: 58);
            SubjectsScreen.OneLine(b);
            return b;
        }

        void Title(string text)
        {
            TextMeshProUGUI t = UiKit.Text("Title", _panel, text, 24, TextAlignmentOptions.Left);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        void ShowMain()
        {
            Clear();
            SubjectPalette pal = LudifyTheme.For(_bundle.Subject);
            Title(_bundle.Name);
            Item("Add items", "plus", ShowAdd, pal.Accent);
            MenuWidgets.Chevron(Item("Subject: " + pal.Name, SubjectsScreen.SubjectIcon(_bundle.Subject), ShowSubjects).transform);
            Item("Rename", "quill", () => { Close(); _screen.StartRename(_bundle); });
            bool confirm = SubjectsScreen.IsConfirmingDelete(_bundle);
            Item(confirm ? "Click again to delete" : "Delete subject", "trash", () =>
            {
                _screen.RequestDelete(_bundle);
                if (LibraryStore.Bundle(_bundle.Id) == null) Close(); else ShowMain();
            }, confirm ? UiKit.WrongColor : UiKit.ButtonColor);
        }

        /// <summary>Everything imported that isn't in this subject: unsorted items first, then ones from other subjects.</summary>
        void ShowAdd()
        {
            Clear();
            Item("Back", "back", ShowMain);
            Title("Add to " + _bundle.Name);
            var candidates = LibraryStore.Items.Where(i => i.BundleId != _bundle.Id)
                                               .OrderBy(i => LibraryStore.Bundle(i.BundleId ?? "") != null)
                                               .ThenByDescending(i => i.AddedUtc).ToList();
            if (candidates.Count == 0)
            {
                UiKit.Text("Empty", _panel, LibraryStore.Items.Count == 0
                    ? "Nothing imported yet. Import lectures from the menu (Esc → Import files) first."
                    : "Everything you've imported is already in this subject.", 19, TextAlignmentOptions.Left, UiKit.MutedTextColor);
                return;
            }
            UiKit.Text("Hint", _panel, "Click an item to move it into this subject.", 18, TextAlignmentOptions.Left, UiKit.MutedTextColor);
            RectTransform list = MenuWidgets.ScrollList(_panel, Mathf.Min(360, candidates.Count * 64 + 20), out _, 6);
            foreach (LibraryItem item in candidates)
            {
                SubjectBundle from = LibraryStore.Bundle(item.BundleId ?? "");
                string label = from == null ? item.Title : $"{item.Title}  <size=75%><alpha=#99>in {from.Name}</size>";
                Button b = UiKit.Button("Item", list, label, 19, () => { LibraryStore.Move(item, _bundle); ShowAdd(); },
                                        UiKit.PanelColor, ThemeArt.Icon(SubjectsScreen.KindIcon(item)));
                MenuWidgets.Size(b, height: 56);
                Transform glyph = b.transform.Find("IconBlock/Icon");
                if (glyph != null) glyph.GetComponent<Image>().color = UiKit.AccentColor;   // cream-on-sand is too faint
                TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        /// <summary>Pick the subject: sets the card's colour and symbols (and the game's, if it's the current one).</summary>
        void ShowSubjects()
        {
            Clear();
            Item("Back", "back", ShowMain);
            Title("Subject");
            RectTransform grid = UiKit.Rect("Subjects", _panel);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((Width - 32 - 10) / 2, 56);
            g.spacing = new Vector2(10, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            foreach (Subject s in LudifyTheme.All)
            {
                SubjectPalette p = LudifyTheme.For(s);
                Subject pick = s;
                Button b = UiKit.Button(p.Name, grid, p.Name, 20, () => { LibraryStore.SetSubject(_bundle, pick); Close(); },
                                        p.Accent, ThemeArt.Icon(s == _bundle.Subject ? "check" : SubjectsScreen.SubjectIcon(s)));
                SubjectsScreen.OneLine(b);
            }
            int rows = Mathf.CeilToInt(LudifyTheme.All.Count() / 2f);
            MenuWidgets.Size(grid, height: rows * 56 + (rows - 1) * 8);
        }
    }

    /// <summary>Swallows clicks so they don't fall through to whatever is behind (e.g. a menu's close-on-click backdrop).</summary>
    public sealed class ClickSink : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e) { }
    }

    /// <summary>Sizes a grid's cells to fill its width with as many columns as fit.</summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class FitGridColumns : MonoBehaviour
    {
        public float MinCellWidth = 460, CellHeight = 330;
        float _width = -1;

        void LateUpdate()
        {
            float w = ((RectTransform)transform).rect.width;
            if (Mathf.Approximately(w, _width) || w <= 0) return;
            _width = w;
            var g = GetComponent<GridLayoutGroup>();
            float inner = w - g.padding.left - g.padding.right;
            int cols = Mathf.Max(1, Mathf.FloorToInt((inner + g.spacing.x) / (MinCellWidth + g.spacing.x)));
            g.constraintCount = cols;
            g.cellSize = new Vector2((inner - g.spacing.x * (cols - 1)) / cols, CellHeight);
        }
    }

    /// <summary>Where library items can be dropped: a subject, the unsorted list (null), or the "New subject" tile.</summary>
    public sealed class DropZone : MonoBehaviour
    {
        public SubjectBundle Bundle;
        public bool CreatesBundle;
        public static void Add(GameObject go, SubjectBundle bundle) => go.AddComponent<DropZone>().Bundle = bundle;
    }

    /// <summary>A floating, slightly tilted copy of a UI element that follows the pointer while dragging.</summary>
    static class DragGhost
    {
        public static RectTransform Create(RectTransform source, Canvas canvas)
        {
            var ghost = (RectTransform)UnityEngine.Object.Instantiate(source.gameObject, canvas.transform).transform;
            foreach (var d in ghost.GetComponentsInChildren<ItemDrag>()) UnityEngine.Object.Destroy(d);
            foreach (var d in ghost.GetComponentsInChildren<BundleDrag>()) UnityEngine.Object.Destroy(d);
            foreach (var d in ghost.GetComponentsInChildren<DropZone>()) UnityEngine.Object.Destroy(d);
            var group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.alpha = 0.85f;
            ghost.sizeDelta = source.rect.size;
            ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.localRotation = Quaternion.Euler(0, 0, 3);
            return ghost;
        }

        public static void Follow(RectTransform ghost, Canvas canvas, Vector2 screen)
        {
            if (ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out Vector2 p);
            ghost.anchoredPosition = p;
        }
    }

    /// <summary>Drag a library item chip onto a subject (or back to Unsorted, or onto "New subject").</summary>
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
            _ghost = DragGhost.Create((RectTransform)transform, _canvas);
            _source.canvasRenderer.SetAlpha(0.35f);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e) => DragGhost.Follow(_ghost, _canvas, e.position);

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            if (_source != null) _source.canvasRenderer.SetAlpha(1f);
            GameObject over = e.pointerCurrentRaycast.gameObject;
            DropZone zone = over != null ? over.GetComponentInParent<DropZone>() : null;
            if (zone == null) return;
            if (zone.CreatesBundle)
            {
                SubjectsScreen screen = GetComponentInParent<SubjectsScreen>();   // before the store change rebuilds (and detaches) this chip
                SubjectBundle b = LibraryStore.CreateBundle("New subject", LudifyTheme.Guess(_item.Title));
                LibraryStore.Move(_item, b);
                if (screen != null) screen.StartRename(b);
            }
            else LibraryStore.Move(_item, zone.Bundle);
        }
    }

    /// <summary>Drag a subject card by its top bar to a new place in the list.</summary>
    public sealed class BundleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        SubjectsScreen _screen;
        SubjectBundle _bundle;
        RectTransform _card, _ghost;
        CanvasGroup _fade;
        Canvas _canvas;

        public void Init(SubjectsScreen screen, SubjectBundle bundle, RectTransform card)
        {
            _screen = screen;
            _bundle = bundle;
            _card = card;
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _screen.BeginCardDrag();
            _ghost = DragGhost.Create(_card, _canvas);
            if (!_card.TryGetComponent(out _fade)) _fade = _card.gameObject.AddComponent<CanvasGroup>();   // (?? doesn't see Unity's fake null)
            _fade.alpha = 0.3f;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            DragGhost.Follow(_ghost, _canvas, e.position);
            _screen.AutoScroll(e.position);
            int i = _screen.CardIndexAt(e.position);
            if (i >= 0 && i != _card.GetSiblingIndex()) _card.SetSiblingIndex(i);   // live preview; the grid re-flows
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            if (_fade != null) _fade.alpha = 1f;
            _screen.EndCardDrag(_bundle, _card.GetSiblingIndex());
        }
    }
}
