using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// Image → <see cref="SceneModel"/> with one Gemini vision call (structured JSON output).
    /// Results are cached by image contents in &lt;persistentDataPath&gt;/ImageModels, so re-using an
    /// image costs no API calls. Call from the main thread.
    /// </summary>
    public sealed class ImageModelGenerator
    {
        /// <summary>Bump when the prompt/schema changes so cached models regenerate.</summary>
        const string PipelineVersion = "scene-v4";
        const int MaxParts = 60, MaxLinks = 120;
        public const long MaxImageBytes = 12 * 1024 * 1024;

        const string SystemPrompt =
            "You turn educational images into 3D exhibits for a learning game, so students can walk around and explore them. " +
            "Be accurate. Only model what is actually in the image.";

        const string Instructions =
            "Describe this image as a 3D model built from parts and links.\n" +
            "Layout box is 10x10x10, seen by a viewer standing in front: x = left to right, y = up (0 = floor), " +
            "z = near to far (0 = closest to the viewer). Use the space; keep parts at least 1.5 apart.\n" +
            "Part kinds:\n" +
            "- Circuits: battery (value like \"9V\"), resistor (value like \"220Ω\"), bulb, switch, capacitor, led, " +
            "meter (value \"A\" or \"V\"), ground, node (a wire junction). Lay the circuit out flat like the diagram: " +
            "diagram left→right = x, diagram top→bottom = z from 9 down to 1, y = 0.5. Two-terminal parts run along their local x axis; " +
            "use rotationY = 90 for parts on vertical sides of the diagram. Connect every connection in the diagram with a \"wire\" link; " +
            "use node parts for junctions where 3+ wires meet.\n" +
            "- Molecules: atom (value = element symbol like \"O\"), with realistic 3D geometry and bond or double_bond links.\n" +
            "- Space (solar system, moons, orbits): star, planet (value \"ringed\" for Saturn-like rings), moon. Put the star in the middle " +
            "and planets at increasing distances along x (same y and z); size reflects relative size. Add an \"orbit\" link from each " +
            "planet to the star and from each moon to its planet; the game animates the orbits.\n" +
            "- Cells and organisms (animal/plant cell, bacterium): one shell part for the membrane or cell wall (size 7-9, centred), with " +
            "organelles inside it as sphere (nucleus, vacuole), cylinder (mitochondria, chloroplasts), box or panel parts, each labelled and coloured.\n" +
            "- Gears and mechanisms: gear (value = number of teeth, size ∝ teeth) arranged in the x/y plane facing the viewer, with a " +
            "\"mesh\" link between each pair of gears whose teeth touch; the game spins them at the correct ratios. Use arrow parts for forces.\n" +
            "- Charts and data (bar charts, graphs of amounts): bar parts (value = the number, label = the category) in a row along x " +
            "at y = 0, plus label parts for the axis titles.\n" +
            "- Geometry and math: one large solid (box, sphere, cylinder, cone, pyramid or prism; size 6-8, centred at x = 5, z = 5, " +
            "resting on y = 0 so its centre y = size / 2) with node parts placed exactly on its real corners, apex or base centre, " +
            "and \"dimension\" links between nodes whose label is the measurement (e.g. \"5 cm\").\n" +
            "- Other diagrams (water cycle, food chains, anatomy, processes, geography): use box, sphere, cylinder, cone, panel " +
            "(a flat sign; value = short text), arrow and label parts arranged in 3D, with arrow links for flows (animated) " +
            "and line links for relationships. Pick sensible colors (CSS names or #RRGGBB).\n" +
            "Give parts short labels taken from the image (e.g. \"R1\", \"Nucleus\"). Size is about 1 for a typical part (0.3–4).\n" +
            "For every part, info = one short sentence for students explaining what that part is or does in this image.\n" +
            "For switches, set value to \"open\" or \"closed\" as drawn.\n" +
            "If the image is mainly text, a photo of a real scene, or otherwise not a buildable diagram, set displayMode to \"image\" " +
            "and return no parts.\n" +
            "Also return: title (short), subject (e.g. Physics), and explanation (2-3 sentences teaching students what it shows).";

        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        readonly LlmConfig _config;

        public ImageModelGenerator(LlmConfig config = null) => _config = config ?? LlmConfig.Load();

        public static string CacheFolder => Path.Combine(Application.persistentDataPath, "ImageModels");

        public async Task<SceneModel> GenerateAsync(string imagePath, IProgress<string> progress = null,
                                                    bool forceRegenerate = false, CancellationToken ct = default)
        {
            if (!File.Exists(imagePath)) throw new ImportException($"Image not found: {imagePath}");
            string mime = MimeType(imagePath) ?? throw new ImportException("Use a PNG or JPG image.");
            string cacheFolder = CacheFolder;

            byte[] bytes = await Task.Run(() => File.ReadAllBytes(imagePath), ct);
            if (bytes.Length > MaxImageBytes) throw new ImportException("That image is too large (max 12 MB).");
            string cachePath = Path.Combine(cacheFolder, Hash(bytes) + ".json");

            if (!forceRegenerate && File.Exists(cachePath))
            {
                try
                {
                    progress?.Report("Loaded saved 3D model (no AI call needed).");
                    return JsonConvert.DeserializeObject<SceneModel>(File.ReadAllText(cachePath), JsonSettings);
                }
                catch (JsonException) { /* regenerate */ }
            }

            if (!_config.HasKey) throw new ImportException(LlmConfig.SetupHelp);
            progress?.Report("Gemini is studying the image…");
            var client = new GeminiClient(_config.ApiKey, _config.Model);
            GeminiResult raw = await client.GenerateAsync(new GeminiRequest
            {
                SystemInstruction = SystemPrompt,
                Text = Instructions,
                ImageBytes = bytes,
                ImageMimeType = mime,
                ResponseSchema = Schema,
                Temperature = 0.3f,
            }, ct);

            SceneModel model;
            try { model = JsonConvert.DeserializeObject<SceneModel>(raw.Text); }
            catch (JsonException e) { throw new ImportException("Gemini returned an unreadable model. Please try again.", e); }
            if (model == null) throw new ImportException("Gemini returned nothing for this image. Please try again.");

            Validate(model);
            Directory.CreateDirectory(cacheFolder);
            File.WriteAllText(cachePath, JsonConvert.SerializeObject(model, JsonSettings));
            progress?.Report(model.IsModel ? $"Built \"{model.Title}\" ({model.Parts.Count} parts)." : $"\"{model.Title}\" will be shown as a picture.");
            return model;
        }

        public static string MimeType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                default: return null;
            }
        }

        static void Validate(SceneModel m)
        {
            m.Title = string.IsNullOrWhiteSpace(m.Title) ? "Untitled" : m.Title.Trim();
            m.Parts = (m.Parts ?? new List<ScenePart>()).Where(p => p != null).Take(MaxParts).ToList();
            var kinds = new HashSet<string>(PartKinds.All);
            var ids = new HashSet<string>();
            for (int i = 0; i < m.Parts.Count; i++)
            {
                ScenePart p = m.Parts[i];
                p.Kind = (p.Kind ?? "box").Trim().ToLowerInvariant();
                if (!kinds.Contains(p.Kind)) p.Kind = "box";
                if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) { p.Id = "p" + i; ids.Add(p.Id); }
                p.X = Mathf.Clamp(p.X, 0, 10); p.Y = Mathf.Clamp(p.Y, 0, 10); p.Z = Mathf.Clamp(p.Z, 0, 10);
                p.Size = p.Size <= 0 ? 1 : Mathf.Clamp(p.Size, 0.2f, 5f);
            }
            var linkKinds = new HashSet<string>(PartKinds.LinkKinds);
            m.Links = (m.Links ?? new List<SceneLink>())
                .Where(l => l != null && l.From != null && l.To != null && l.From != l.To && ids.Contains(l.From) && ids.Contains(l.To))
                .Take(MaxLinks).ToList();
            foreach (SceneLink l in m.Links)
            {
                l.Kind = (l.Kind ?? "line").Trim().ToLowerInvariant();
                if (!linkKinds.Contains(l.Kind)) l.Kind = "line";
            }
            if (m.Parts.Count == 0) m.DisplayMode = "image";
        }

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(bytes.Concat(System.Text.Encoding.UTF8.GetBytes(PipelineVersion)).ToArray());
                return BitConverter.ToString(h, 0, 12).Replace("-", "").ToLowerInvariant();
            }
        }

        static readonly JObject Schema = BuildSchema();

        static JObject BuildSchema()
        {
            JArray Enum(IEnumerable<string> values) => new JArray(values);
            var num = new JObject { ["type"] = "NUMBER" };
            var str = new JObject { ["type"] = "STRING" };
            var part = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["id"] = str.DeepClone(), ["kind"] = new JObject { ["type"] = "STRING", ["enum"] = Enum(PartKinds.All) },
                    ["x"] = num.DeepClone(), ["y"] = num.DeepClone(), ["z"] = num.DeepClone(),
                    ["size"] = num.DeepClone(), ["rotationY"] = num.DeepClone(),
                    ["color"] = str.DeepClone(), ["label"] = str.DeepClone(), ["value"] = str.DeepClone(),
                    ["info"] = str.DeepClone(),
                },
                ["required"] = new JArray("id", "kind", "x", "y", "z", "info"),
                ["propertyOrdering"] = new JArray("id", "kind", "x", "y", "z", "size", "rotationY", "color", "label", "value", "info"),
            };
            var link = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["from"] = str.DeepClone(), ["to"] = str.DeepClone(),
                    ["kind"] = new JObject { ["type"] = "STRING", ["enum"] = Enum(PartKinds.LinkKinds) },
                    ["label"] = str.DeepClone(),
                },
                ["required"] = new JArray("from", "to", "kind"),
            };
            return new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["title"] = str.DeepClone(), ["subject"] = str.DeepClone(), ["explanation"] = str.DeepClone(),
                    ["displayMode"] = new JObject { ["type"] = "STRING", ["enum"] = new JArray("model", "image") },
                    ["parts"] = new JObject { ["type"] = "ARRAY", ["items"] = part },
                    ["links"] = new JObject { ["type"] = "ARRAY", ["items"] = link },
                },
                ["required"] = new JArray("title", "subject", "explanation", "displayMode", "parts", "links"),
                ["propertyOrdering"] = new JArray("title", "subject", "explanation", "displayMode", "parts", "links"),
            };
        }
    }
}
