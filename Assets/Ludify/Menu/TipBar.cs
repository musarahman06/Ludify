using System;
using System.Collections.Generic;
using Ludify.Gallery;
using Ludify.Import;
using Ludify.Map;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ludify.Menu
{
    /// <summary>
    /// Tip bar at the top of the screen: tells the player what they can do where they are (racetrack, farm,
    /// suburbs, city, gallery, driving, …), rotating through a few tips per place. Click it for the next tip.
    /// Shows import progress instead while a lecture is being imported. Hidden while menus, the full map, prompts,
    /// the loading screen or a race are up. Installs itself in any scene with a player (no scene edits); places
    /// are found from the live scene, reading (never editing) teammates' objects.
    /// </summary>
    public sealed class TipBar : MonoBehaviour
    {
        const int CanvasOrder = 32;
        const float TipSeconds = 9f, FadeSeconds = 0.18f, CheckSeconds = 0.4f;

        sealed class Place
        {
            public string Name, Icon;
            public string[] Tips;
            public Func<Vector3, bool> Contains;
        }

        const string NoLecturesTip = "No lectures yet: Menu → Import files turns your notes into practice questions";

        static readonly Place Driving = new Place
        {
            Name = "Driving", Icon = "car",
            Tips = new[]
            {
                "W / S to speed up and brake · A / D to steer · Space for the handbrake",
                "Race a Knowledge Time Trial: right answers give you a speed boost",
                "Slow down, then press X to get out",
            },
        };

        static readonly Place Biking = new Place
        {
            Name = "Paper round", Icon = "house",
            Tips = new[] { "Ride up to a glowing mailbox and press E to throw a paper", "Finish quickly for a bonus" },
        };

        static readonly Place Anywhere = new Place
        {
            Name = "Explore", Icon = "map",
            Tips = new[]
            {
                "Press M for the map. Click a place to fast travel there (answer a question first)",
                "Open the menu (top-left, or Esc) to import lectures and organize subjects",
            },
        };

        readonly List<Place> _places = new List<Place>();
        TrackPath _track;
        Transform _player;
        MapSystem _map;
        bool _inCar, _placesReady;

        RectTransform _bar;
        CanvasGroup _group, _content;
        Image _iconBack, _icon;
        TextMeshProUGUI _placeText, _tipText;

        Place _place;
        string[] _tips;
        int _tipIndex;
        float _nextTipAt, _nextCheckAt;
        string _wantPlace, _wantTip, _wantIcon;

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
            if (FindAnyObjectByType<TipBar>() != null) return;
            if (FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null) return;
            new GameObject(nameof(TipBar)).AddComponent<TipBar>();
        }

        void OnEnable()
        {
            VehicleInteraction.CarEntered += OnCarEntered;
            VehicleInteraction.CarExited += OnCarExited;
        }

        void OnDisable()
        {
            VehicleInteraction.CarEntered -= OnCarEntered;
            VehicleInteraction.CarExited -= OnCarExited;
        }

        void OnCarEntered(CarController car) { _inCar = true; _nextCheckAt = 0; }
        void OnCarExited(CarController car) { _inCar = false; _nextCheckAt = 0; }

        void Start()
        {
            ImportButtonOverlay.StatusShownElsewhere = true;
            _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include)?.transform;
            Build();
        }

        void OnDestroy() => ImportButtonOverlay.StatusShownElsewhere = false;

        void Build()
        {
            Canvas canvas = UiKit.CreateCanvas("TipBarCanvas", CanvasOrder);
            canvas.transform.SetParent(transform, false);
            _group = canvas.gameObject.AddComponent<CanvasGroup>();

            Image bar = UiKit.Image("TipBar", canvas.transform, UiKit.PanelColor, UiKit.RoundedSprite, raycast: true);
            _bar = UiKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(600, 68));
            var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 28, 9, 9);
            h.spacing = 14;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var next = bar.gameObject.AddComponent<Button>();
            next.transition = Selectable.Transition.None;
            next.onClick.AddListener(NextTip);
            _content = bar.gameObject.AddComponent<CanvasGroup>();

            _iconBack = UiKit.Image("IconBack", bar.transform, UiKit.AccentColor, UiKit.CircleSprite);
            MenuWidgets.Size(_iconBack, width: 50, height: 50, flex: 0);
            _icon = UiKit.Image("Icon", _iconBack.transform, LudifyTheme.Palette.LightText, ThemeArt.Icon("bulb"));
            _icon.preserveAspect = true;
            UiKit.Stretch(_icon.rectTransform, 10);

            _placeText = UiKit.Text("Place", bar.transform, "", 24, TextAlignmentOptions.MidlineLeft);
            DestroyImmediate(_placeText.GetComponent<AutoContrastText>());   // keeps the subject colour instead
            ThemedGraphic.Attach(_placeText, ThemeRole.AccentDark);
            _placeText.textWrappingMode = TextWrappingModes.NoWrap;

            Image divider = UiKit.Image("Divider", bar.transform, UiKit.MutedTextColor);
            MenuWidgets.Size(divider, width: 3, height: 34, flex: 0);

            _tipText = UiKit.Text("Tip", bar.transform, "", 22, TextAlignmentOptions.MidlineLeft);
            _tipText.textWrappingMode = TextWrappingModes.NoWrap;
            _group.alpha = 0;
        }

        // ---- Places ----

        /// <summary>Works out where the racetrack, farm and suburbs are from the scene (once it has finished building).</summary>
        void ResolvePlaces()
        {
            _placesReady = true;
            _places.Clear();
            _track = TrackPath.Load();

            _places.Add(new Place
            {
                Name = "Art gallery", Icon = "palette", Contains = GalleryArea.Contains,
                Tips = new[]
                {
                    "Walk up to an empty easel and press I to turn an image into a 3D exhibit",
                    "Copied an image? Press Ctrl+V at an easel to paste it straight in",
                    "Press I at a finished exhibit to spin it, take it apart and explore its parts",
                },
            });

            Rect? farm = AreaOf(8f, "FarmCrops", "FarmHouse", FarmDressing.RootName);
            if (farm.HasValue)
                _places.Add(new Place
                {
                    Name = "Farm", Icon = "barn", Contains = p => farm.Value.Contains(new Vector2(p.x, p.z)),
                    Tips = new[]
                    {
                        "Farmer Joe, Rosa and Gardener Sam have farm jobs: look for the yellow !",
                        "Walk up to someone and press E to talk. Jobs pay coins and a star",
                        "Stuck on a job? Talk to whoever gave it to you for help (after a practice question)",
                    },
                });

            if (_track != null)
                _places.Add(new Place
                {
                    Name = "Racetrack", Icon = "flag", Contains = NearTrack,
                    Tips = new[]
                    {
                        "Walk up to a race car and press X to get in",
                        "Getting in starts a Knowledge Time Trial: 3 or 5 laps, with practice questions each lap",
                        "Right answers give you a speed boost; wrong ones cost 3 seconds",
                    },
                });

            Rect? suburbs = AreaOf(12f, "SuburbHouses");
            if (suburbs.HasValue)
                _places.Add(new Place
                {
                    Name = "Suburbs", Icon = "house", Contains = p => suburbs.Value.Contains(new Vector2(p.x, p.z)),
                    Tips = new[]
                    {
                        "Paperboy Pete needs help with his newspaper round by bike",
                        "Mrs. Green's lawn needs mowing: find her by the yellow !",
                        "Press E to talk to anyone with a yellow ! over their head",
                    },
                });

            _places.Add(new Place
            {
                Name = "City", Icon = "city", Contains = CityArea.Contains,
                Tips = new[]
                {
                    "Residents with a yellow ! need help: walk up and press E",
                    "People with a blue ? give hints if you answer a practice question",
                    "Press B to open the clothes shop, or visit Stella's stall downtown",
                    "The pink park in the north-east is the art gallery",
                },
            });

            _places.Add(new Place
            {
                Name = "River", Icon = "map", Contains = p => Mathf.Abs(p.x - CityArea.RiverCenterX(p.z)) < 34f,
                Tips = new[] { "Four bridges cross the river: the city is on the east bank, the farm and suburbs on the west" },
            });
        }

        bool NearTrack(Vector3 p)
        {
            Vector3 q = _track.PointAt(_track.NearestIndex(p));
            return new Vector2(p.x - q.x, p.z - q.z).sqrMagnitude < 32f * 32f;
        }

        /// <summary>The ground rectangle covered by the named objects' renderers, grown by <paramref name="margin"/>.</summary>
        static Rect? AreaOf(float margin, params string[] names)
        {
            Bounds? all = null;
            foreach (string name in names)
            {
                GameObject go = GameObject.Find(name);
                if (go == null) continue;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    if (all == null) all = r.bounds;
                    else { Bounds b = all.Value; b.Encapsulate(r.bounds); all = b; }
                }
            }
            if (all == null) return null;
            Bounds a = all.Value;
            return Rect.MinMaxRect(a.min.x - margin, a.min.z - margin, a.max.x + margin, a.max.z + margin);
        }

        Place Where()
        {
            if (_inCar) return Driving;
            BikeController bike = FindAnyObjectByType<BikeController>();
            if (bike != null && bike.IsRidden) return Biking;
            if (_map == null) _map = FindAnyObjectByType<MapSystem>();
            Transform focus = _map != null && _map.Focus != null ? _map.Focus : _player;
            if (focus == null) return Anywhere;
            foreach (Place p in _places)
                if (p.Contains(focus.position)) return p;
            return Anywhere;
        }

        // ---- Showing ----

        void Update()
        {
            if (_group == null) return;
            bool hidden = LoadingScreen.IsLoading || PauseMenu.IsOpen || ModalGuard.IsOpen || QuestionPrompt.IsOpen || PauseMenu.OthersOpen();
            _group.alpha = Mathf.MoveTowards(_group.alpha, hidden ? 0 : 1, Time.unscaledDeltaTime / FadeSeconds);
            _group.blocksRaycasts = !hidden;
            if (hidden) return;
            if (!_placesReady) ResolvePlaces();

            string status = ImportButtonOverlay.StatusMessage;
            if (status != null)
                Want("Importing", status, "import");
            else
            {
                if (Time.unscaledTime >= _nextCheckAt)
                {
                    _nextCheckAt = Time.unscaledTime + CheckSeconds;
                    Place p = Where();
                    if (p != _place)
                    {
                        _place = p;
                        _tipIndex = 0;
                        _nextTipAt = Time.unscaledTime + TipSeconds;
                    }
                    _tips = TipsFor(_place);
                }
                if (Time.unscaledTime >= _nextTipAt) NextTip();
                Want(_place.Name, _tips[_tipIndex % _tips.Length], _place.Icon);
            }
            Crossfade();
        }

        string[] TipsFor(Place p)
        {
            if (p == Driving || p == Biking || QuestionPool.HasQuestions) return p.Tips;
            return new List<string>(p.Tips) { NoLecturesTip }.ToArray();
        }

        void NextTip()
        {
            _tipIndex++;
            _nextTipAt = Time.unscaledTime + TipSeconds;
        }

        void Want(string place, string tip, string icon)
        {
            _wantPlace = place;
            _wantTip = tip;
            _wantIcon = icon;
        }

        /// <summary>Fades the old tip out and the new one in.</summary>
        void Crossfade()
        {
            bool same = _tipText.text == _wantTip && _placeText.text == _wantPlace;
            _content.alpha = Mathf.MoveTowards(_content.alpha, same ? 1 : 0, Time.unscaledDeltaTime / FadeSeconds);
            if (same || _content.alpha > 0) return;
            _placeText.text = _wantPlace;
            _tipText.text = _wantTip;
            _icon.sprite = ThemeArt.Icon(_wantIcon);
        }
    }
}
