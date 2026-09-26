using UnityEngine.InputSystem;

namespace Ludify.Gameplay.Core
{
    /// <summary>
    /// Owns the LudifyControls action asset and keeps exactly one action map enabled per game mode,
    /// so movement keys never leak into menus and vice versa.
    /// </summary>
    public sealed class InputRouter
    {
        public InputActionMap OnFoot { get; }
        public InputActionMap Driving { get; }
        public InputActionMap Inspect { get; }
        public InputActionMap Menu { get; }

        // OnFoot
        public InputAction Move { get; }
        public InputAction Look { get; }
        public InputAction OrbitHold { get; }
        public InputAction Zoom { get; }
        public InputAction Jump { get; }
        public InputAction Sprint { get; }
        public InputAction Interact { get; }
        public InputAction Codex { get; }
        public InputAction Map { get; }
        public InputAction Pause { get; }

        // Driving
        public InputAction Throttle { get; }
        public InputAction Brake { get; }
        public InputAction Steer { get; }
        public InputAction Handbrake { get; }
        public InputAction ResetVehicle { get; }
        public InputAction ExitVehicle { get; }
        public InputAction DriveLook { get; }
        public InputAction DriveOrbitHold { get; }
        public InputAction DriveZoom { get; }

        // Inspect
        public InputAction InspectOrbit { get; }
        public InputAction InspectOrbitHold { get; }
        public InputAction InspectZoom { get; }
        public InputAction InspectSelect { get; }
        public InputAction InspectExit { get; }

        // Menu
        public InputAction MenuClose { get; }
        public InputAction MenuCodex { get; }
        public InputAction MenuMap { get; }

        readonly InputActionAsset asset;

        public InputRouter(InputActionAsset asset)
        {
            this.asset = asset;
            OnFoot = asset.FindActionMap("OnFoot", true);
            Driving = asset.FindActionMap("Driving", true);
            Inspect = asset.FindActionMap("Inspect", true);
            Menu = asset.FindActionMap("Menu", true);

            Move = OnFoot.FindAction("Move", true);
            Look = OnFoot.FindAction("Look", true);
            OrbitHold = OnFoot.FindAction("OrbitHold", true);
            Zoom = OnFoot.FindAction("Zoom", true);
            Jump = OnFoot.FindAction("Jump", true);
            Sprint = OnFoot.FindAction("Sprint", true);
            Interact = OnFoot.FindAction("Interact", true);
            Codex = OnFoot.FindAction("Codex", true);
            Map = OnFoot.FindAction("Map", true);
            Pause = OnFoot.FindAction("Pause", true);

            Throttle = Driving.FindAction("Throttle", true);
            Brake = Driving.FindAction("Brake", true);
            Steer = Driving.FindAction("Steer", true);
            Handbrake = Driving.FindAction("Handbrake", true);
            ResetVehicle = Driving.FindAction("Reset", true);
            ExitVehicle = Driving.FindAction("Exit", true);
            DriveLook = Driving.FindAction("Look", true);
            DriveOrbitHold = Driving.FindAction("OrbitHold", true);
            DriveZoom = Driving.FindAction("Zoom", true);

            InspectOrbit = Inspect.FindAction("Orbit", true);
            InspectOrbitHold = Inspect.FindAction("OrbitHold", true);
            InspectZoom = Inspect.FindAction("Zoom", true);
            InspectSelect = Inspect.FindAction("Select", true);
            InspectExit = Inspect.FindAction("Exit", true);

            MenuClose = Menu.FindAction("Close", true);
            MenuCodex = Menu.FindAction("Codex", true);
            MenuMap = Menu.FindAction("Map", true);
        }

        public void Activate(GameMode mode)
        {
            asset.Disable();
            switch (mode)
            {
                case GameMode.Exploring: OnFoot.Enable(); break;
                case GameMode.Driving:
                case GameMode.Racing: Driving.Enable(); break;
                case GameMode.Inspecting: Inspect.Enable(); break;
                case GameMode.InMenu: Menu.Enable(); break;
                case GameMode.Loading: break;
            }
        }

        public void DisableAll() => asset.Disable();
    }
}
