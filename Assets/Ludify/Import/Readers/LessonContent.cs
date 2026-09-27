namespace Ludify.Import
{
    /// <summary>
    /// What a lesson file contains, normalized for the LLM: either extracted text,
    /// or raw PDF bytes (Gemini reads PDFs natively, including scanned pages).
    /// </summary>
    public sealed class LessonContent
    {
        public string FileName;
        public string Text;
        public byte[] PdfBytes;
        /// <summary>A picture of the material (slide screenshot, photo of notes…), read by Gemini's vision.</summary>
        public byte[] ImageBytes;
        public string ImageMimeType;

        /// <summary>Extra attachments (bundle generation: several PDFs/images at once).</summary>
        public System.Collections.Generic.List<(byte[] Data, string Mime)> Attachments;

        public bool IsPdf => PdfBytes != null;
        public bool IsImage => ImageBytes != null;
        /// <summary>True when Gemini reads an attachment instead of extracted text.</summary>
        public bool IsAttachment => IsPdf || IsImage || (Attachments != null && Attachments.Count > 0);
    }

    public interface ILessonReader
    {
        /// <summary>Lowercase extensions including the dot, e.g. ".pptx".</summary>
        string[] Extensions { get; }

        /// <summary>Pure .NET; safe to call off the main thread.</summary>
        LessonContent Read(string path);
    }
}
