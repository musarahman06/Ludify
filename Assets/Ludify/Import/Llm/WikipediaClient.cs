using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Ludify.Import
{
    /// <summary>
    /// Looks up a topic's summary on Wikipedia (REST API: free, no key) so exhibits can show real
    /// background facts from the web. Returns null if there's no article or no connection.
    /// </summary>
    public static class WikipediaClient
    {
        const string Endpoint = "https://en.wikipedia.org/api/rest_v1/page/summary/";

        public static async Task<WebReference> SummaryAsync(string topic, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(topic)) return null;
            string url = Endpoint + Uri.EscapeDataString(topic.Trim().Replace(' ', '_')) + "?redirect=true";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                // Wikimedia asks API clients to identify themselves.
                req.SetRequestHeader("User-Agent", "Ludify/1.0 (educational game; https://github.com/musarahman06/Ludify)");
                var done = new TaskCompletionSource<bool>();
                req.SendWebRequest().completed += _ => done.TrySetResult(true);
                using (ct.Register(() => { req.Abort(); done.TrySetCanceled(); }))
                    await done.Task;
                if (req.result != UnityWebRequest.Result.Success) return null;

                try
                {
                    JObject j = JObject.Parse(req.downloadHandler.text);
                    if ((string)j["type"] == "disambiguation") return null;
                    string extract = (string)j["extract"];
                    if (string.IsNullOrWhiteSpace(extract)) return null;
                    return new WebReference
                    {
                        Title = (string)j["title"],
                        Extract = Shorten(extract, 420),
                        Url = (string)j.SelectToken("content_urls.desktop.page"),
                    };
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ludify.Import] Couldn't read Wikipedia summary: " + e.Message);
                    return null;
                }
            }
        }

        /// <summary>Cuts at a sentence end near the limit.</summary>
        static string Shorten(string text, int max)
        {
            if (text.Length <= max) return text;
            int cut = text.LastIndexOf(". ", max, StringComparison.Ordinal);
            return cut > max / 2 ? text.Substring(0, cut + 1) : text.Substring(0, max) + "…";
        }
    }
}
