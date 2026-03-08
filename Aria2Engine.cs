using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SONA.Services
{
    public class AriaDownloadInfo
    {
        public string Title { get; set; } = "";
        public string SourceName { get; set; } = "Repack";
        public string Gid { get; set; } = "";
        public string GameId { get; set; } = ""; // Associated Hydra/Steam ID
        public string Status { get; set; } = "Waiting...";
        public double ProgressPercent { get; set; } = 0;
        public string DownloadSpeedStr { get; set; } = "0 B/s";
        public string EtaStr { get; set; } = "--:--";
    }

    public static class Aria2Engine
    {
        public static event Action<AriaDownloadInfo>? OnDownloadUpdated;
        private static Process? _ariaProcess;
        private static readonly string _rpcSecret = "sona_aria_rpc_secret_8492"; // Hardcoded simple secret for local usage
        private static readonly int _rpcPort = 6800;
        private static readonly HttpClient _http = new();
        public static readonly List<AriaDownloadInfo> ActiveDownloads = new();

        public static bool EnsureAriaStarted()
        {
            if (_ariaProcess != null && !_ariaProcess.HasExited) return true;

            string ariaPath = "aria2c"; // Assume available in PATH or downloaded previously

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ariaPath,
                    Arguments = $"--enable-rpc=true --rpc-listen-port={_rpcPort} --rpc-secret={_rpcSecret} --dir=\"{AppConfig.GamesDir}\" --max-connection-per-server=16 --split=16 --min-split-size=1M",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _ariaProcess = Process.Start(psi);
                return _ariaProcess != null;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<string> AddDownloadAsync(string magnetUri, string title, string sourceName, string? objectId = null, bool isStreaming = false, string? fileIndex = null)
        {
            if (!EnsureAriaStarted()) return "";

            try
            {
                var options = new JObject
                {
                    ["dir"] = isStreaming ? Path.Combine(Path.GetTempPath(), "SONA_Stream") : AppConfig.GamesDir
                };

                if (isStreaming)
                {
                    options["sequential-download"] = "true";
                    options["bt-prioritize-first-last-piece"] = "true";
                    options["enable-http-pipelining"] = "true";
                    if (!string.IsNullOrEmpty(fileIndex))
                    {
                        options["select-file"] = fileIndex;
                    }
                }

                var req = new
                {
                    jsonrpc = "2.0",
                    id = "qwer",
                    method = "aria2.addUri",
                    @params = new object[]
                    {
                        $"token:{_rpcSecret}",
                        new[] { magnetUri },
                        options
                    }
                };

                var content = new StringContent(JsonConvert.SerializeObject(req), System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"http://localhost:{_rpcPort}/jsonrpc", content);
                var jsonStr = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonStr);

                string gid = json["result"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(gid))
                {
                    var info = new AriaDownloadInfo { Gid = gid, Title = title, SourceName = sourceName, GameId = objectId ?? "" };
                    ActiveDownloads.Add(info);
                    OnDownloadUpdated?.Invoke(info);
                    return gid;
                }
            }
            catch { }
            return "";
        }

        public static async Task<List<string>> GetFilesAsync(string gid)
        {
            try
            {
                var req = new
                {
                    jsonrpc = "2.0",
                    id = "files",
                    method = "aria2.getFiles",
                    @params = new object[] { $"token:{_rpcSecret}", gid }
                };

                var content = new StringContent(JsonConvert.SerializeObject(req), System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"http://localhost:{_rpcPort}/jsonrpc", content);
                var jsonStr = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonStr);

                var files = new List<string>();
                if (json["result"] is JArray arr)
                {
                    foreach (var f in arr)
                    {
                        string path = f["path"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(path)) files.Add(path);
                    }
                }
                return files;
            }
            catch { return new List<string>(); }
        }

        public static async Task<long> GetCompletedLengthAsync(string gid)
        {
            try
            {
                var req = new
                {
                    jsonrpc = "2.0",
                    id = "tellStatus",
                    method = "aria2.tellStatus",
                    @params = new object[] { $"token:{_rpcSecret}", gid, new[] { "completedLength" } }
                };

                var content = new StringContent(JsonConvert.SerializeObject(req), System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"http://localhost:{_rpcPort}/jsonrpc", content);
                var jsonStr = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonStr);

                return long.TryParse(json["result"]?["completedLength"]?.ToString(), out long c) ? c : 0;
            }
            catch { return 0; }
        }

        public static async Task UpdateStatusAsync()
        {
            if (!EnsureAriaStarted() || ActiveDownloads.Count == 0) return;

            try
            {
                var req = new
                {
                    jsonrpc = "2.0",
                    id = "update",
                    method = "aria2.tellActive",
                    @params = new object[] { $"token:{_rpcSecret}", new[] { "gid", "status", "totalLength", "completedLength", "downloadSpeed" } }
                };

                var content = new StringContent(JsonConvert.SerializeObject(req), System.Text.Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"http://localhost:{_rpcPort}/jsonrpc", content);
                var jsonStr = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(jsonStr);

                if (json["result"] is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        string gid = item["gid"]?.ToString() ?? "";
                        var dl = ActiveDownloads.Find(d => d.Gid == gid);
                        if (dl != null)
                        {
                            dl.Status = item["status"]?.ToString() ?? "Unknown";
                            long total = long.TryParse(item["totalLength"]?.ToString(), out long t) ? t : 0;
                            long comp = long.TryParse(item["completedLength"]?.ToString(), out long c) ? c : 0;
                            long speed = long.TryParse(item["downloadSpeed"]?.ToString(), out long s) ? s : 0;

                            if (total > 0) dl.ProgressPercent = (double)comp / total * 100.0;
                            dl.DownloadSpeedStr = FormatSpeed(speed);
                            
                            if (speed > 0 && total > 0)
                            {
                                long seconds = (total - comp) / speed;
                                dl.EtaStr = TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
                            }

                            OnDownloadUpdated?.Invoke(dl);
                        }
                    }
                }
            }
            catch { }
        }

        private static string FormatSpeed(long bytesPerSec)
        {
            if (bytesPerSec > 1024 * 1024) return $"{(bytesPerSec / 1024.0 / 1024.0):0.0} MB/s";
            if (bytesPerSec > 1024) return $"{(bytesPerSec / 1024.0):0.0} KB/s";
            return $"{bytesPerSec} B/s";
        }

        public static void Shutdown()
        {
            if (_ariaProcess != null && !_ariaProcess.HasExited)
            {
                try { _ariaProcess.Kill(); } catch { }
            }
        }
    }
}
