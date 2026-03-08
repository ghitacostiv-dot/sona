using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SONA.Services
{
    public static class HarmonyService
    {
        private static Process? _harmonyProcess;
        public static bool IsRunning { get; private set; } = false;

        public static async Task StartAsync()
        {
            if (IsRunning || _harmonyProcess != null) return;

            string path = @"C:\Users\LionGhost\Downloads\harmony-music";
            if (!Directory.Exists(path))
            {
                Debug.WriteLine("Harmony Music directory not found.");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c npm run dev",
                    WorkingDirectory = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _harmonyProcess = new Process { StartInfo = psi };
                _harmonyProcess.Start();
                IsRunning = true;

                // Wait for the server to bind
                await WaitForServerAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start Harmony Music: {ex.Message}");
                Stop();
            }
        }

        private static async Task WaitForServerAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            for (int i = 0; i < 15; i++)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:3000");
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch { }
                await Task.Delay(1000);
            }
        }

        public static void Stop()
        {
            if (_harmonyProcess != null && !_harmonyProcess.HasExited)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/PID {_harmonyProcess.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit(2000);
                }
                catch { }
            }
            _harmonyProcess = null;
            IsRunning = false;
        }
    }
}
