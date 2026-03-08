using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SONA.Services
{
    public class NativeAppInfo
    {
        public string AppId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string WingetId { get; set; } = "";
        public string? ExecutablePath { get; set; }
        public bool IsInstalled => !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    }

    public static class NativeAppIntegrationService
    {
        private static readonly Dictionary<string, (string WingetId, string ExeName, string DisplayName)> _appConfigs = new()
        {
            { "stremio", ("Stremio.Stremio", "Stremio.exe", "Stremio") },
            { "hydra", ("HydraLauncher.Hydra", "Hydra.exe", "Hydra Launcher") }
        };

        public static async Task<NativeAppInfo> GetAppInfoAsync(string appId)
        {
            if (!_appConfigs.TryGetValue(appId.ToLower(), out var config))
                return new NativeAppInfo { AppId = appId };

            var info = new NativeAppInfo
            {
                AppId = appId,
                DisplayName = config.DisplayName,
                WingetId = config.WingetId,
                ExecutablePath = await FindExecutablePathAsync(config.WingetId, config.ExeName)
            };

            return info;
        }

        public static async Task<bool> InstallAppAsync(string appId, IProgress<string> progress)
        {
            if (!_appConfigs.TryGetValue(appId.ToLower(), out var config)) return false;

            progress.Report($"Starting installation of {config.DisplayName} via winget...");
            await WingetInstaller.InstallPackagesAsync(new[] { config.WingetId }, progress);
            
            return (await GetAppInfoAsync(appId)).IsInstalled;
        }

        private static async Task<string?> FindExecutablePathAsync(string wingetId, string exeName)
        {
            // 1. Check common AppData/Local paths
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Specifically for Stremio/Hydra typical paths
            string[] specificPaths = {
                Path.Combine(localAppData, "Programs", "hydra", exeName),
                Path.Combine(localAppData, "Programs", "LBRY", exeName),
                Path.Combine(localAppData, "Smart Code LTD", "Stremio-4", exeName),
                Path.Combine(localAppData, "stremio-runtime", exeName),
                Path.Combine(localAppData, "Hydra", exeName),
                Path.Combine(programFiles, "Stremio-4", exeName),
                Path.Combine(programFilesX86, "Stremio-4", exeName),
                Path.Combine(localAppData, "Programs", "Stremio-4", exeName),
                Path.Combine(userProfile, "AppData", "Local", "Programs", "hydra", exeName)
            };

            foreach (var path in specificPaths)
            {
                if (File.Exists(path)) return path;
            }

            // 2. Check Registry for InstallLocation (More reliable)
            var programConfigs = new (string KeyName, string DisplayName)[] {
                (wingetId, exeName.Replace(".exe", "")),
                ("Stremio", "Stremio"),
                ("Hydra", "Hydra")
            };

            foreach (var config in programConfigs)
            {
                string pathFromReg = GetInstallPathFromRegistry(config.DisplayName);
                if (!string.IsNullOrEmpty(pathFromReg))
                {
                    string fullPath = Path.Combine(pathFromReg, exeName);
                    if (File.Exists(fullPath)) return fullPath;
                    
                    // Recursive search in that dir
                    try {
                        var files = Directory.GetFiles(pathFromReg, exeName, SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    } catch { }
                }
            }

            // 3. Check PATH
            if (DependencyChecker.IsExecutableInPath(exeName))
            {
                // Try to resolve full path from 'where'
                try
                {
                    var psi = new ProcessStartInfo("where", exeName) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using var proc = Process.Start(psi);
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0 && File.Exists(lines[0])) return lines[0];
                }
                catch { }
            }

            // 4. Last resort: Registry lookups for uninstall strings
            var installed = SystemUtils.GetInstalledPrograms();
            var matched = installed.FirstOrDefault(p => p.DisplayName.Contains(exeName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase) 
                                                     || p.UninstallString.Contains(exeName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
            
            if (matched != null)
            {
                // Extract directory from uninstall string if possible
                try {
                    string cleanUninstall = matched.UninstallString.Replace("\"", "").Trim();
                    string? dir = Path.GetDirectoryName(cleanUninstall);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var files = Directory.GetFiles(dir, exeName, SearchOption.AllDirectories);
                        if (files.Length > 0) return files[0];
                    }
                } catch { }
            }

            return null;
        }

        private static string GetInstallPathFromRegistry(string displayName)
        {
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var keyPath in registryKeys)
            {
                foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using var key = baseKey.OpenSubKey(keyPath);
                        if (key == null) continue;
                        foreach (var subkeyName in key.GetSubKeyNames())
                        {
                            using var subkey = key.OpenSubKey(subkeyName);
                            if (subkey == null) continue;
                            var name = subkey.GetValue("DisplayName") as string;
                            if (name != null && name.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                            {
                                var installLoc = subkey.GetValue("InstallLocation") as string;
                                if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                                    return installLoc;
                            }
                        }
                    }
                    catch { }
                }
            }
            return "";
        }
    }
}
