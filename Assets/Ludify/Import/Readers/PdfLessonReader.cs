using System.IO;

namespace Ludify.Import
{
    /// <summary>
    /// PDFs are sent to Gemini as-is: it reads text, diagrams and scanned pages itself,
    /// so no PDF parsing library is needed in Unity.
    /// </summary>
    public sealed class PdfLessonReader : ILessonReader
    {
        // Gemini's inline request limit is 20 MB total; base64 adds ~33%, so leave headroom.
        public const long MaxBytes = 14 * 1024 * 1024;

        public string[] Extensions => new[] { ".pdf" };

        public LessonContent Read(string path)
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
                throw new ImportException(
                    $"\"{info.Name}\" is {info.Length / (1024 * 1024)} MB. PDFs must be under {MaxBytes / (1024 * 1024)} MB. " +
                    "Try splitting it or exporting without high-resolution images.");

            return new LessonContent { FileName = info.Name, PdfBytes = File.ReadAllBytes(path) };
        }
    }
}
