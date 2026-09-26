using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Ludify.Import
{
    /// <summary>Minimal helpers for reading Office Open XML packages (.pptx/.docx are zip files of XML parts).</summary>
    internal static class OpenXml
    {
        public static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
        public static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
        public static readonly XNamespace Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        public static readonly XNamespace OfficeRels = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        public static readonly XNamespace PackageRels = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static XDocument LoadPart(ZipArchive zip, string partPath)
        {
            ZipArchiveEntry entry = zip.GetEntry(partPath);
            if (entry == null) return null;
            using (Stream s = entry.Open())
                return XDocument.Load(s);
        }

        /// <summary>
        /// Reads the .rels file for a part, returning relationship Id → (absolute part path, type).
        /// </summary>
        public static Dictionary<string, (string Path, string Type)> LoadRelationships(ZipArchive zip, string partPath)
        {
            string dir = GetDirectory(partPath);
            string relsPath = (dir.Length > 0 ? dir + "/" : "") + "_rels/" + Path.GetFileName(partPath) + ".rels";
            var result = new Dictionary<string, (string, string)>();
            XDocument rels = LoadPart(zip, relsPath);
            if (rels == null) return result;

            foreach (XElement rel in rels.Root.Elements(PackageRels + "Relationship"))
            {
                if ((string)rel.Attribute("TargetMode") == "External") continue;
                string id = (string)rel.Attribute("Id");
                string target = (string)rel.Attribute("Target");
                if (id == null || target == null) continue;
                result[id] = (ResolvePath(dir, target), (string)rel.Attribute("Type") ?? "");
            }
            return result;
        }

        /// <summary>Joins a relationship target onto a base folder, resolving "..".</summary>
        public static string ResolvePath(string baseDir, string target)
        {
            if (target.StartsWith("/")) return target.TrimStart('/');
            var parts = new List<string>(baseDir.Length > 0 ? baseDir.Split('/') : new string[0]);
            foreach (string segment in target.Split('/'))
            {
                if (segment == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else if (segment != "." && segment.Length > 0) parts.Add(segment);
            }
            return string.Join("/", parts);
        }

        static string GetDirectory(string partPath)
        {
            int slash = partPath.LastIndexOf('/');
            return slash < 0 ? "" : partPath.Substring(0, slash);
        }

        /// <summary>Text of every DrawingML paragraph (a:p) in a slide/notes part, one string per paragraph.</summary>
        public static IEnumerable<string> DrawingParagraphs(XDocument doc)
        {
            if (doc == null) yield break;
            foreach (XElement p in doc.Descendants(Drawing + "p"))
            {
                string text = string.Concat(p.Descendants(Drawing + "t").Select(t => t.Value)).Trim();
                if (text.Length > 0) yield return text;
            }
        }
    }
}
