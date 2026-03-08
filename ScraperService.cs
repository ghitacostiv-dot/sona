using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SONA.Services
{
    /// <summary>
    /// Manages the SONA Master Stream Scraper service (Node.js/Playwright on port 3333).
    /// Call ScrapeAsync() to get a direct .m3u8/.mpd URL from any streaming page.
    /// </summary>
    public static class ScraperService
    {
        private static Process? _process;
        private static string _scraperDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "Scraper");
        private static readonly HttpClient _http = new HttpClient();

        public static bool IsRunning { get; private set; } = false;
        public static string BaseUrl => "http://localhost:3333";

        // ── Start the scraper service ─────────────────────────────────────
        public static async Task StartAsync()
        {
            if (IsRunning) return;

            string root = Directory.GetCurrentDirectory();
            
            // Search up to 5 levels up for the "Services\Scraper" folder (for dev environments)
            string? searchPath = root;
            string? foundScraperPath = null;
            for (int i = 0; i < 5 && searchPath != null; i++)
            {
                var check = Path.Combine(searchPath, "Services", "Scraper");
                if (Directory.Exists(check)) { foundScraperPath = check; break; }

                // Also check SONA/Services/Scraper if running from repo root
                var check2 = Path.Combine(searchPath, "SONA", "Services", "Scraper");
                if (Directory.Exists(check2)) { foundScraperPath = check2; break; }

                searchPath = Path.GetDirectoryName(searchPath);
            }

            if (foundScraperPath != null)
            {
                _scraperDir = foundScraperPath;
            }
            else
            {
                return; // Not found
            }

            var psi = new ProcessStartInfo("node", "scraper.js")
            {
                WorkingDirectory = _scraperDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                _process = Process.Start(psi);
                // Wait for the scraper API to come online
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        innerCts.CancelAfter(TimeSpan.FromSeconds(1));
                        var resp = await _http.GetAsync(BaseUrl + "/health", innerCts.Token);
                        if (resp.IsSuccessStatusCode) { IsRunning = true; return; }
                    }
                    catch { }
                    await Task.Delay(800, cts.Token);
                }
            }
            catch { }
        }

        public static void Stop()
        {
            IsRunning = false;
            try { _process?.Kill(entireProcessTree: true); _process?.Dispose(); _process = null; } catch { }
        }

        // ── Convenience: scrape a single URL with retries ─────────────────
        public static async Task<ScrapeResult?> ScrapeAsync(string sourceUrl, int timeoutMs = 10000)
        {
            if (!IsRunning) return null;
            const int maxRetries = 2;
            Exception? lastEx = null;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var innerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs + 5000));
                    var body = System.Text.Json.JsonSerializer.Serialize(new { source_url = sourceUrl, timeout_ms = timeoutMs });
                    var response = await _http.PostAsync(BaseUrl + "/scrape",
                        new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"),
                        innerCts.Token);
                    var json = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<ScrapeResult>(json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result != null && result.Success && !string.IsNullOrWhiteSpace(result.StreamUrl))
                        return result;
                    if (result != null && !result.Success)
                        return result; // return error result so caller can show result.Error
                    lastEx = new InvalidOperationException("Invalid scraper response: missing or empty stream URL.");
                }
                catch (Exception ex) { lastEx = ex; }
                if (attempt < maxRetries)
                    await Task.Delay(800);
            }
            return new ScrapeResult { Success = false, Error = lastEx?.Message ?? "Scraper request failed." };
        }
    }

    public class ScrapeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? StreamUrl { get; set; }
        public string? StreamType { get; set; }
        public string? Provider { get; set; }
        public ScrapeHeaders? Headers { get; set; }
    }

    public class ScrapeHeaders
    {
        public string? Referer { get; set; }
        public string? UserAgent { get; set; }
        public string? Origin { get; set; }
    }
}
