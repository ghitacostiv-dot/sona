using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SONA.Services
{
    public static class WingetInstaller
    {
        public static async Task InstallPackagesAsync(IEnumerable<string> packageIds, IProgress<string> progress)
        {
            foreach (var id in packageIds)
            {
                progress?.Report($"Installing {id}...");
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install --id {id} -e --silent --accept-package-agreements --accept-source-agreements",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                try
                {
                    using var proc = Process.Start(psi);
                    if (proc == null) { progress?.Report($"❌ Failed to start winget for {id}"); continue; }
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    string error = await proc.StandardError.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    if (proc.ExitCode == 0)
                        progress?.Report($"✅ {id} installed successfully.");
                    else
                        progress?.Report($"❌ Failed to install {id}: {error?.Trim() ?? output?.Trim()}");
                }
                catch (Exception ex)
                {
                    progress?.Report($"❌ Error installing {id}: {ex.Message}");
                }
            }
        }
    }
}
