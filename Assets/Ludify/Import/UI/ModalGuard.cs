using System.Collections.Generic;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// While an import panel is open (e.g. someone is typing/pasting text), pause the game and switch
    /// off the player, camera and car controls so keys like W, E, X or M don't also move or trigger
    /// things. Gameplay scripts are found by type name, because this assembly can't reference them.
    /// Other systems already stay quiet while <see cref="LessonFilePicker.IsOpen"/> is true, which
    /// includes this guard.
    /// </summary>
    public static class ModalGuard
    {
        static readonly string[] ControlTypes = { "PlayerController", "OrbitCamera", "VehicleInteraction", "CarController" };

        static int _depth;
        static float _timeScale = 1f;
        static readonly List<Behaviour> Paused = new List<Behaviour>();

        public static bool IsOpen => _depth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _depth = 0;
            Paused.Clear();
        }

        public static void Push()
        {
            if (_depth++ > 0) return;
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>())
                if (b.enabled && System.Array.IndexOf(ControlTypes, b.GetType().Name) >= 0)
                {
                    b.enabled = false;
                    Paused.Add(b);
                }
        }

        public static void Pop()
        {
            if (_depth == 0 || --_depth > 0) return;
            foreach (Behaviour b in Paused)
                if (b != null) b.enabled = true;
            Paused.Clear();
            Time.timeScale = _timeScale;
        }
    }
}
