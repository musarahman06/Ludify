using System.Linq;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ludify.Gallery
{
    /// <summary>
    /// Close-up view of an exhibit: the camera orbits the pedestal (drag to look around, Ctrl+scroll to
    /// zoom), and a side panel explains it, with Replace / Remove / Back. The player is frozen meanwhile.
    /// </summary>
    public sealed class InspectMode : MonoBehaviour
    {
        const float OrbitDistance = 6.5f;

        GallerySystem _gallery;
        OrbitCamera _camera;
        PlayerController _player;
        Pedestal _pedestal;
        Transform _previousTarget;
        float _previousDistance;
        bool _playerWasEnabled;
        int _beganFrame;
        GameObject _panel;
        TextMeshProUGUI _title, _body;

        public bool IsActive => _pedestal != null;

        public void Init(GallerySystem gallery)
        {
            _gallery = gallery;
            _camera = FindAnyObjectByType<OrbitCamera>();
            _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            BuildPanel();
        }

        public void Begin(Pedestal pedestal)
        {
            if (IsActive || _camera == null) return;
            _pedestal = pedestal;
            _beganFrame = Time.frameCount;   // the same I press must not also close it
            _previousTarget = _camera.target;
            _previousDistance = _camera.distance;
            _camera.SetTarget(pedestal.Focus, OrbitDistance);
            _camera.yaw = pedestal.transform.eulerAngles.y;     // start in front of the exhibit
            if (_player != null) { _playerWasEnabled = _player.enabled; _player.enabled = false; }

            SceneModel m = pedestal.Record.Model;
            _title.text = m?.Title ?? "Exhibit";
            string parts = m != null && m.IsModel
                ? string.Join(", ", m.Parts.Select(p => p.Label).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().Take(14))
                : "";
            _body.text = $"<i>{m?.Subject}</i>\n\n{m?.Explanation}" +
                         (parts.Length > 0 ? $"\n\n<b>Parts:</b> {parts}" : "") +
                         "\n\n<size=80%><color=#9fb3c8>Drag to look around · Ctrl+scroll to zoom · Esc to go back</color></size>";
            _panel.SetActive(true);
        }

        public void End()
        {
            if (!IsActive) return;
            _pedestal = null;
            _panel.SetActive(false);
            if (_camera != null && _previousTarget != null) _camera.SetTarget(_previousTarget, _previousDistance);
            if (_player != null) _player.enabled = _playerWasEnabled;
        }

        void Update()
        {
            if (!IsActive || Time.frameCount == _beganFrame) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) && !LessonFilePicker.IsOpen) End();
        }

        void BuildPanel()
        {
            Canvas canvas = UiKit.CreateCanvas("InspectCanvas", 46);
            canvas.transform.SetParent(transform, false);
            Image panel = UiKit.Image("Panel", canvas.transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            panel.type = Image.Type.Sliced;
            UiKit.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-30, 0), new Vector2(560, 0));
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 26, 26);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = UiKit.Text("Title", panel.transform, "", 32, TextAlignmentOptions.Left, UiKit.AccentColor);
            _title.fontStyle = FontStyles.Bold;
            _body = UiKit.Text("Body", panel.transform, "", 21, TextAlignmentOptions.TopLeft);

            Transform row = UiKit.Rect("Buttons", panel.transform);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            UiKit.Button("Replace", row, "Replace image", 20, () => { Pedestal p = _pedestal; End(); _gallery.AddImage(p); }, UiKit.AccentColor);
            UiKit.Button("Remove", row, "Remove", 20, () => { Pedestal p = _pedestal; End(); _gallery.Remove(p); }, UiKit.WrongColor);
            UiKit.Button("Back", row, "Back", 20, End);
            _panel = panel.gameObject;
            _panel.SetActive(false);
        }
    }
}
