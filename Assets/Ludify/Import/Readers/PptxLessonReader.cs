using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Ludify.Import
{
    /// <summary>
    /// Extracts slide text and speaker notes from a PowerPoint file, in presentation order.
    /// Speaker notes matter: professors often put most of the lecture content there.
    /// </summary>
    public sealed class PptxLessonReader : ILessonReader
    {
        const string PresentationPart = "ppt/presentation.xml";

        public string[] Extensions => new[] { ".pptx" };

        public LessonContent Read(string path)
        {
            var sb = new StringBuilder();
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(path))
                {
                    XDocument presentation = OpenXml.LoadPart(zip, PresentationPart)
                        ?? throw new InvalidDataException("missing " + PresentationPart);
                    var presentationRels = OpenXml.LoadRelationships(zip, PresentationPart);

                    int slideNumber = 0;
                    foreach (XElement sldId in presentation.Descendants(OpenXml.Presentation + "sldId"))
                    {
                        string relId = (string)sldId.Attribute(OpenXml.OfficeRels + "id");
                        if (relId == null || !presentationRels.TryGetValue(relId, out var slide)) continue;
                        slideNumber++;

                        sb.Append("--- Slide ").Append(slideNumber).AppendLine(" ---");
                        foreach (string line in OpenXml.DrawingParagraphs(OpenXml.LoadPart(zip, slide.Path)))
                            sb.AppendLine(line);

                        var notesRel = OpenXml.LoadRelationships(zip, slide.Path).Values
                            .FirstOrDefault(r => r.Type.EndsWith("/notesSlide"));
                        if (notesRel.Path != null)
                        {
                            // Notes pages also contain the slide-number placeholder; skip bare numbers.
                            var notes = OpenXml.DrawingParagraphs(OpenXml.LoadPart(zip, notesRel.Path))
                                .Where(l => !l.All(char.IsDigit)).ToList();
                            if (notes.Count > 0)
                            {
                                sb.AppendLine("Speaker notes:");
                                foreach (string line in notes) sb.AppendLine(line);
                            }
                        }
                        sb.AppendLine();
                    }
                }
            }
            catch (System.Exception e) when (e is InvalidDataException || e is System.Xml.XmlException)
            {
                throw new ImportException($"\"{Path.GetFileName(path)}\" isn't a readable PowerPoint (.pptx) file.", e);
            }

            return new LessonContent { FileName = Path.GetFileName(path), Text = sb.ToString() };
        }
    }
}
