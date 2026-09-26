using System.IO;

namespace Ludify.Import
{
    public sealed class TextLessonReader : ILessonReader
    {
        public string[] Extensions => new[] { ".txt", ".md" };

        public LessonContent Read(string path)
        {
            // ReadAllText detects UTF-8/UTF-16 BOMs and defaults to UTF-8.
            return new LessonContent { FileName = Path.GetFileName(path), Text = File.ReadAllText(path) };
        }
    }
}
