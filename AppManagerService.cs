using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SONA.Services
{
    public class AppInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ExeNames { get; set; } = ""; // Comma separated possible exe names
        public string InstallSourceType { get; set; } = "Winget"; // "Winget" or "GitHub"
        public string InstallSourceId { get; set; } = ""; // Winget ID or GitHub "Owner/Repo"
        public string Keyword { get; set; } = ""; // Used for path searching
    }

    public static class AppManagerService
    {
        private static readonly Dictionary<string, AppInfo> Apps = new()
        {
            // --- Winget Apps ---
            { "brave",      new AppInfo { Id = "brave",      Name = "Brave Browser",          ExeNames = "brave.exe",                InstallSourceType = "Winget", InstallSourceId = "Brave.Brave",                               Keyword = "BraveSoftware\\Brave-Browser" } },
            { "gplay",      new AppInfo { Id = "gplay",      Name = "Google Play / Custom",   ExeNames = "",                         InstallSourceType = "Winget", InstallSourceId = "",                                          Keyword = "" } },
            { "yacreader",  new AppInfo { Id = "yacreader",  Name = "YACReader",               ExeNames = "YACReader.exe",            InstallSourceType = "Winget", InstallSourceId = "YACReader.YACReader",                       Keyword = "YACReader" } },
            { "poddr",      new AppInfo { Id = "poddr",      Name = "Poddr",                   ExeNames = "Poddr.exe",                InstallSourceType = "Winget", InstallSourceId = "Sn8z.Poddr",                                Keyword = "Poddr" } },
            { "retrobat",   new AppInfo { Id = "retrobat",   Name = "RetroBat",                ExeNames = "retrobat.exe",             InstallSourceType = "Winget", InstallSourceId = "RetroBat.RetroBat",                         Keyword = "RetroBat" } },
            { "osu",        new AppInfo { Id = "osu",        Name = "osu!",                    ExeNames = "osu!.exe",                 InstallSourceType = "Winget", InstallSourceId = "ppy.osu!",                                  Keyword = "osu!" } },
            { "tailscale",  new AppInfo { Id = "tailscale",  Name = "Tailscale",               ExeNames = "tailscale-ipn.exe,tailscaled.exe", InstallSourceType = "Winget", InstallSourceId = "Tailscale.Tailscale",            Keyword = "Tailscale" } },
            { "moonlight",  new AppInfo { Id = "moonlight",  Name = "Moonlight",               ExeNames = "Moonlight.exe",            InstallSourceType = "Winget", InstallSourceId = "MoonlightGameStreamingProject.Moonlight",   Keyword = "Moonlight Game Streaming" } },
            { "steam",      new AppInfo { Id = "steam",      Name = "Steam",                   ExeNames = "steam.exe",                InstallSourceType = "Winget", InstallSourceId = "Valve.Steam",                               Keyword = "Steam" } },
            { "epic",       new AppInfo { Id = "epic",       Name = "Epic Games Launcher",      ExeNames = "EpicGamesLauncher.exe",   InstallSourceType = "Winget", InstallSourceId = "EpicGames.EpicGamesLauncher",               Keyword = "Epic Games\\Launcher" } },
            { "spotify",    new AppInfo { Id = "spotify",    Name = "Spotify",                 ExeNames = "Spotify.exe",              InstallSourceType = "Winget", InstallSourceId = "Spotify.Spotify",                           Keyword = "Spotify" } },
            { "discord",    new AppInfo { Id = "discord",    Name = "Discord",                 ExeNames = "Discord.exe",              InstallSourceType = "Winget", InstallSourceId = "Discord.Discord",                           Keyword = "Discord" } },
            { "whatsapp",   new AppInfo { Id = "whatsapp",   Name = "WhatsApp",                ExeNames = "WhatsApp.exe",             InstallSourceType = "Winget", InstallSourceId = "WhatsApp.WhatsApp",                         Keyword = "WhatsApp" } },
            { "battlenet",  new AppInfo { Id = "battlenet",  Name = "Battle.net",              ExeNames = "Battle.net.exe",           InstallSourceType = "Winget", InstallSourceId = "Blizzard.BattleNet",                        Keyword = "Battle.net" } },

            // --- GitHub Apps ---
            { "nuclear",    new AppInfo { Id = "nuclear",    Name = "Nuclear Music Player",    ExeNames = "nuclear.exe",              InstallSourceType = "GitHub", InstallSourceId = "nukeop/nuclear",                            Keyword = "nuclear" } },
            { "miru",       new AppInfo { Id = "miru",       Name = "Miru",                    ExeNames = "Miru.exe",                 InstallSourceType = "GitHub", InstallSourceId = "ThaUnknown/miru",                           Keyword = "Miru" } },
            { "librum",     new AppInfo { Id = "librum",     Name = "Librum",                  ExeNames = "Librum.exe",               InstallSourceType = "GitHub", InstallSourceId = "Librum-Reader/Librum",                      Keyword = "Librum" } },
            { "mangayomi",  new AppInfo { Id = "mangayomi",  Name = "Mangayomi",               ExeNames = "mangayomi.exe",            InstallSourceType = "GitHub", InstallSourceId = "kodjodevf/mangayomi",                       Keyword = "mangayomi" } },
            { "hydra",      new AppInfo { Id = "hydra",      Name = "Hydra Launcher",          ExeNames = "hydra.exe",                InstallSourceType = "GitHub", InstallSourceId = "hydralauncher/hydra",                       Keyword = "hydra" } },
        };

        private static readonly string DownloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SONA_Downloads");

        // ── Start Menu locations ────────────────────────────────────────────────
        private static readonly string[] StartMenuRoots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),       "Programs"),
        };

        static AppManagerService()
        {
            if (!Directory.Exists(DownloadDir))
                Directory.CreateDirectory(DownloadDir);
        }

        public static AppInfo? GetApp(string id) => Apps.TryGetValue(id.ToLower(), out var app) ? app : null;

        // ── Public catalogue access ────────────────────────────────────────────
        public static IReadOnlyDictionary<string, AppInfo> AllApps => Apps;

        // ══════════════════════════════════════════════════════════════════════
        // START MENU SCANNER
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resolves all .lnk shortcuts in the Start Menu and returns (DisplayName, TargetExePath) pairs.
        /// Uses WScript.Shell COM to resolve shortcuts — no extra NuGet needed.
        /// </summary>
        public static List<(string Name, string ExePath)> ScanStartMenu()
        {
            var results = new List<(string, string)>();

            foreach (var root in StartMenuRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var lnkFiles = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
                    foreach (var lnk in lnkFiles)
                    {
                        try
                        {
                            string? target = ResolveLnkTarget(lnk);
                            if (string.IsNullOrEmpty(target)) continue;
                            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!File.Exists(target)) continue;

                            // Skip installer/uninstaller entries
                            string lower = target.ToLowerInvariant();
                            if (lower.Contains("uninstall") || lower.Contains("setup") ||
                                lower.Contains("helper") || lower.Contains("updater") ||
                                lower.Contains("crashpad") || lower.Contains("squirrel"))
                                continue;

                            string name = Path.GetFileNameWithoutExtension(lnk);
                            results.Add((name, target));
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return results;
        }

        /// <summary>
        /// Resolves a .lnk file to its target path using WScript.Shell COM automation.
        /// </summary>
        private static string? ResolveLnkTarget(string lnkPath)
        {
            try
            {
                // Use WScript.Shell to resolve the shortcut
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string target = shortcut.TargetPath;
                Marshal.ReleaseComObject(shortcut);
                Marshal.ReleaseComObject(shell);
                return string.IsNullOrEmpty(target) ? null : target;
            }
            catch { return null; }
        }

        // ══════════════════════════════════════════════════════════════════════
        // FULL INSTALLED APPS SCAN (async, for InstalledAppsPage)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Async comprehensive scan: Start Menu + Registry uninstall keys.
        /// Returns deduplicated list suitable for InstalledAppsPage.
        /// </summary>
        public static Task<List<(string Name, string ExePath)>> ScanAllInstalledAppsAsync(
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var byExe = new Dictionary<string, (string Name, string ExePath)>(StringComparer.OrdinalIgnoreCase);

                // ── Pass 1: Start Menu (fastest + most complete for GUI apps) ──
                progress?.Report("📂 Scanning Start Menu…");
                var startMenu = ScanStartMenu();
                foreach (var (name, exe) in startMenu)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!byExe.ContainsKey(exe))
                        byExe[exe] = (name, exe);
                }
                progress?.Report($"   Start Menu: {byExe.Count} apps found so far");

                // ── Pass 2: Registry Uninstall keys ───────────────────────────
                progress?.Report("🔍 Scanning Registry (installed programs)…");
                var regApps = ScanRegistryInstalledApps();
                foreach (var (name, exe) in regApps)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!byExe.ContainsKey(exe))
                        byExe[exe] = (name, exe);
                }
                progress?.Report($"   Registry: {byExe.Count} total apps found");

                progress?.Report($"✅ Scan complete — {byExe.Count} apps discovered");
                return byExe.Values.ToList();
            }, ct);
        }

        /// <summary>
        /// Reads HKLM + HKCU Uninstall registry keys and returns (DisplayName, MainExe) pairs.
        /// </summary>
        private static List<(string Name, string ExePath)> ScanRegistryInstalledApps()
        {
            var results = new List<(string, string)>();
            var regRoots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            using var hklm = Microsoft.Win32.Registry.LocalMachine;
            using var hkcu = Microsoft.Win32.Registry.CurrentUser;

            foreach (var root in regRoots)
            {
                foreach (var hive in new[] { hklm, hkcu })
                {
                    try
                    {
                        using var key = hive.OpenSubKey(root);
                        if (key == null) continue;
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var sub = key.OpenSubKey(subKeyName);
                                if (sub == null) continue;

                                string? displayName = sub.GetValue("DisplayName") as string;
                                string? installLoc  = sub.GetValue("InstallLocation") as string;
                                string? displayIcon = sub.GetValue("DisplayIcon") as string;

                                if (string.IsNullOrWhiteSpace(displayName)) continue;

                                // Skip Windows components, updates, drivers
                                if (displayName.StartsWith("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase) ||
                                    displayName.StartsWith("Microsoft .NET", StringComparison.OrdinalIgnoreCase) ||
                                    displayName.StartsWith("Windows ", StringComparison.OrdinalIgnoreCase) ||
                                    displayName.Contains("Update") ||
                                    displayName.Contains("Redistributable") ||
                                    displayName.Contains("Runtime"))
                                    continue;

                                // Try DisplayIcon first — often points to the real exe
                                if (!string.IsNullOrEmpty(displayIcon))
                                {
                                    string iconPath = displayIcon.Split(',')[0].Trim('"', ' ');
                                    if (iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
                                    {
                                        results.Add((displayName, iconPath));
                                        continue;
                                    }
                                }

                                // Try InstallLocation dir for a single .exe
                                if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc))
                                {
                                    var exes = Directory.GetFiles(installLoc, "*.exe", SearchOption.TopDirectoryOnly)
                                        .Where(f =>
                                        {
                                            string n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                                            return !n.Contains("uninstall") && !n.Contains("setup") &&
                                                   !n.Contains("helper") && !n.Contains("updater");
                                        })
                                        .ToList();

                                    if (exes.Count == 1)
                                    {
                                        results.Add((displayName, exes[0]));
                                    }
                                    else if (exes.Count > 1)
                                    {
                                        // Prefer exe whose name most closely resembles the app name
                                        string appLower = displayName.Replace(" ", "").ToLowerInvariant();
                                        var best = exes.OrderByDescending(e =>
                                        {
                                            string en = Path.GetFileNameWithoutExtension(e).Replace(" ", "").ToLowerInvariant();
                                            int overlap = 0;
                                            for (int i = 0; i < Math.Min(en.Length, appLower.Length); i++)
                                                if (en[i] == appLower[i]) overlap++;
                                            return overlap;
                                        }).First();
                                        results.Add((displayName, best));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            return results;
        }

        // ══════════════════════════════════════════════════════════════════════
        // EXISTING – FindExecutable (now Start Menu first)
        // ══════════════════════════════════════════════════════════════════════

        public static string? FindExecutable(string id)
        {
            var app = GetApp(id);
            if (app == null) return null;

            // ── 0. Check manual override in AppConfig ─────────────────────────
            string configPath = AppConfig.GetString($"{id}_exe_path");
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath)) return configPath;

            var exeNames = app.ExeNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .ToArray();

            // ── 1. Start Menu shortcuts ───────────────────────────────────────
            string? startMenuPath = SearchStartMenuForApp(app, exeNames);
            if (startMenuPath != null) { AppConfig.Set($"{id}_exe_path", startMenuPath); return startMenuPath; }

            // ── 2. Windows Registry uninstall keys ────────────────────────────
            string? regPath = SearchRegistry(app, exeNames);
            if (regPath != null) { AppConfig.Set($"{id}_exe_path", regPath); return regPath; }

            // ── 3. `where.exe` PATH lookup (fast) ────────────────────────────
            foreach (var exeName in exeNames)
            {
                try
                {
                    var result = Process.Start(new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = exeName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    if (result != null)
                    {
                        string output = result.StandardOutput.ReadToEnd().Trim();
                        result.WaitForExit();
                        if (!string.IsNullOrEmpty(output))
                        {
                            string found = output.Split('\n')[0].Trim();
                            if (File.Exists(found)) { AppConfig.Set($"{id}_exe_path", found); return found; }
                        }
                    }
                }
                catch { }
            }

            // ── 4. Search common install paths ────────────────────────────────
            var commonPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData\\Local\\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop\\apps"),
                @"C:\Program Files",
                @"C:\Program Files (x86)"
            };

            foreach (var basePath in commonPaths)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    if (!string.IsNullOrEmpty(app.Keyword))
                    {
                        var keywordDirs = Directory.EnumerateDirectories(basePath, $"*{app.Keyword}*", SearchOption.AllDirectories)
                            .Where(d => !d.Contains("Temp") && !d.Contains("Cache") && !d.Contains("Crash") && !d.Contains("\\bin\\Debug"));
                        foreach (var dir in keywordDirs)
                            foreach (var exe in exeNames)
                            {
                                string p = Path.Combine(dir, exe);
                                if (File.Exists(p)) { AppConfig.Set($"{id}_exe_path", p); return p; }
                            }
                    }
                }
                catch { }
            }

            // ── 5. Full scan of all logical drives (thorough but slow) ─────────
            foreach (var drive in DriveInfo.GetDrives()
                         .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable)))
            {
                foreach (var exeName in exeNames)
                {
                    try
                    {
                        var roots = new[]
                        {
                            Path.Combine(drive.RootDirectory.FullName, "Program Files"),
                            Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)"),
                            Path.Combine(drive.RootDirectory.FullName, "Users"),
                            Path.Combine(drive.RootDirectory.FullName, "Apps"),
                            Path.Combine(drive.RootDirectory.FullName, "Portable"),
                            Path.Combine(drive.RootDirectory.FullName, "Games"),
                        };
                        foreach (var root in roots)
                        {
                            if (!Directory.Exists(root)) continue;
                            var files = Directory.EnumerateFiles(root, exeName, SearchOption.AllDirectories)
                                .Where(f => !f.Contains("Temp") && !f.Contains("Cache") && !f.Contains("Setup")
                                         && !f.Contains("Uninstall") && !f.Contains("\\bin\\Debug"));
                            foreach (var f in files)
                            {
                                AppConfig.Set($"{id}_exe_path", f);
                                return f;
                            }
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        // ── Start Menu targeted search ─────────────────────────────────────────
        private static string? SearchStartMenuForApp(AppInfo app, string[] exeNames)
        {
            foreach (var root in StartMenuRoots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    var lnkFiles = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
                    foreach (var lnk in lnkFiles)
                    {
                        // Quick name match before paying the COM cost
                        string lnkName = Path.GetFileNameWithoutExtension(lnk);
                        bool nameMatch = lnkName.Contains(app.Name, StringComparison.OrdinalIgnoreCase) ||
                                         lnkName.Contains(app.Keyword, StringComparison.OrdinalIgnoreCase) ||
                                         exeNames.Any(e => lnkName.Contains(Path.GetFileNameWithoutExtension(e), StringComparison.OrdinalIgnoreCase));
                        if (!nameMatch) continue;

                        string? target = ResolveLnkTarget(lnk);
                        if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                        if (exeNames.Any(e => target.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                            return target;
                    }
                }
                catch { }
            }
            return null;
        }

        // ── Registry search (unchanged logic) ─────────────────────────────────
        private static string? SearchRegistry(AppInfo app, string[] exeNames)
        {
            var regRoots = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            using var hklm = Microsoft.Win32.Registry.LocalMachine;
            using var hkcu = Microsoft.Win32.Registry.CurrentUser;

            foreach (var root in regRoots)
            {
                foreach (var hive in new[] { hklm, hkcu })
                {
                    try
                    {
                        using var key = hive.OpenSubKey(root);
                        if (key == null) continue;
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var sub = key.OpenSubKey(subKeyName);
                                if (sub == null) continue;
                                string? displayName = sub.GetValue("DisplayName") as string;
                                string? installLoc  = sub.GetValue("InstallLocation") as string;
                                string? displayIcon = sub.GetValue("DisplayIcon") as string;

                                bool nameMatch = !string.IsNullOrEmpty(displayName) &&
                                    (displayName.Contains(app.Name, StringComparison.OrdinalIgnoreCase) ||
                                     displayName.Contains(app.Keyword, StringComparison.OrdinalIgnoreCase));

                                if (nameMatch)
                                {
                                    if (!string.IsNullOrEmpty(installLoc))
                                    {
                                        foreach (var exe in exeNames)
                                        {
                                            string p = Path.Combine(installLoc, exe);
                                            if (File.Exists(p)) return p;
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(displayIcon))
                                    {
                                        string iconPath = displayIcon.Split(',')[0].Trim('"', ' ');
                                        if (File.Exists(iconPath) &&
                                            exeNames.Any(e => iconPath.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                                            return iconPath;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        public static bool IsInstalled(string id) => !string.IsNullOrEmpty(FindExecutable(id));

        public static async Task<bool> InstallAppAsync(string id, Action<string>? progressCallback = null)
        {
            var app = GetApp(id);
            if (app == null) return false;

            if (IsInstalled(id)) return true;

            try
            {
                if (app.InstallSourceType == "Winget")
                    return await InstallViaWinget(app, progressCallback);
                else if (app.InstallSourceType == "GitHub")
                    return await InstallViaGitHub(app, progressCallback);
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Error installing {app.Name}: {ex.Message}");
                Debug.WriteLine($"Install Error: {ex}");
            }

            return false;
        }

        private static async Task<bool> InstallViaWinget(AppInfo app, Action<string>? progressCallback)
        {
            progressCallback?.Invoke($"Downloading and installing {app.Name} via Winget...");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = $"install --id {app.InstallSourceId} --exact --silent --accept-package-agreements --accept-source-agreements",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            await proc.WaitForExitAsync();

            if (proc.ExitCode == 0)
            {
                progressCallback?.Invoke($"{app.Name} installed successfully.");
                return true;
            }
            else
            {
                string error = await proc.StandardError.ReadToEndAsync();
                progressCallback?.Invoke($"Winget installation failed for {app.Name}. Error: {error}");
                return false;
            }
        }

        public static async Task<bool> InstallPackageAsync(string wingetId)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "winget",
                        Arguments = $"install --id {wingetId} --exact --silent --accept-package-agreements --accept-source-agreements",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
        }

        private static async Task<bool> InstallViaGitHub(AppInfo app, Action<string>? progressCallback)
        {
            progressCallback?.Invoke($"Fetching latest release for {app.Name} from GitHub...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SONA-Installer");

            string apiUrl = $"https://api.github.com/repos/{app.InstallSourceId}/releases/latest";
            var response = await client.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Failed to find GitHub release for {app.Name}.");
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var releaseData = JObject.Parse(content);
            var assets = releaseData["assets"] as JArray;

            if (assets == null || assets.Count == 0) return false;

            var targetAsset = assets.FirstOrDefault(a =>
                a["name"]?.ToString().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true &&
                !a["name"]?.ToString().Contains("mac", StringComparison.OrdinalIgnoreCase) == true &&
                !a["name"]?.ToString().Contains("linux", StringComparison.OrdinalIgnoreCase) == true)
                ?? assets.FirstOrDefault(a => a["name"]?.ToString().EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true);

            if (targetAsset == null)
            {
                progressCallback?.Invoke($"No Windows executable found for {app.Name}.");
                return false;
            }

            string downloadUrl = targetAsset["browser_download_url"]?.ToString() ?? "";
            string fileName = targetAsset["name"]?.ToString() ?? $"{app.Id}_installer.exe";
            string filePath = Path.Combine(DownloadDir, fileName);

            progressCallback?.Invoke($"Downloading {fileName}...");

            try
            {
                var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(filePath, fileBytes);
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Download failed: {ex.Message}");
                return false;
            }

            progressCallback?.Invoke($"Installing {app.Name}...");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = "/S /quiet",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            proc.Start();
            await proc.WaitForExitAsync();

            progressCallback?.Invoke($"{app.Name} installation finished. Mapping executable...");
            await Task.Delay(2000);

            return IsInstalled(app.Id);
        }

        public static async Task AutoRunFirstTimeChecksAsync(Action<string> logger)
        {
            logger("SONA — First-run app detection started...");

            foreach (var kvp in Apps)
            {
                string id = kvp.Key;
                var app  = kvp.Value;
                try
                {
                    string? path = FindExecutable(id);
                    if (!string.IsNullOrEmpty(path))
                        logger($"✅ {app.Name} — already installed.");
                    else
                    {
                        logger($"⬇️  {app.Name} — not found, installing silently...");
                        bool ok = await InstallAppAsync(id, logger);
                        logger(ok ? $"✅ {app.Name} — installed." : $"⚠️  {app.Name} — install failed (try manually).");
                    }
                }
                catch (Exception ex)
                {
                    logger($"⚠️  {app.Name} error: {ex.Message}");
                }
            }

            AppConfig.Set("first_run_complete", true);
            logger("First-run setup complete.");
        }

        public static void AutoRunFirstTimeChecks(Action<string> logger)
        {
            _ = AutoRunFirstTimeChecksAsync(logger);
        }
    }
}
