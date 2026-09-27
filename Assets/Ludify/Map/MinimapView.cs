using System.Collections.Generic;
using Ludify.Import;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ludify.Map
{
    /// <summary>Circular north-up minimap in the bottom-left corner. Click it to open the full map.</summary>
    public sealed class MinimapView : MonoBehaviour
    {
        const float Size = 240f;
        const float Border = 6f;
        const float MetersAcross = 120f;

        MapSystem _map;
        RawImage _image;
        RectTransform _player, _icons;
        readonly List<(FastTravelPoint Point, RectTransform Icon)> _pointIcons = new List<(FastTravelPoint, RectTransform)>();
        readonly List<(FastTravelPoint Point, RectTransform Icon)> _markerIcons = new List<(FastTravelPoint, RectTransform)>();

        public static MinimapView Create(MapSystem map, Transform canvas)
        {
            RectTransform root = UiKit.Place(UiKit.Rect("Minimap", canvas), Vector2.zero, new Vector2(28, 28), new Vector2(Size, Size));
            var view = root.gameObject.AddComponent<MinimapView>();
            view.Build(map);
            return view;
        }

        void Build(MapSystem map)
        {
            _map = map;

            Image shadow = UiKit.Image("Shadow", transform, new Color(0, 0, 0, 0.45f), UiKit.CircleSprite);
            UiKit.Stretch(shadow.rectTransform, -4);

            // The circular mask doubles as the click target.
            Image mask = UiKit.Image("Mask", transform, Color.white, UiKit.CircleSprite, raycast: true);
            UiKit.Stretch(mask.rectTransform, Border);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var button = mask.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(map.OpenFullMap);

            _image = UiKit.Rect("Map", mask.transform).gameObject.AddComponent<RawImage>();
            UiKit.Stretch(_image.rectTransform);
            _image.texture = map.Snapshot.Texture;
            _image.raycastTarget = false;

            _icons = UiKit.Stretch(UiKit.Rect("Icons", mask.transform));
            foreach (FastTravelPoint point in map.Points)
                _pointIcons.Add((point, PointIcon(point, _icons, 22, 14)));
            RebuildMarkers();
            MapMarkers.Changed += RebuildMarkers;

            _player = UiKit.Place(UiKit.Image("Player", mask.transform, Color.white, UiKit.ArrowSprite).rectTransform,
                                  new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));

            UiKit.Stretch(UiKit.Image("Border", transform, new Color(1, 1, 1, 0.9f), UiKit.RingSprite).rectTransform);

            TextMeshProUGUI hint = UiKit.Text("Hint", transform, "Map  [M]", 18);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 26), new Vector2(Size, 24));
            hint.fontStyle = FontStyles.Bold;
            hint.outlineWidth = 0.2f;
            hint.outlineColor = Color.black;
        }

        /// <summary>Coloured dot with the point's first letter.</summary>
        public static RectTransform PointIcon(FastTravelPoint point, Transform parent, float size, float fontSize)
        {
            Image dot = UiKit.Image(point.Name, parent, point.Color, UiKit.CircleSprite);
            UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            TextMeshProUGUI letter = UiKit.Text("Letter", dot.transform, point.IconText, fontSize);
            letter.fontStyle = FontStyles.Bold;
            UiKit.Stretch(letter.rectTransform);
            return dot.rectTransform;
        }

        void OnDestroy() => MapMarkers.Changed -= RebuildMarkers;

        /// <summary>Recreate the icons for runtime markers (<see cref="MapMarkers"/>) when the set changes.</summary>
        void RebuildMarkers()
        {
            foreach (var (_, icon) in _markerIcons)
                if (icon != null) Destroy(icon.gameObject);
            _markerIcons.Clear();
            foreach (FastTravelPoint point in MapMarkers.All)
                _markerIcons.Add((point, PointIcon(point, _icons, 20, 15)));
        }

        void LateUpdate()
        {
            Transform focus = _map.Focus;
            if (focus == null) return;
            MapMarkers.UpdatePositions();

            MapSnapshot snapshot = _map.Snapshot;
            Vector2 uv = snapshot.WorldToUv(focus.position);
            float span = MetersAcross / snapshot.WorldRect.width;
            _image.uvRect = new Rect(uv.x - span / 2, uv.y - span / 2, span, span);
            _player.localRotation = Quaternion.Euler(0, 0, -focus.eulerAngles.y);

            float inner = Size - 2 * Border;
            float maxRadius = inner / 2 - 12;
            PlaceIcons(_pointIcons, focus.position, inner, maxRadius);
            PlaceIcons(_markerIcons, focus.position, inner, maxRadius);
        }

        static void PlaceIcons(List<(FastTravelPoint Point, RectTransform Icon)> icons, Vector3 focus, float inner, float maxRadius)
        {
            foreach (var (point, icon) in icons)
            {
                Vector2 offset = new Vector2(point.Position.x - focus.x, point.Position.z - focus.z)
                                 / MetersAcross * inner;
                icon.anchoredPosition = Vector2.ClampMagnitude(offset, maxRadius); // off-map points sit on the rim
            }
        }
    }
}
