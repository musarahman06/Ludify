using System.Collections;
using System.Collections.Generic;
using Ludify.Import;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ludify.Map
{
    /// <summary>
    /// Minimap + full map + fast travel. Installs itself in any scene that has a
    /// <see cref="PlayerController"/>, so no scene edits are needed. Fast travel asks a practice
    /// question from the imported lectures first (free travel if none have been imported).
    /// </summary>
    public sealed class MapSystem : MonoBehaviour
    {
        const int CanvasOrder = 50;
        const float FadeSeconds = 0.5f;

        public MapSnapshot Snapshot { get; private set; }
        public List<FastTravelPoint> Points { get; private set; }
        public bool IsFullMapOpen => _fullMap != null && _fullMap.gameObject.activeSelf;

        /// <summary>What the map is centred on: the player, or the car they're driving.</summary>
        public Transform Focus => _camera != null && _camera.target != null ? _camera.target
                                : _player != null ? _player.transform : null;

        PlayerController _player;
        OrbitCamera _camera;
        FullMapView _fullMap;
        Image _fade;
        bool _travelling;
        float _timeScaleBeforeMap = 1f;
        readonly List<Behaviour> _pausedBehaviours = new List<Behaviour>();

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
            if (FindAnyObjectByType<MapSystem>() != null) return;
            if (FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) == null) return;
            new GameObject(nameof(MapSystem)).AddComponent<MapSystem>();
        }

        IEnumerator Start()
        {
            _player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            _camera = FindAnyObjectByType<OrbitCamera>();
            Vector3 spawn = _player.transform.position;
            Quaternion spawnRotation = _player.transform.rotation;

            // Let other runtime setup (colliders, cars) finish, and let the render pipeline draw a few
            // frames first. Offscreen renders requested on the very first frame come out empty.
            for (int i = 0; i < 3; i++) yield return null;

            Snapshot = new MapSnapshot(MapSnapshot.FindWorldBounds());
            yield return Snapshot.Capture();
            Points = FastTravelPoints.Resolve(spawn, spawnRotation, Snapshot.WorldRect);

            Canvas canvas = UiKit.CreateCanvas("MapCanvas", CanvasOrder);
            canvas.transform.SetParent(transform, false);
            MinimapView.Create(this, canvas.transform);
            _fullMap = FullMapView.Create(this, canvas.transform);

            _fade = UiKit.Image("Fade", canvas.transform, new Color(0, 0, 0, 0));
            UiKit.Stretch(_fade.rectTransform);

            // Safety net: capture again once everything has definitely rendered and settled.
            yield return new WaitForSecondsRealtime(1f);
            yield return Snapshot.Capture();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || Snapshot == null || QuestionPrompt.IsOpen || LessonFilePicker.IsOpen) return;

            if (kb.mKey.wasPressedThisFrame)
            {
                if (IsFullMapOpen) CloseFullMap(); else OpenFullMap();
            }
            else if (kb.escapeKey.wasPressedThisFrame && IsFullMapOpen)
            {
                CloseFullMap();
            }
        }

        public void OpenFullMap()
        {
            if (IsFullMapOpen || _travelling || RaceInProgress()) return;
            StartCoroutine(Snapshot.Capture()); // refresh so the map shows the world as it is right now

            _timeScaleBeforeMap = Time.timeScale;
            Time.timeScale = 0f;
            // Stop the player/camera/car controls reacting to clicks and keys while the map is open.
            Pause(_camera);
            Pause(_player);
            Pause(FindAnyObjectByType<VehicleInteraction>());

            _fullMap.gameObject.SetActive(true);
            _fullMap.SetStatus(FastTravel.BlockedReason(_player) ?? (QuestionPool.HasQuestions
                ? "Click a location to fast travel. Answer a practice question correctly to go."
                : "Click a location to fast travel. (Import a lecture to add practice questions.)"));
        }

        public void CloseFullMap()
        {
            if (!IsFullMapOpen) return;
            _fullMap.gameObject.SetActive(false);
            foreach (Behaviour b in _pausedBehaviours)
                if (b != null) b.enabled = true;
            _pausedBehaviours.Clear();
            Time.timeScale = _timeScaleBeforeMap;
        }

        /// <summary>The full map pauses the game, which would break a time trial's timing and slow motion.</summary>
        static bool RaceInProgress()
        {
            TimeTrialManager trial = FindAnyObjectByType<TimeTrialManager>();
            return trial != null && trial.CurrentState != TimeTrialManager.State.Idle
                                 && trial.CurrentState != TimeTrialManager.State.Finished;
        }

        void Pause(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;
            behaviour.enabled = false;
            _pausedBehaviours.Add(behaviour);
        }

        public async void RequestTravel(FastTravelPoint point)
        {
            if (_travelling) return;
            string blocked = FastTravel.BlockedReason(_player);
            if (blocked != null)
            {
                _fullMap.SetStatus(blocked);
                return;
            }

            _travelling = true;
            try
            {
                QuestionResult result = await QuestionPrompt.AskAsync($"Answer correctly to fast travel to {point.Name}", "Fast travel");
                if (this == null) return; // scene unloaded while the question was open
                if (result == QuestionResult.Cancelled)
                {
                    _fullMap.SetStatus("Fast travel cancelled.");
                    return;
                }

                if (!FastTravel.TryTravel(_player, point))
                {
                    _fullMap.SetStatus($"Couldn't find a safe place to land at {point.Name}.");
                    return;
                }
                // Look over the player's shoulder in the direction they arrive facing.
                if (_camera != null) _camera.yaw = point.ArrivalFacing.eulerAngles.y;
                CloseFullMap();
                StartCoroutine(FadeIn());
            }
            finally
            {
                _travelling = false;
            }
        }

        IEnumerator FadeIn()
        {
            for (float t = 0; t < FadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0, 0, 0, 1 - t / FadeSeconds);
                yield return null;
            }
            _fade.color = new Color(0, 0, 0, 0);
        }

        void OnDestroy()
        {
            if (IsFullMapOpen) Time.timeScale = _timeScaleBeforeMap;
            Snapshot?.Dispose();
        }
    }
}
