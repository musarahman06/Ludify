using System.Linq;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ludify.Gallery
{
    /// <summary>
    /// Hands-on viewer: the exhibit floats up in front of its pedestal and a dedicated camera takes
    /// over. Drag to spin it freely, right-drag to pan, scroll to zoom, F to flip, X to explode,
    /// R to reset, hover parts for details, click switches to operate circuits. Esc / I to go back.
    /// The game is paused meanwhile (<see cref="ModalGuard"/>); everything here uses unscaled time.
    /// </summary>
    public sealed class ExhibitViewer : MonoBehaviour
    {
        const float CameraPitch = 18f;
        const float RotateSpeed = 0.35f;     // degrees per pixel
        const float MoveSeconds = 0.6f;

        GallerySystem _gallery;
        Pedestal _pedestal;
        ExhibitInfo _info;
        Transform _pivot, _exhibit, _originalParent;
        Vector3 _originalLocalPos, _originalLocalScale;
        Quaternion _originalLocalRot, _homeRotation, _targetRotation;
        Vector3 _home, _focus;
        float _size, _distance, _homeDistance, _moveT;
        bool _animatingRotation;

        Camera _camera, _mainCamera;
        GameObject _panel, _tooltip;
        TextMeshProUGUI _title, _body, _tooltipText;
        Button _refresh, _explodeButton, _article;
        ExhibitPart _hovered;
        Vector2 _pressPos;
        float _pressTime, _lastClickTime;
        ExhibitPart _lastClicked;
        int _beganFrame;

        public bool IsActive => _pedestal != null;

        public void Init(GallerySystem gallery)
        {
            _gallery = gallery;
            BuildUi();
        }

        // ---- Enter / leave ----

        public void Begin(Pedestal pedestal)
        {
            if (IsActive || pedestal.Exhibit == null) return;
            _pedestal = pedestal;
            _info = pedestal.Info;
            _exhibit = pedestal.Exhibit;
            _beganFrame = Time.frameCount;
            ModalGuard.Push();
            pedestal.InViewer = true;

            _originalParent = _exhibit.parent;
            _originalLocalPos = _exhibit.localPosition;
            _originalLocalRot = _exhibit.localRotation;
            _originalLocalScale = _exhibit.localScale;

            // Start from the exhibit's built orientation, its front (−Z) facing the viewer.
            Quaternion facing = Quaternion.Euler(0, pedestal.transform.eulerAngles.y, 0);
            _exhibit.rotation = facing;

            // Pivot at the exhibit's visual centre so it spins around its middle.
            Bounds b = VisualBounds(_exhibit);
            _size = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.5f);
            _pivot = new GameObject("ViewerPivot").transform;
            _pivot.SetPositionAndRotation(b.center, Quaternion.identity);
            _exhibit.SetParent(_pivot, true);

            // Float to eye level, a little in front of the pedestal.
            _home = pedestal.transform.position + Vector3.up * 2.2f - pedestal.transform.forward * 1.0f;
            _focus = _home;
            Quaternion look = facing * Quaternion.Euler(CameraPitch, 0, 0);
            _homeDistance = _distance = _size * 1.35f + 0.8f;

            // Circuit boards: tip the top face toward the camera so you look at the components, not the edge.
            _homeRotation = _info != null && _info.IsCircuit
                ? Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, -(look * Vector3.forward), 0.6f).normalized)
                : Quaternion.identity;
            _targetRotation = _homeRotation;
            _animatingRotation = true;
            _moveT = 0;

            // Our own camera; the game camera is switched off (ModalGuard already paused its script).
            _mainCamera = Camera.main;
            var camGo = new GameObject("ExhibitViewerCamera") { tag = "MainCamera" };
            _camera = camGo.AddComponent<Camera>();
            if (_mainCamera != null)
            {
                _camera.CopyFrom(_mainCamera);
                _mainCamera.enabled = false;
            }
            _camera.nearClipPlane = 0.03f;
            _camera.transform.rotation = look;
            _camera.transform.position = _mainCamera != null ? _mainCamera.transform.position : _focus - look * Vector3.forward * _distance;

            _info?.SetInteractive(true);
            FillPanel();
            _panel.SetActive(true);
        }

        public void End()
        {
            if (!IsActive) return;
            if (_hovered != null) _hovered.SetHighlight(false);
            _hovered = null;
            _tooltip.SetActive(false);
            _panel.SetActive(false);

            if (_info != null)
            {
                _info.SetInteractive(false);
                _info.SetExploded(false);
                foreach (ExhibitPart p in _info.Parts) p.transform.localPosition = p.BasePosition;
            }
            if (_exhibit != null)
            {
                _exhibit.SetParent(_originalParent, false);
                _exhibit.localPosition = _originalLocalPos;
                _exhibit.localRotation = _originalLocalRot;
                _exhibit.localScale = _originalLocalScale;
            }
            if (_pivot != null) Destroy(_pivot.gameObject);
            if (_camera != null) Destroy(_camera.gameObject);
            if (_mainCamera != null) _mainCamera.enabled = true;
            if (_pedestal != null) _pedestal.InViewer = false;

            _pedestal = null;
            _info = null;
            _exhibit = null;
            ModalGuard.Pop();
        }

        /// <summary>Re-opens the viewer after the exhibit was rebuilt (e.g. details refreshed).</summary>
        public void Reopen(Pedestal pedestal)
        {
            End();
            Begin(pedestal);
        }

        // ---- Per-frame ----

        void Update()
        {
            if (!IsActive) return;
            if (_exhibit == null) { End(); return; }
            float dt = Time.unscaledDeltaTime;

            // Fly the exhibit up to eye level, then keep the camera on the focus point.
            _moveT = Mathf.Min(1f, _moveT + dt / MoveSeconds);
            float ease = Mathf.SmoothStep(0, 1, _moveT);
            _pivot.position = Vector3.Lerp(_pivot.position, _home, ease);
            if (_animatingRotation)
            {
                _pivot.rotation = Quaternion.Slerp(_pivot.rotation, _targetRotation, 1f - Mathf.Exp(-8f * dt));
                if (Quaternion.Angle(_pivot.rotation, _targetRotation) < 0.3f) { _pivot.rotation = _targetRotation; _animatingRotation = false; }
            }
            Vector3 camTarget = _focus - _camera.transform.forward * _distance;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, camTarget, 1f - Mathf.Exp(-10f * dt));

            if (Time.frameCount == _beganFrame || ImportPanel.IsShowing || LessonFilePickerOpen()) return;
            HandleKeys();
            HandleMouse();
        }

        static bool LessonFilePickerOpen() => SimpleFileBrowser.FileBrowser.IsOpen;

        void HandleKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) { End(); return; }
            if (kb.fKey.wasPressedThisFrame) Flip();
            if (kb.xKey.wasPressedThisFrame && _info != null && !_info.Animated) ToggleExplode();
            if (kb.rKey.wasPressedThisFrame) ResetView();
        }

        void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 pos = mouse.position.ReadValue();
            Vector2 delta = mouse.delta.ReadValue();

            if (!overUi)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float notches = scroll / 120f;
                    if (Mathf.Abs(notches) < 0.05f) notches = Mathf.Sign(scroll) * 0.25f;   // trackpads report small values
                    _distance = Mathf.Clamp(_distance * Mathf.Pow(0.85f, notches), _size * 0.12f, _size * 4f);
                }
            }

            if (mouse.leftButton.wasPressedThisFrame && !overUi) { _pressPos = pos; _pressTime = Time.unscaledTime; }
            if (mouse.leftButton.isPressed && (pos - _pressPos).sqrMagnitude > 25f && _pressTime > 0)
            {
                // Trackball: horizontal drag spins around the screen's up axis, vertical around its right axis.
                Transform cam = _camera.transform;
                Quaternion spin = Quaternion.AngleAxis(-delta.x * RotateSpeed, cam.up) * Quaternion.AngleAxis(delta.y * RotateSpeed, cam.right);
                _pivot.rotation = spin * _pivot.rotation;
                _targetRotation = _pivot.rotation;
                _animatingRotation = false;
            }
            if (mouse.leftButton.wasReleasedThisFrame && _pressTime > 0)
            {
                if ((pos - _pressPos).sqrMagnitude <= 25f && Time.unscaledTime - _pressTime < 0.5f) Click();
                _pressTime = 0;
            }
            if (mouse.rightButton.isPressed && !overUi)
            {
                float perPixel = _distance * 0.0018f;
                _focus += (-_camera.transform.right * delta.x - _camera.transform.up * delta.y) * perPixel;
            }

            UpdateHover(overUi ? (Vector2?)null : pos);
        }

        // Parts under the cursor, innermost (smallest) first: a nucleus wins over the cell membrane around it.
        readonly System.Collections.Generic.List<ExhibitPart> _underCursor = new System.Collections.Generic.List<ExhibitPart>();
        int _cycle;
        Vector2 _lastHoverPos;

        void UpdateHover(Vector2? screen)
        {
            ExhibitPart hit = null;
            if (screen.HasValue && _info != null)
            {
                Ray ray = _camera.ScreenPointToRay(screen.Value);
                var parts = new System.Collections.Generic.List<(ExhibitPart part, float volume)>();
                foreach (RaycastHit h in Physics.RaycastAll(ray, _distance * 4f, ~0, QueryTriggerInteraction.Collide))
                {
                    ExhibitPart part = h.collider.GetComponent<ExhibitPart>();
                    if (part == null || !_info.Parts.Contains(part) || parts.Any(x => x.part == part)) continue;
                    Vector3 size = h.collider.bounds.size;
                    parts.Add((part, size.x * size.y * size.z));
                }
                var ordered = parts.OrderBy(x => x.volume).Select(x => x.part).ToList();
                if ((screen.Value - _lastHoverPos).sqrMagnitude > 16f || !ordered.SequenceEqual(_underCursor)) _cycle = 0;
                _lastHoverPos = screen.Value;
                _underCursor.Clear();
                _underCursor.AddRange(ordered);
                if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && _underCursor.Count > 1) _cycle++;
                if (_underCursor.Count > 0) hit = _underCursor[_cycle % _underCursor.Count];
            }
            if (hit != _hovered)
            {
                if (_hovered != null) _hovered.SetHighlight(false);
                _hovered = hit;
                if (_hovered != null) _hovered.SetHighlight(true);
            }
            _tooltip.SetActive(_hovered != null);
            if (_hovered != null)
            {
                _tooltipText.text = _hovered.Describe(_info.Sim) +
                    (_underCursor.Count > 1 ? $"\n<size=80%><color=#9fb3c8>Tab: next part here ({_underCursor.Count})</color></size>" : "");
                var rt = (RectTransform)_tooltip.transform;
                var canvas = (RectTransform)rt.parent;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen.Value, null, out Vector2 local);
                // Keep the tooltip on screen: flip to the left of the cursor near the right edge.
                bool right = screen.Value.x > Screen.width * 0.6f;
                rt.pivot = new Vector2(right ? 1 : 0, 1);
                rt.anchoredPosition = local + new Vector2(right ? -24 : 24, -24);
            }
        }

        void Click()
        {
            ExhibitPart part = _hovered;
            if (part == null) return;
            bool doubleClick = part == _lastClicked && Time.unscaledTime - _lastClickTime < 0.35f;
            _lastClicked = part;
            _lastClickTime = Time.unscaledTime;

            if (doubleClick)
            {
                Bounds b = VisualBounds(part.transform);
                _focus = b.center;
                _distance = Mathf.Clamp(Mathf.Max(b.size.x, b.size.y, b.size.z) * 3f, _size * 0.12f, _size * 4f);
                return;
            }
            if (_info.Sim != null && _info.Sim.IsSwitch(part.Data.Id)) _info.Sim.Toggle(part.Data.Id);
        }

        // ---- Actions ----

        void Flip()
        {
            _targetRotation = Quaternion.AngleAxis(180f, _camera.transform.right) * (_animatingRotation ? _targetRotation : _pivot.rotation);
            _animatingRotation = true;
        }

        void ToggleExplode()
        {
            if (_info == null) return;
            _info.SetExploded(!_info.Exploded);
            UiKit.SetButtonLabel(_explodeButton, _info.Exploded ? "Rebuild  (X)" : "Explode  (X)");
        }

        void ResetView()
        {
            _targetRotation = _homeRotation;
            _animatingRotation = true;
            _focus = _home;
            _distance = _homeDistance;
            if (_info != null && _info.Exploded) ToggleExplode();
        }

        static Bounds VisualBounds(Transform t)
        {
            Renderer[] rs = t.GetComponentsInChildren<Renderer>().Where(r => r.GetComponent<TMP_Text>() == null && r.name != "Electron").ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.one);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ---- UI ----

        void BuildUi()
        {
            Canvas canvas = UiKit.CreateCanvas("ExhibitViewerCanvas", 160);
            canvas.transform.SetParent(transform, false);

            Image panel = UiKit.Image("Panel", canvas.transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            panel.type = Image.Type.Sliced;
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24, 0), new Vector2(540, 0));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 26, 24, 24);
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = UiKit.Text("Title", panel.transform, "", 30, TextAlignmentOptions.Left, UiKit.AccentColor);
            _title.fontStyle = FontStyles.Bold;
            _body = UiKit.Text("Body", panel.transform, "", 19, TextAlignmentOptions.TopLeft);

            Transform row1 = Row(panel.transform);
            UiKit.Button("Flip", row1, "Flip  (F)", 17, Flip, UiKit.ButtonColor);
            _explodeButton = UiKit.Button("Explode", row1, "Explode  (X)", 17, ToggleExplode, UiKit.ButtonColor);
            UiKit.Button("Reset", row1, "Reset  (R)", 17, ResetView, UiKit.ButtonColor);

            Transform row2 = Row(panel.transform);
            _article = UiKit.Button("Article", row1, "Read more", 17, () =>
            {
                string url = _pedestal?.Record?.Model?.Reference?.Url;
                if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
            }, UiKit.ButtonColor);
            _refresh = UiKit.Button("Refresh", row2, "Add details", 17, () => { Pedestal p = _pedestal; _gallery.RefreshDetails(p); }, UiKit.AccentColor);
            UiKit.Button("Replace", row2, "Replace", 17, () => { Pedestal p = _pedestal; End(); _gallery.AddImage(p); }, UiKit.ButtonColor);
            UiKit.Button("Remove", row2, "Remove", 17, () => { Pedestal p = _pedestal; End(); _gallery.Remove(p); }, UiKit.WrongColor);
            UiKit.Button("Back", row2, "Back", 17, End);
            _panel = panel.gameObject;
            _panel.SetActive(false);

            Image tip = UiKit.Image("Tooltip", canvas.transform, new Color(0.05f, 0.07f, 0.1f, 0.94f), UiKit.RoundedSprite);
            tip.type = Image.Type.Sliced;
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            tip.rectTransform.sizeDelta = new Vector2(430, 0);
            var tipLayout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
            tipLayout.padding = new RectOffset(16, 16, 12, 12);
            tipLayout.childControlWidth = tipLayout.childControlHeight = true;
            tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _tooltipText = UiKit.Text("Text", tip.transform, "", 19, TextAlignmentOptions.TopLeft);
            _tooltip = tip.gameObject;
            _tooltip.SetActive(false);
        }

        static Transform Row(Transform parent)
        {
            Transform row = UiKit.Rect("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
            return row;
        }

        void FillPanel()
        {
            SceneModel m = _pedestal.Record?.Model;
            _title.text = m?.Title ?? "Exhibit";
            bool circuit = _info != null && _info.IsCircuit;
            string how =
                "<b>Drag</b> to turn it any way  ·  <b>Scroll</b> to zoom  ·  <b>Right-drag</b> to move\n" +
                "<b>Hover</b> a part for details (<b>Tab</b> picks parts behind it)  ·  <b>Double-click</b> a part to zoom to it" +
                (circuit ? "\n<b>Click the switch</b> to turn the circuit on or off" : "");
            string mode = m?.Mode == "concept" ? "Concept model" : "Traced from the image";
            string identified = string.IsNullOrWhiteSpace(m?.Identified) ? "" : $"\n<size=85%><color=#9fb3c8>Gemini saw: {m.Identified}</color></size>";
            string facts = m?.Reference != null && !string.IsNullOrWhiteSpace(m.Reference.Extract)
                ? $"\n\n<b>From Wikipedia · {m.Reference.Title}</b>\n<size=90%>{m.Reference.Extract}</size>" : "";
            _body.text = $"<i>{m?.Subject}  ·  {mode}</i>{identified}\n{m?.Explanation}{facts}\n\n<size=85%><color=#b8c7d9>{how}</color></size>";
            _article.gameObject.SetActive(!string.IsNullOrEmpty(m?.Reference?.Url));

            bool missingInfo = m != null && m.IsModel && m.Parts.Any(p => string.IsNullOrWhiteSpace(p.Info));
            _refresh.gameObject.SetActive(missingInfo && _pedestal.Record?.ImageFile != null);
            _explodeButton.gameObject.SetActive(_info != null && _info.Parts.Count > 1 && !_info.Animated);
            UiKit.SetButtonLabel(_explodeButton, "Explode  (X)");
        }
    }
}
