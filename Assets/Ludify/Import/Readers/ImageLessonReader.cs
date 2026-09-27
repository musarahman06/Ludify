using System.IO;

namespace Ludify.Import
{
    /// <summary>A screenshot or photo of lecture material (e.g. a slide from the Snipping Tool); Gemini reads it.</summary>
    public sealed class ImageLessonReader : ILessonReader
    {
        public const long MaxBytes = 12 * 1024 * 1024;

        public string[] Extensions => new[] { ".png", ".jpg", ".jpeg" };

        public LessonContent Read(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
                throw new ImportException($"\"{info.Name}\" is too large (max {MaxBytes / (1024 * 1024)} MB).");
            string ext = info.Extension.ToLowerInvariant();
            return new LessonContent
            {
                FileName = info.Name,
                ImageBytes = File.ReadAllBytes(path),
                ImageMimeType = ext == ".png" ? "image/png" : "image/jpeg",
            };
        }
    }
}
