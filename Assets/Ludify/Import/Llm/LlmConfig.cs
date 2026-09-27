using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ludify.Import
{
    /// <summary>
    /// LLM settings. The API key is never stored in the project. It is looked up, in order, from:
    ///   1. the GEMINI_API_KEY environment variable
    ///   2. &lt;project root&gt;/ludify_secrets.json (gitignored, outside Assets/ so it never ships in a build)
    ///   3. &lt;Application.persistentDataPath&gt;/ludify_secrets.json (for built games)
    /// ludify_secrets.json looks like: { "geminiApiKey": "...", "geminiModel": "optional-model-name" }
    /// </summary>
    public sealed class LlmConfig
    {
        public const string EnvVar = "GEMINI_API_KEY";
        public const string SecretsFileName = "ludify_secrets.json";
        /// <summary>PlayerPrefs key for the "Questions per lecture" setting.</summary>
        public const string QuestionsPerFilePref = "Ludify.QuestionsPerFile";

        /// <summary>
        /// Google's alias for the current Flash-Lite model: the largest free daily quota.
        /// Override with "geminiModel" in ludify_secrets.json or the GEMINI_MODEL env var.
        /// </summary>
        public const string DefaultModel = "gemini-flash-lite-latest";

        public string ApiKey;
        public string Model = DefaultModel;
        public int QuestionsPerFile = 20;

        /// <summary>Use Google Search grounding in the research step.</summary>
        public bool UseWebResearch = true;

        /// <summary>Where the key came from, for diagnostics. Never the key itself.</summary>
        public string KeySource = "not found";

        public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);

        public static string SetupHelp =>
            "No Gemini API key found. Get a free key at https://aistudio.google.com (\"Get API key\"), then either:\n" +
            $"  - set the {EnvVar} environment variable and restart Unity, or\n" +
            $"  - copy ludify_secrets.example.json to {SecretsFileName} in the project folder and paste your key into it.";

        /// <summary>Call on the main thread (uses Application paths).</summary>
        public static LlmConfig Load()
        {
            var config = new LlmConfig();
            try { config.QuestionsPerFile = Mathf.Clamp(PlayerPrefs.GetInt(QuestionsPerFilePref, config.QuestionsPerFile), 5, 40); } catch { }

            string envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
            if (!string.IsNullOrWhiteSpace(envModel)) config.Model = envModel.Trim();

            string envKey = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                config.ApiKey = envKey.Trim();
                config.KeySource = $"{EnvVar} environment variable";
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (string dir in new[] { projectRoot, Application.persistentDataPath })
            {
                string path = Path.Combine(dir, SecretsFileName);
                if (!File.Exists(path)) continue;
                try
                {
                    JObject secrets = JObject.Parse(File.ReadAllText(path));
                    string key = (string)secrets["geminiApiKey"];
                    string model = (string)secrets["geminiModel"];
                    if (!config.HasKey && !string.IsNullOrWhiteSpace(key))
                    {
                        config.ApiKey = key.Trim();
                        config.KeySource = path;
                    }
                    if (string.IsNullOrWhiteSpace(envModel) && !string.IsNullOrWhiteSpace(model))
                        config.Model = model.Trim();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Ludify.Import] Couldn't read {path}: {e.Message}");
                }
            }
            return config;
        }
    }
}
