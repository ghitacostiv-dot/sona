using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace SONA.Services
{
    public class TorrentStats
    {
        public string InfoHash { get; set; } = "";
        public string Name { get; set; } = "";
        public double Progress { get; set; }
        public double DownloadSpeed { get; set; }
        public int NumPeers { get; set; }
        public bool Ready { get; set; }
    }

    /// <summary>
    /// Higher-level service to interact with the Nexus Node.js streaming API.
    /// Implements retry logic, process monitoring, and status polling.
    /// </summary>
    public static class NexusTorrentService
    {
        private static readonly HttpClient _http = new HttpClient();

        /// <summary>
        /// Sends a magnet link to the Node.js server and returns the local streaming URL once it's resolved.
        /// Includes health checks and automatic restarts if the server doesn't respond in 5s.
        /// </summary>
        public static async Task<string?> GetStreamUrlAsync(string magnetUrl, int fileIndex = 0)
        {
            // 1. Ensure Node.js is running (Requirement 1 & 4)
            await NexusService.EnsureRunning();

            try
            {
                // 2. Send HTTP GET request with magnet link (Requirement 2)
                var encodedUri = Uri.EscapeDataString(magnetUrl);
                var url = $"{NexusService.ApiUrl}/api/add-magnet?uri={encodedUri}";
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _http.GetAsync(url, cts.Token);
                
                if (!response.IsSuccessStatusCode) return null;

                // 3. Parse JSON response (Requirement 3)
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("infoHash", out var hashProp))
                {
                    string infoHash = hashProp.GetString()?.ToLowerInvariant() ?? "";
                    // The streaming URL format used by VideoPlayer
                    return $"{NexusService.ApiUrl}/api/stream/{infoHash}/{fileIndex}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NexusTorrentService] Request failed: {ex.Message}. Restarting...");
                // Requirement 4: Retry/Restart if no response
                await NexusService.StartAsync();
            }

            return null;
        }

        /// <summary>
        /// Polls the server for the current state of a torrent. (Requirement 5)
        /// </summary>
        public static async Task<TorrentStats?> GetStatsAsync(string infoHash)
        {
            try
            {
                var response = await _http.GetAsync($"{NexusService.ApiUrl}/api/torrents");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<TorrentStats>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return list?.FirstOrDefault(t => t.InfoHash.Equals(infoHash, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }
    }
}
