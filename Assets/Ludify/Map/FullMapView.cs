using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Map
{
    /// <summary>Full-screen map with clickable fast-travel markers.</summary>
    public sealed class FullMapView : MonoBehaviour
    {
        const float MapSize = 820f;

        MapSystem _map;
        RectTransform _mapRect, _player;
        TextMeshProUGUI _status;

        public static FullMapView Create(MapSystem map, Transform canvas)
        {
            RectTransform root = UiKit.Stretch(UiKit.Rect("FullMap", canvas));
            var view = root.gameObject.AddComponent<FullMapView>();
            view.Build(map);
            root.gameObject.SetActive(false);
            return view;
        }

        void Build(MapSystem map)
        {
            _map = map;
            UiKit.Stretch(UiKit.Image("Dim", transform, new Color(0, 0, 0, 0.75f), raycast: true).rectTransform);

            Image frame = UiKit.Image("Frame", transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            frame.type = Image.Type.Sliced;
            UiKit.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(MapSize + 24, MapSize + 24));

            var image = UiKit.Rect("Map", frame.transform).gameObject.AddComponent<RawImage>();
            _mapRect = UiKit.Stretch(image.rectTransform, 12);
            image.texture = map.Snapshot.Texture;
            image.raycastTarget = false;

            foreach (FastTravelPoint point in map.Points) AddMarker(point);

            _player = UiKit.Image("Player", _mapRect, Color.white, UiKit.ArrowSprite).rectTransform;
            _player.anchorMin = _player.anchorMax = Vector2.zero;
            _player.sizeDelta = new Vector2(30, 30);

            TextMeshProUGUI title = UiKit.Text("Title", transform, "Map", 40);
            title.fontStyle = FontStyles.Bold;
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -62), new Vector2(800, 50)); // below the Import lecture button

            _status = UiKit.Text("Status", transform, "", 22, TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 1f));
            UiKit.Place(_status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -114), new Vector2(1200, 34));

            Button close = UiKit.Button("Close", frame.transform, "X", 28, map.CloseFullMap, UiKit.WrongColor);
            UiKit.Place((RectTransform)close.transform, Vector2.one, new Vector2(20, 20), new Vector2(52, 52));

            TextMeshProUGUI help = UiKit.Text("Help", transform, "Esc / M to close", 18, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.6f));
            UiKit.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(600, 28));
        }

        void AddMarker(FastTravelPoint point)
        {
            Vector2 uv = _map.Snapshot.WorldToUv(point.Position);
            RectTransform marker = UiKit.Rect(point.Name, _mapRect);
            marker.anchorMin = marker.anchorMax = uv;
            marker.sizeDelta = new Vector2(44, 44);

            Button dot = UiKit.Button("Dot", marker, null, 0, () => _map.RequestTravel(point), point.Color);
            ((Image)dot.targetGraphic).sprite = UiKit.CircleSprite;
            ((Image)dot.targetGraphic).type = Image.Type.Simple;
            UiKit.Stretch((RectTransform)dot.transform);
            TextMeshProUGUI letter = UiKit.Text("Letter", dot.transform, point.Name.Substring(0, 1), 24);
            letter.fontStyle = FontStyles.Bold;
            UiKit.Stretch(letter.rectTransform);

            // Name label under the dot; also clickable.
            Button label = UiKit.Button("Label", marker, point.Name, 20, () => _map.RequestTravel(point), new Color(0, 0, 0, 0.7f));
            UiKit.Place((RectTransform)label.transform, new Vector2(0.5f, 0f), new Vector2(0, -34), new Vector2(170, 32));
        }

        public void SetStatus(string text) => _status.text = text;

        void LateUpdate()
        {
            Transform focus = _map.Focus;
            if (focus == null) return;
            Vector2 uv = _map.Snapshot.WorldToUv(focus.position);
            _player.anchoredPosition = new Vector2(uv.x * _mapRect.rect.width, uv.y * _mapRect.rect.height);
            _player.localRotation = Quaternion.Euler(0, 0, -focus.eulerAngles.y);
        }
    }
}
