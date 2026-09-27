using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ludify.Import
{
    public static class LessonReaderFactory
    {
        static readonly ILessonReader[] Readers =
        {
            new PdfLessonReader(),
            new PptxLessonReader(),
            new DocxLessonReader(),
            new TextLessonReader(),
            new ImageLessonReader(),
        };

        /// <summary>Extensions without the dot, for file-picker filters, e.g. "pdf".</summary>
        public static IEnumerable<string> SupportedExtensions =>
            Readers.SelectMany(r => r.Extensions).Select(e => e.TrimStart('.'));

        public static bool IsSupported(string path) => Find(path) != null;

        /// <summary>Reads a lesson file. Throws <see cref="ImportException"/> with a user-facing message on failure.</summary>
        public static LessonContent Read(string path)
        {
            if (!File.Exists(path))
                throw new ImportException($"File not found: {path}");

            ILessonReader reader = Find(path) ?? throw new ImportException(
                $"\"{Path.GetFileName(path)}\" isn't supported. Use: {string.Join(", ", SupportedExtensions)}. " +
                "(Older .ppt/.doc files: re-save as .pptx/.docx or export to PDF.)");

            LessonContent content;
            try { content = reader.Read(path); }
            catch (IOException e) { throw new ImportException($"Couldn't open \"{Path.GetFileName(path)}\". Is it open in another program?", e); }

            if (!content.IsAttachment && string.IsNullOrWhiteSpace(content.Text))
                throw new ImportException(
                    $"No text found in \"{content.FileName}\". If the slides are pictures of text, export them to PDF and import that instead.");
            return content;
        }

        static ILessonReader Find(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return Readers.FirstOrDefault(r => r.Extensions.Contains(ext));
        }
    }
}
