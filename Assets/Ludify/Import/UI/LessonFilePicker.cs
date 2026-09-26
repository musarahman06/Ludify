using System;
using System.Linq;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Ludify.Import
{
    /// <summary>Opens the runtime file browser (works in Mac/Windows builds) filtered to lecture files.</summary>
    public static class LessonFilePicker
    {
        public static bool IsOpen => FileBrowser.IsOpen;

        public static void Show(Action<string> onPicked)
        {
            EnsureEventSystem();
            string[] extensions = LessonReaderFactory.SupportedExtensions.Select(e => "." + e).ToArray();
            FileBrowser.SetFilters(false, new FileBrowser.Filter("Lecture files", extensions));
            FileBrowser.ShowLoadDialog(paths => onPicked(paths[0]), null, FileBrowser.PickMode.Files,
                                       false, null, null, "Choose a lecture file", "Import");
        }

        // The file browser is uGUI and needs an EventSystem (this project uses the Input System).
        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
