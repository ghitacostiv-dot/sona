using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace SONA.Services
{
    public class YTResult
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Uploader { get; set; } = "";
        public string DurationStr { get; set; } = "";
        public string WebpageUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
    }

    public static class MusicEngine
    {
        private static readonly HttpClient _http = new();

        /// <summary>
        /// Searches YouTube for the best audio match and extracts the direct streaming URL using yt-dlp.
        /// </summary>
        public static async Task<string> GetYoutubeAudioUrlAsync(string songName, string artist)
        {
            try
            {
                string query = $"ytsearch1:\"{songName} {artist} official audio\"";
                string args = $"-f bestaudio -g {query}";

                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                var outputTask = proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                string output = await outputTask;
                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim();
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task<List<YTResult>> SearchYouTubeAsync(string query, int count = 50, int startIndex = 1)
        {
            var results = new List<YTResult>();
            try
            {
                int endIndex = startIndex + count - 1;
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"ytsearch{endIndex}:\"{query}\" --playlist-start {startIndex} --dump-json --no-warnings --flat-playlist",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return results;

                using var reader = proc.StandardOutput;
                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var json = JObject.Parse(line);
                        string id = json["id"]?.ToString() ?? "";
                        string title = json["title"]?.ToString() ?? "";
                        string uploader = json["uploader"]?.ToString() ?? "";
                        string duration = json["duration"]?.ToString() ?? "";
                        // Pick highest resolution thumbnail from the thumbnails array
                        string thumb = "";
                        var thumbs = json["thumbnails"] as JArray;
                        if (thumbs != null && thumbs.Count > 0)
                            thumb = thumbs.Last?["url"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(thumb))
                            thumb = json["thumbnail"]?.ToString() ?? "";
                        string url = json["webpage_url"]?.ToString() ?? json["url"]?.ToString() ?? "";

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                        {
                            if (double.TryParse(duration, out double secs))
                            {
                                duration = TimeSpan.FromSeconds(secs).ToString(@"m\:ss");
                            }

                            results.Add(new YTResult
                            {
                                Id = id,
                                Title = title,
                                Uploader = uploader,
                                DurationStr = duration,
                                WebpageUrl = url,
                                ThumbnailUrl = thumb
                            });
                        }
                    }
                    catch { }
                }

                await proc.WaitForExitAsync();
            }
            catch { }
            return results;
        }

        public static async Task<string> GetDirectAudioUrlAsync(string webpageUrl)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f bestaudio -g \"{webpageUrl}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                return proc.ExitCode == 0 ? output.Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Get direct video+audio URL for video playback mode (like YT Music video toggle).
        /// </summary>
        public static async Task<string> GetDirectVideoUrlAsync(string webpageUrl)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f \"bestvideo[height<=720]+bestaudio/best[height<=720]/best\" -g \"{webpageUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return string.Empty;

                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // yt-dlp may output two URLs (video + audio). Take the first.
                    var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return lines[0].Trim();
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Fetch lyrics from lrclib.net API (free, no key required).
        /// Returns (plainLyrics, syncedLyrics). Either may be empty.
        /// </summary>
        public static async Task<(string Plain, string Synced)> GetLyricsAsync(string title, string artist)
        {
            try
            {
                var url = $"https://lrclib.net/api/get?artist_name={Uri.EscapeDataString(artist)}&track_name={Uri.EscapeDataString(title)}";
                _http.DefaultRequestHeaders.UserAgent.Clear();
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("SONA/1.0");
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var plain = json["plainLyrics"]?.ToString() ?? "";
                var synced = json["syncedLyrics"]?.ToString() ?? "";
                return (plain, synced);
            }
            catch
            {
                return ("", "");
            }
        }

        /// <summary>
        /// Download audio to a local file using yt-dlp in the specified format (mp3, m4a, flac, wav).
        /// </summary>
        public static async Task<bool> DownloadAudioAsync(string webpageUrl, string outputDir, string filename, string format = "mp3")
        {
            try
            {
                var safeName = string.Join("_", filename.Split(System.IO.Path.GetInvalidFileNameChars()));
                var outputPath = System.IO.Path.Combine(outputDir, safeName + ".%(ext)s");
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f bestaudio -x --audio-format {format} -o \"{outputPath}\" \"{webpageUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Download an entire playlist (or video containing a playlist) to a local directory in the specified format.
        /// </summary>
        public static async Task<bool> DownloadPlaylistAsync(string webpageUrl, string outputDir, string format = "mp3")
        {
            try
            {
                var outputPath = System.IO.Path.Combine(outputDir, "%(playlist_index)s - %(title)s.%(ext)s");
                var psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"-f bestaudio -x --audio-format {format} -o \"{outputPath}\" --yes-playlist \"{webpageUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
