using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Ludify.Import
{
    public sealed class GeminiRequest
    {
        public string SystemInstruction;
        public string Text;

        /// <summary>Optional PDF sent alongside the text (Gemini reads it natively).</summary>
        public byte[] PdfBytes;

        /// <summary>Let the model search Google (grounding). Returns source links.</summary>
        public bool UseGoogleSearch;

        /// <summary>If set, the model must answer with JSON matching this schema (OpenAPI subset).</summary>
        public JObject ResponseSchema;

        public float? Temperature;
    }

    public sealed class GeminiResult
    {
        public string Text;
        public string FinishReason;
        public List<SourceLink> Sources = new List<SourceLink>();
    }

    /// <summary>Failure talking to Gemini. Message is user-facing.</summary>
    public sealed class GeminiException : ImportException
    {
        public readonly long StatusCode;
        public GeminiException(string message, long statusCode = 0) : base(message) { StatusCode = statusCode; }
    }

    /// <summary>
    /// Minimal Gemini REST client (generateContent) built on UnityWebRequest, so it works in the
    /// Editor and in Mac/Windows builds. Must be called from the main thread.
    /// </summary>
    public sealed class GeminiClient
    {
        const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";
        const int MaxAttempts = 3;
        const int TimeoutSeconds = 180;

        readonly string _apiKey;
        public string Model { get; }

        public GeminiClient(string apiKey, string model)
        {
            _apiKey = apiKey;
            Model = model;
        }

        public async Task<GeminiResult> GenerateAsync(GeminiRequest request, CancellationToken ct = default)
        {
            JObject body = BuildBody(request);
            string url = $"{BaseUrl}models/{Uri.EscapeDataString(Model)}:generateContent";
            // A 429 on a grounded request usually means the key has no search quota at all, so don't wait and retry.
            JObject response = await SendWithRetryAsync("POST", url, body.ToString(Newtonsoft.Json.Formatting.None), ct,
                                                        retryRateLimits: !request.UseGoogleSearch);
            return ParseResult(response);
        }

        /// <summary>Model names this key can call with generateContent (for diagnostics).</summary>
        public async Task<List<string>> ListModelsAsync(CancellationToken ct = default)
        {
            JObject response = await SendWithRetryAsync("GET", BaseUrl + "models?pageSize=1000", null, ct);
            return (response["models"] as JArray ?? new JArray())
                .Where(m => (m["supportedGenerationMethods"] as JArray)?.Any(x => (string)x == "generateContent") == true)
                .Select(m => ((string)m["name"])?.Replace("models/", ""))
                .ToList();
        }

        static JObject BuildBody(GeminiRequest r)
        {
            var parts = new JArray();
            if (r.PdfBytes != null)
                parts.Add(new JObject { ["inline_data"] = new JObject { ["mime_type"] = "application/pdf", ["data"] = Convert.ToBase64String(r.PdfBytes) } });
            parts.Add(new JObject { ["text"] = r.Text });

            var body = new JObject
            {
                ["contents"] = new JArray { new JObject { ["role"] = "user", ["parts"] = parts } }
            };
            if (!string.IsNullOrEmpty(r.SystemInstruction))
                body["system_instruction"] = new JObject { ["parts"] = new JArray { new JObject { ["text"] = r.SystemInstruction } } };
            if (r.UseGoogleSearch)
                body["tools"] = new JArray { new JObject { ["google_search"] = new JObject() } };

            var gen = new JObject();
            if (r.Temperature.HasValue) gen["temperature"] = r.Temperature.Value;
            if (r.ResponseSchema != null)
            {
                gen["responseMimeType"] = "application/json";
                gen["responseSchema"] = r.ResponseSchema;
            }
            if (gen.Count > 0) body["generationConfig"] = gen;
            return body;
        }

        static GeminiResult ParseResult(JObject response)
        {
            string blocked = (string)response.SelectToken("promptFeedback.blockReason");
            if (blocked != null)
                throw new GeminiException($"Gemini refused this file ({blocked}). Try a different file.");

            JToken candidate = response.SelectToken("candidates[0]");
            if (candidate == null)
                throw new GeminiException("Gemini returned no answer. Please try again.");

            var result = new GeminiResult
            {
                FinishReason = (string)candidate["finishReason"],
                Text = string.Concat((candidate.SelectToken("content.parts") as JArray ?? new JArray())
                    .Select(p => (string)p["text"] ?? "")),
            };

            if (result.FinishReason == "SAFETY" || result.FinishReason == "PROHIBITED_CONTENT")
                throw new GeminiException("Gemini stopped because of its safety filters. Try a different file.");

            var seen = new HashSet<string>();
            foreach (JToken chunk in candidate.SelectToken("groundingMetadata.groundingChunks") as JArray ?? new JArray())
            {
                string uri = (string)chunk.SelectToken("web.uri");
                if (string.IsNullOrEmpty(uri) || !seen.Add(uri)) continue;
                result.Sources.Add(new SourceLink { Url = uri, Title = (string)chunk.SelectToken("web.title") ?? uri });
            }
            return result;
        }

        async Task<JObject> SendWithRetryAsync(string method, string url, string json, CancellationToken ct,
                                               bool retryRateLimits = true)
        {
            for (int attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await SendOnceAsync(method, url, json, ct);
                }
                catch (GeminiException e) when (attempt < MaxAttempts && IsTransient(e) && (retryRateLimits || e.StatusCode != 429))
                {
                    int delayMs = 2000 * (1 << (attempt - 1)); // 2s, 4s
                    Debug.LogWarning($"[Ludify.Import] Gemini request failed ({e.StatusCode}), retrying in {delayMs / 1000}s: {e.Message}");
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        static bool IsTransient(GeminiException e) =>
            e.StatusCode == 0 || e.StatusCode == 500 || e.StatusCode == 503 ||
            (e.StatusCode == 429 && !e.Message.Contains("daily"));

        async Task<JObject> SendOnceAsync(string method, string url, string json, CancellationToken ct)
        {
            using (var req = new UnityWebRequest(url, method))
            {
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                // Header, not a URL query parameter, so the key never shows up in logs of URLs.
                req.SetRequestHeader("x-goog-api-key", _apiKey);
                req.timeout = TimeoutSeconds;

                var done = new TaskCompletionSource<bool>();
                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                op.completed += _ => done.TrySetResult(true);
                using (ct.Register(() => { req.Abort(); done.TrySetCanceled(); }))
                    await done.Task;

                string text = req.downloadHandler.text;
                if (req.result == UnityWebRequest.Result.ConnectionError)
                    throw new GeminiException("Couldn't reach Gemini. Check your internet connection.");

                JObject parsed = null;
                try { if (!string.IsNullOrEmpty(text)) parsed = JObject.Parse(text); }
                catch (Newtonsoft.Json.JsonException) { }

                if (req.result == UnityWebRequest.Result.Success && parsed != null)
                    return parsed;

                throw ToException(req.responseCode, parsed, text);
            }
        }

        GeminiException ToException(long code, JObject parsed, string raw)
        {
            string status = (string)parsed?.SelectToken("error.status") ?? "";
            string message = (string)parsed?.SelectToken("error.message") ?? raw ?? "";
            string reasons = parsed?.SelectToken("error.details")?.ToString() ?? "";

            if (reasons.Contains("API_KEY_INVALID") || code == 401)
                return new GeminiException("The Gemini API key was rejected. Check that it was copied correctly.", code);
            if (code == 403)
                return new GeminiException("This Gemini API key isn't allowed to use the API. Create a new key at aistudio.google.com.", code);
            if (code == 404)
                return new GeminiException($"Gemini model \"{Model}\" wasn't found. Set \"geminiModel\" in ludify_secrets.json " +
                                           "(Ludify > Import > Check Gemini Setup lists the available models).", code);
            if (code == 429)
            {
                bool daily = (message + reasons).IndexOf("PerDay", StringComparison.OrdinalIgnoreCase) >= 0;
                return new GeminiException(daily
                    ? "The free Gemini daily limit is used up. It resets at midnight Pacific time. Previously imported files still work."
                    : "Too many Gemini requests at once (free-tier limit). Wait a minute and try again.", code);
            }
            if (code == 400)
                return new GeminiException($"Gemini couldn't process this request: {message}", code);

            Debug.LogWarning($"[Ludify.Import] Gemini HTTP {code} {status}: {message}");
            return new GeminiException($"Gemini had a problem (HTTP {code}). Please try again.", code);
        }
    }
}
