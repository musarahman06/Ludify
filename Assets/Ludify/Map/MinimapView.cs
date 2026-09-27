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
        CanvasGroup _group;
        readonly List<(FastTravelPoint Point, RectTransform Icon)> _pointIcons = new List<(FastTravelPoint, RectTransform)>();
        readonly List<(FastTravelPoint Point, RectTransform Icon)> _markerIcons = new List<(FastTravelPoint, RectTransform)>();
        readonly List<(FastTravelPoint Point, RectTransform Circle)> _areas = new List<(FastTravelPoint, RectTransform)>();

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

            UiKit.Stretch(UiKit.Image("Border", transform, UiKit.PanelColor, UiKit.RingSprite).rectTransform);
            AddCompass();
            _group = gameObject.AddComponent<CanvasGroup>();

            TextMeshProUGUI hint = UiKit.Text("Hint", transform, "Map  [M]", 18);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 40), new Vector2(Size, 24)); // above the N
            hint.fontStyle = FontStyles.Bold;
            hint.outlineWidth = 0.2f;
            hint.outlineColor = Color.black;
        }

        /// <summary>N / E / S / W on the rim. The minimap is always north-up (+Z), so these never move.</summary>
        void AddCompass()
        {
            float r = Size / 2 - Border / 2;
            foreach (var (letter, dir) in new[] { ("N", Vector2.up), ("E", Vector2.right), ("S", Vector2.down), ("W", Vector2.left) })
            {
                Image badge = UiKit.Image("Compass" + letter, transform, new Color(0.1f, 0.12f, 0.18f, 0.95f), UiKit.CircleSprite);
                UiKit.Place(badge.rectTransform, new Vector2(0.5f, 0.5f), dir * r, new Vector2(26, 26));
                TextMeshProUGUI text = UiKit.Text("Letter", badge.transform, letter, 16, TextAlignmentOptions.Center,
                                                  letter == "N" ? new Color(1f, 0.45f, 0.4f) : Color.white);
                text.fontStyle = FontStyles.Bold;
                UiKit.Stretch(text.rectTransform);
            }
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
            foreach (var (_, circle) in _areas)
                if (circle != null) Destroy(circle.gameObject);
            _markerIcons.Clear();
            _areas.Clear();
            foreach (FastTravelPoint point in MapMarkers.All)
            {
                if (point.Radius > 0) _areas.Add((point, AreaCircle(point, _icons)));
                _markerIcons.Add((point, PointIcon(point, _icons, 20, 15)));
            }
        }

        void LateUpdate()
        {
            // Out of the way while the Esc menu or another pausing panel is up (they cover this corner).
            bool hidden = ModalGuard.IsOpen;
            _group.alpha = hidden ? 0 : 1;
            _group.blocksRaycasts = !hidden;
            if (hidden) return;

            Transform focus = _map.Focus;
            if (focus == null) return;
            MapMarkers.UpdatePositions();

            MapSnapshot snapshot = _map.Snapshot;
            if (snapshot == null) return;   // e.g. right after a script reload in the Editor
            Vector2 uv = snapshot.WorldToUv(focus.position);
            float span = MetersAcross / snapshot.WorldRect.width;
            _image.uvRect = new Rect(uv.x - span / 2, uv.y - span / 2, span, span);
            _player.localRotation = Quaternion.Euler(0, 0, -focus.eulerAngles.y);

            float inner = Size - 2 * Border;
            float maxRadius = inner / 2 - 12;
            PlaceIcons(_pointIcons, focus.position, inner, maxRadius);
            PlaceIcons(_markerIcons, focus.position, inner, maxRadius);

            // Area circles aren't pinned to the rim; the round mask clips whatever is off the minimap.
            foreach (var (point, circle) in _areas)
            {
                circle.anchoredPosition = new Vector2(point.Position.x - focus.position.x, point.Position.z - focus.position.z)
                                          / MetersAcross * inner;
                float diameter = point.Radius * 2 / MetersAcross * inner;
                circle.sizeDelta = new Vector2(diameter, diameter);
            }
        }

        /// <summary>Translucent filled circle with an outline, behind the other icons (sized by the caller).</summary>
        public static RectTransform AreaCircle(FastTravelPoint point, Transform parent)
        {
            Color c = point.Color;
            Image fill = UiKit.Image(point.Name + " area", parent, new Color(c.r, c.g, c.b, 0.22f), UiKit.CircleSprite);
            UiKit.Place(fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            fill.rectTransform.SetAsFirstSibling();
            UiKit.Stretch(UiKit.Image("Outline", fill.transform, new Color(c.r, c.g, c.b, 0.9f), UiKit.RingSprite).rectTransform);
            return fill.rectTransform;
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
