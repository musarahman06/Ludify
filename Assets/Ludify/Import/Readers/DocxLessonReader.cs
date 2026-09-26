using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Ludify.Import
{
    /// <summary>Extracts paragraph text (including tables) from a Word document.</summary>
    public sealed class DocxLessonReader : ILessonReader
    {
        const string DocumentPart = "word/document.xml";

        public string[] Extensions => new[] { ".docx" };

        public LessonContent Read(string path)
        {
            var sb = new StringBuilder();
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    XDocument doc = OpenXml.LoadPart(zip, DocumentPart)
                        ?? throw new InvalidDataException("missing " + DocumentPart);
                    XNamespace w = OpenXml.Word;

                    foreach (XElement p in doc.Descendants(w + "p"))
                    {
                        var line = new StringBuilder();
                        foreach (XElement e in p.Descendants())
                        {
                            if (e.Name == w + "t") line.Append(e.Value);
                            else if (e.Name == w + "tab") line.Append('\t');
                            else if (e.Name == w + "br" || e.Name == w + "cr") line.Append('\n');
                        }
                        string text = line.ToString().Trim();
                        if (text.Length > 0) sb.AppendLine(text);
                    }
                }
            }
            catch (System.Exception e) when (e is InvalidDataException || e is System.Xml.XmlException)
            {
                throw new ImportException($"\"{Path.GetFileName(path)}\" isn't a readable Word (.docx) file.", e);
            }

            return new LessonContent { FileName = Path.GetFileName(path), Text = sb.ToString() };
        }
    }
}
