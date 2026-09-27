using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Ludify.Import
{
    public sealed class PastedImage
    {
        public byte[] Bytes;
        public string MimeType;
        /// <summary>".png" or ".jpg".</summary>
        public string Extension;
        /// <summary>Where it came from, for titles/messages ("Pasted image", a file name or a link).</summary>
        public string SourceName;

        /// <summary>Writes the image to a temp file (for pipelines that take a path).</summary>
        public string SaveTemp()
        {
            string path = Path.Combine(Application.temporaryCachePath, "pasted_" + Guid.NewGuid().ToString("N").Substring(0, 8) + Extension);
            File.WriteAllBytes(path, Bytes);
            return path;
        }
    }

    /// <summary>
    /// Gets an image from the system clipboard, in this order:
    ///  1. a copied picture (Snipping Tool / screenshot, or "Copy image" in a browser),
    ///  2. a copied image file (Ctrl+C / Cmd+C on a file),
    ///  3. copied text that is an image link (https://…png/jpg) or a data:image URL.
    /// Unity can only read text from the clipboard, so pictures go through the OS: PowerShell on
    /// Windows, osascript on macOS. Call from the main thread.
    /// </summary>
    public static class ImagePaste
    {
        public const string HowTo =
            "Copy an image first: take a screenshot (Snipping Tool / Win+Shift+S, or Cmd+Ctrl+Shift+4 on Mac), " +
            "or right-click an image in your browser → Copy image. You can also copy an image link.";

        public static async Task<PastedImage> ReadAsync(CancellationToken ct = default)
        {
            string tempPng = Path.Combine(Application.temporaryCachePath, "clipboard_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
            string clipboardText = GUIUtility.systemCopyBuffer;
            RuntimePlatform platform = Application.platform;

            string result = await Task.Run(() => ReadPictureWithOs(platform, tempPng), ct);
            try
            {
                if (result == "OK" && File.Exists(tempPng))
                    return FromBytes(File.ReadAllBytes(tempPng), "Pasted image");
                if (result != null && result.StartsWith("FILE:"))
                {
                    string file = result.Substring(5).Trim();
                    if (File.Exists(file)) return FromBytes(File.ReadAllBytes(file), Path.GetFileNameWithoutExtension(file));
                }
            }
            finally
            {
                if (File.Exists(tempPng)) File.Delete(tempPng);
            }

            string text = clipboardText?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return FromDataUrl(text);
                if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return await DownloadAsync(text, ct);
            }
            throw new ImportException("There's no image on the clipboard. " + HowTo);
        }

        static PastedImage FromBytes(byte[] bytes, string source)
        {
            if (bytes.Length > 12 * 1024 * 1024) throw new ImportException("That image is too large (max 12 MB).");
            if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return new PastedImage { Bytes = bytes, MimeType = "image/png", Extension = ".png", SourceName = source };
            if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                return new PastedImage { Bytes = bytes, MimeType = "image/jpeg", Extension = ".jpg", SourceName = source };
            throw new ImportException("That isn't a PNG or JPG image. Try copying the image itself (right-click → Copy image).");
        }

        static PastedImage FromDataUrl(string url)
        {
            int comma = url.IndexOf(',');
            if (comma < 0 || !url.Substring(0, comma).Contains(";base64")) throw new ImportException("That image link can't be read.");
            try { return FromBytes(Convert.FromBase64String(url.Substring(comma + 1)), "Pasted image"); }
            catch (FormatException) { throw new ImportException("That image link can't be read."); }
        }

        static async Task<PastedImage> DownloadAsync(string url, CancellationToken ct)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 30;
                // Many image hosts reject requests without a browser-like User-Agent.
                req.SetRequestHeader("User-Agent", "Mozilla/5.0 (compatible; Ludify/1.0; educational game)");
                req.SetRequestHeader("Accept", "image/png,image/jpeg,image/*;q=0.8");
                var done = new TaskCompletionSource<bool>();
                req.SendWebRequest().completed += _ => done.TrySetResult(true);
                using (ct.Register(() => { req.Abort(); done.TrySetCanceled(); }))
                    await done.Task;
                if (req.result != UnityWebRequest.Result.Success)
                    throw new ImportException($"Couldn't download the image from that link ({req.error}).");
                string name = Path.GetFileNameWithoutExtension(new Uri(url).AbsolutePath);
                return FromBytes(req.downloadHandler.data, string.IsNullOrEmpty(name) ? "Image from link" : name);
            }
        }

        // ---- OS clipboard access (runs on a worker thread) ----

        /// <summary>Returns "OK" (PNG written to outPng), "FILE:&lt;path&gt;", "NONE", or null if unsupported.</summary>
        static string ReadPictureWithOs(RuntimePlatform platform, string outPng)
        {
            try
            {
                switch (platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        return Run("powershell.exe", "-NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -EncodedCommand " +
                                   Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsScript(outPng))));
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        string script = Path.ChangeExtension(outPng, ".applescript");
                        File.WriteAllText(script, MacScript(outPng), new UTF8Encoding(false));
                        try { return Run("/usr/bin/osascript", Quote(script)); }
                        finally { File.Delete(script); }
                    default:
                        return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ludify.Import] Couldn't read the clipboard picture: " + e.Message);
                return null;
            }
        }

        static string Run(string exe, string args)
        {
            var info = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            using (Process p = Process.Start(info))
            {
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return null; }
                return output.Trim();
            }
        }

        static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

        static string WindowsScript(string outPng) =>
            "Add-Type -AssemblyName System.Windows.Forms, System.Drawing\n" +
            "$img = [System.Windows.Forms.Clipboard]::GetImage()\n" +
            "if ($img -ne $null) {\n" +
            // Big screenshots (4K etc.) make huge PNGs; scale down to at most 2000 px, plenty for Gemini.
            "  $max = [Math]::Max($img.Width, $img.Height)\n" +
            "  if ($max -gt 2000) { $s = 2000.0 / $max; $img = New-Object System.Drawing.Bitmap($img, [int]($img.Width * $s), [int]($img.Height * $s)) }\n" +
            "  $img.Save('" + outPng.Replace("'", "''") + "', [System.Drawing.Imaging.ImageFormat]::Png); Write-Output 'OK'; exit\n" +
            "}\n" +
            "$files = [System.Windows.Forms.Clipboard]::GetFileDropList()\n" +
            "if ($files.Count -gt 0) { Write-Output ('FILE:' + $files[0]); exit }\n" +
            "Write-Output 'NONE'\n";

        static string MacScript(string outPng)
        {
            string path = outPng.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return
                "set outPath to \"" + path + "\"\n" +
                "try\n" +
                "  set d to (the clipboard as «class PNGf»)\n" +
                "  set f to open for access (POSIX file outPath) with write permission\n" +
                "  set eof f to 0\n  write d to f\n  close access f\n" +
                "  do shell script \"sips -Z 2000 \" & quoted form of outPath\n" +   // Retina screenshots are huge
                "  return \"OK\"\n" +
                "end try\n" +
                "try\n" +
                "  return \"FILE:\" & (POSIX path of (the clipboard as «class furl»))\n" +
                "end try\n" +
                "try\n" +
                "  set d to (the clipboard as «class TIFF»)\n" +
                "  set tiffPath to outPath & \".tiff\"\n" +
                "  set f to open for access (POSIX file tiffPath) with write permission\n" +
                "  set eof f to 0\n  write d to f\n  close access f\n" +
                "  do shell script \"sips -s format png -Z 2000 \" & quoted form of tiffPath & \" --out \" & quoted form of outPath & \" && rm \" & quoted form of tiffPath\n" +
                "  return \"OK\"\n" +
                "end try\n" +
                "return \"NONE\"\n";
        }
    }
}
