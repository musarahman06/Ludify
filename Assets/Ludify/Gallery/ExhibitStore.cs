using System;
using System.Collections.Generic;
using System.IO;
using Ludify.Import;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Ludify.Gallery
{
    /// <summary>One placed exhibit: which pedestal, the image it came from, and the 3D model to rebuild.</summary>
    [Serializable]
    public sealed class ExhibitRecord
    {
        /// <summary>Pedestal position (matched to the nearest pedestal on load, so layout tweaks don't lose exhibits).</summary>
        public float X, Z;
        /// <summary>File name of the copied image inside the gallery images folder.</summary>
        public string ImageFile;
        public SceneModel Model;
    }

    /// <summary>Saves the gallery to &lt;persistentDataPath&gt;/Gallery so exhibits survive restarts (no API calls on reload).</summary>
    public static class ExhibitStore
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        public static string Folder => Path.Combine(Application.persistentDataPath, "Gallery");
        static string IndexPath => Path.Combine(Folder, "exhibits.json");
        static string ImagesFolder => Path.Combine(Folder, "images");

        public static List<ExhibitRecord> Load()
        {
            try
            {
                if (File.Exists(IndexPath))
                    return JsonConvert.DeserializeObject<List<ExhibitRecord>>(File.ReadAllText(IndexPath), Settings) ?? new List<ExhibitRecord>();
            }
            catch (Exception e) { Debug.LogWarning("[Gallery] Couldn't read saved exhibits: " + e.Message); }
            return new List<ExhibitRecord>();
        }

        public static void Save(List<ExhibitRecord> records)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(IndexPath, JsonConvert.SerializeObject(records, Settings));
        }

        /// <summary>Copies the picked image into the gallery folder so the exhibit still works if the original moves.</summary>
        public static string CopyImage(string sourcePath)
        {
            Directory.CreateDirectory(ImagesFolder);
            string name = Guid.NewGuid().ToString("N").Substring(0, 12) + Path.GetExtension(sourcePath).ToLowerInvariant();
            File.Copy(sourcePath, Path.Combine(ImagesFolder, name), true);
            return name;
        }

        /// <summary>Full path of a stored image, or null if it's gone.</summary>
        public static string ImagePath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string path = Path.Combine(ImagesFolder, fileName);
            return File.Exists(path) ? path : null;
        }

        public static Texture2D LoadImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            string path = Path.Combine(ImagesFolder, fileName);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
        }

        public static void DeleteImage(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            string path = Path.Combine(ImagesFolder, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
