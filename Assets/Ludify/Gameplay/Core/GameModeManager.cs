using UnityEngine;
using UnityEngine.InputSystem;

namespace Ludify.Gameplay.Core
{
    /// <summary>
    /// Owns the current <see cref="GameMode"/> and the <see cref="InputRouter"/>. Every mode switch goes
    /// through <see cref="SetMode"/>, which swaps the active input map, updates the cursor and raises
    /// <see cref="GameEvents.GameModeChanged"/>.
    /// </summary>
    public sealed class GameModeManager : Singleton<GameModeManager>
    {
        [SerializeField] InputActionAsset controls;
        [SerializeField] GameMode startMode = GameMode.Exploring;

        public InputRouter Input { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.Loading;

        /// <summary>Mode to return to when a menu or inspection closes.</summary>
        public GameMode PreviousMode { get; private set; } = GameMode.Exploring;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            if (controls == null)
            {
                Debug.LogError("GameModeManager has no controls asset assigned.", this);
                return;
            }
            Input = new InputRouter(controls);
        }

        void Start() => SetMode(startMode);

        public void SetMode(GameMode mode)
        {
            if (Input == null) return;
            var previous = Mode;
            if (previous == mode) return;
            if (mode == GameMode.InMenu || mode == GameMode.Inspecting) PreviousMode = previous;
            Mode = mode;
            Input.Activate(mode);

            // The third-person cameras orbit on right-drag, so the cursor stays free in every mode.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameEvents.RaiseGameModeChanged(previous, mode);
        }

        /// <summary>Leave a menu/inspection and return to whatever mode was active before it.</summary>
        public void ReturnFromOverlay()
        {
            var target = PreviousMode == GameMode.InMenu || PreviousMode == GameMode.Inspecting ? GameMode.Exploring : PreviousMode;
            SetMode(target);
        }

        protected override void OnDestroy()
        {
            Input?.DisableAll();
            base.OnDestroy();
        }
    }
}
