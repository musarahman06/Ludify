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
        /// <summary>True while the file browser or any import panel is open (gameplay input should wait).</summary>
        public static bool IsOpen => FileBrowser.IsOpen || ModalGuard.IsOpen;

        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        public static void Show(Action<string> onPicked) =>
            Show(onPicked, "Lecture files", LessonReaderFactory.SupportedExtensions.Select(e => "." + e).ToArray(),
                 "Choose a lecture file", "Import");

        public static void ShowImages(Action<string> onPicked) =>
            Show(onPicked, "Images", ImageExtensions, "Choose an image", "Use image");

        public static void Show(Action<string> onPicked, string filterName, string[] extensions, string title, string button)
        {
            EnsureEventSystem();
            FileBrowser.SetFilters(false, new FileBrowser.Filter(filterName, extensions));
            FileBrowser.ShowLoadDialog(paths => onPicked(paths[0]), null, FileBrowser.PickMode.Files,
                                       false, null, null, title, button);
        }

        /// <summary>uGUI needs an EventSystem for clicks (this project uses the Input System).</summary>
        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
