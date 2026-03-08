using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SONA.Models;

namespace SONA.Services
{
    public static class DependencyChecker
    {
        public static List<DependencyItem> GetDefaultDependencies()
        {
            return new List<DependencyItem>
            {
                new() { DisplayName = "ADB (Universal Driver)", PackageId = "ClockworkMod.UniversalADBDriver" },
                new() { DisplayName = "yt-dlp", PackageId = "yt-dlp.yt-dlp" },
                new() { DisplayName = "qBittorrent (Enhanced)", PackageId = "c0re100.qBittorrent-Enhanced-Edition" },
                new() { DisplayName = "Aria2", PackageId = "aria2.aria2" },
                new() { DisplayName = "VLC media player", PackageId = "VideoLan.VLC" },
                new() { DisplayName = "WinRAR", PackageId = "RARLab.WinRAR" },
                new() { DisplayName = "7-Zip", PackageId = "7zip.7zip" },
                new() { DisplayName = "Brave Browser", PackageId = "Brave.Brave" }
            };
        }

        public static bool IsExecutableInPath(string exeName)
        {
            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var full = Path.Combine(path.Trim(), exeName);
                if (File.Exists(full)) return true;
                if (File.Exists(full + ".exe")) return true;
            }
            return false;
        }

        public static bool IsWingetPackageInstalled(string packageId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"list --id {packageId} --exact",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                return output.Contains(packageId) && !output.Contains("No installed package found");
            }
            catch { return false; }
        }

        public static void CheckInstalledStatus(List<DependencyItem> deps)
        {
            foreach (var dep in deps)
            {
                bool installed = dep.DisplayName switch
                {
                    "ADB (Universal Driver)" => IsExecutableInPath("adb"),
                    "yt-dlp" => IsExecutableInPath("yt-dlp"),
                    "qBittorrent (Enhanced)" => IsExecutableInPath("qbittorrent"),
                    "Aria2" => IsExecutableInPath("aria2c"),
                    "VLC media player" => IsExecutableInPath("vlc"),
                    "WinRAR" => IsExecutableInPath("winrar"),
                    "7-Zip" => IsExecutableInPath("7z"),
                    "Brave Browser" => IsExecutableInPath("brave"),
                    _ => false
                };
                if (!installed)
                    installed = IsWingetPackageInstalled(dep.PackageId);
                dep.IsInstalled = installed;
            }
        }
    }
}
