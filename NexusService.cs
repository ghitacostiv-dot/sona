using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace SONA.Services
{
    /// <summary>
    /// NexusService ΓÇö starts the Nexus React/Node.js app on port 3003,
    /// then waits for it to accept connections before signaling ready.
    /// </summary>
    public static class NexusService
    {
        private static Process? _nexusProcess;
        private static string _nexusDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nexus");
        private static readonly HttpClient _http = new HttpClient();

        public static bool IsRunning { get; private set; } = false;
        public static string BaseUrl => "http://localhost:3003";
        public static string ApiUrl => "http://localhost:3004";

        public static async Task EnsureRunning()
        {
            if (!IsRunning)
            {
                await StartAsync();
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var res = await _http.GetAsync(ApiUrl + "/api/health", cts.Token);
                if (!res.IsSuccessStatusCode) throw new Exception("Unhealthy");
            }
            catch
            {
                IsRunning = false;
                await StartAsync();
            }
        }

        public static async Task StartAsync()
        {
            if (IsRunning) return;

            // Kill anything already on port 3003 and 3004
            await Task.Run(() => KillProcessOnPort(3003));
            await Task.Run(() => KillProcessOnPort(3004));

            string root = Directory.GetCurrentDirectory();
            
            // Search up to 5 levels up for the "Services\Nexus" folder (for dev environments)
            string? searchPath = root;
            string? foundNexusPath = null;
            for (int i = 0; i < 5 && searchPath != null; i++)
            {
                var check = Path.Combine(searchPath, "Services", "Nexus");
                if (Directory.Exists(check)) { foundNexusPath = check; break; }
                
                // Also check SONA/Services/Nexus if running from repo root
                var check2 = Path.Combine(searchPath, "SONA", "Services", "Nexus");
                if (Directory.Exists(check2)) { foundNexusPath = check2; break; }

                searchPath = Path.GetDirectoryName(searchPath);
            }

            if (foundNexusPath != null)
            {
                _nexusDir = foundNexusPath;
            }
            else
            {
                // Fallback to absolute user path
                _nexusDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nexus");
            }

            string cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            var psi = new ProcessStartInfo(cmdPath, "/c npm run start")
            {
                WorkingDirectory = _nexusDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            // Ensure System32 is in Path for cmd.exe and utilities
            string currentPathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPathEnv.Contains("System32"))
            {
                psi.EnvironmentVariables["PATH"] = currentPathEnv + ";C:\\Windows\\System32";
            }

            try
            {
                _nexusProcess = Process.Start(psi);
                
                // Wait for the server to start accepting connections (increased to 30s)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        using var innerCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        innerCts.CancelAfter(TimeSpan.FromSeconds(1));
                        var resp = await _http.GetAsync(BaseUrl + "/", innerCts.Token);
                        if (resp.IsSuccessStatusCode)
                        {
                            IsRunning = true;
                            return;
                        }
                    }
                    catch { }
                    await Task.Delay(1000, cts.Token);
                }
            }
            catch { }
        }

        private static void KillProcessOnPort(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd", $"/c netstat -ano | findstr :{port}")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                var p = Process.Start(psi);
                if (p != null)
                {
                    var o = p.StandardOutput.ReadToEnd();
                    foreach (var line in o.Split('\n'))
                    {
                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid))
                        {
                            try { Process.GetProcessById(pid)?.Kill(true); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        public static void Stop()
        {
            IsRunning = false;
            try
            {
                _nexusProcess?.Kill(entireProcessTree: true);
                _nexusProcess?.Dispose();
                _nexusProcess = null;
            }
            catch { }
        }
    }
}
