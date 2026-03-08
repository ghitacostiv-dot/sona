using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SONA
{
    public static class AppConfig
    {
        public static readonly string AppDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "SONA_Data");
        public static readonly string DataDir = Path.Combine(AppDir, "data");
        public static readonly string CacheDir = Path.Combine(AppDir, "cache");
        public static readonly string BgDir = Path.Combine(AppDir, "backgrounds");
        public static readonly string MusicDir = Path.Combine(AppDir, "music");
        public static readonly string BooksDir = Path.Combine(AppDir, "books");
        public static readonly string DownloadDir = Path.Combine(AppDir, "downloads");
        public static readonly string GamesDir = Path.Combine(AppDir, "games");
        public static readonly string LogsDir = Path.Combine(AppDir, "logs");
        public static readonly string ConfigPath = Path.Combine(DataDir, "config.json");

        private static Dictionary<string, object?> _data = new();
        private static readonly object _lock = new();

        public static void EnsureDirectories()
        {
            foreach (var d in new[] { AppDir, DataDir, CacheDir, BgDir, MusicDir, BooksDir, DownloadDir, GamesDir, LogsDir })
                Directory.CreateDirectory(d);
            Load();
        }

        public static void Load()
        {
            lock (_lock)
            {
                _data = new Dictionary<string, object?>
                {
                    // --- General & UI ---
                    ["accent_color"] = "#7c3aed",
                    ["theme"] = "dark",
                    ["bg_path"] = "",
                    ["bg_opacity"] = 0.15,
                    ["bg_blur"] = 0.0,
                    ["show_animations"] = true,
                    ["sidebar_width"] = 240,
                    ["compact_mode"] = false,
                    ["high_quality_icons"] = true,
                    ["font_size_base"] = 14,
                    ["border_radius"] = 8,
                    ["show_tray_icon"] = true,
                    ["minimize_to_tray"] = false,
                    ["close_to_tray"] = false,
                    ["start_with_windows"] = false,
                    ["language"] = "en",

                    // --- Media Playback ---
                    ["volume"] = 0.7,
                    ["muted"] = false,
                    ["auto_resume"] = true,
                    ["auto_fullscreen"] = false,
                    ["skip_intro_duration"] = 85,
                    ["player_buffer_size"] = 10485760, // 10MB
                    ["hardware_acceleration"] = true,
                    ["subtitles_enabled"] = true,
                    ["subtitles_size"] = 24,
                    ["subtitles_color"] = "#ffffff",
                    ["subtitles_bg"] = "#00000000",
                    ["audio_language_pref"] = "eng",
                    ["subtitle_language_pref"] = "eng",
                    ["remember_player_size"] = true,
                    ["discord_rpc_enabled"] = true,

                    // --- Movies & TV ---
                    ["stremio_addons"] = "[\"https://cinemeta-live.strem.io/manifest.json\"]",
                    ["prefer_torrents"] = false,
                    ["torrent_streaming_buffer"] = 20, // 20%
                    ["torrent_stream_buffer_mb"] = 10, // MB to buffer before starting playback (browser-like progressive)
                    ["default_resolution"] = "1080p",
                    ["tmdb_api_key"] = "",
                    ["fanart_api_key"] = "",

                    // --- Games ---
                    ["games_path"] = "",
                    ["show_repack_sources"] = true,
                    ["auto_update_sources"] = true,
                    ["steam_key"] = "",
                    ["rawg_api_key"] = "",
                    ["steamgriddb_key"] = "",
                    ["igdb_client_id"] = "",
                    ["igdb_client_secret"] = "",
                    ["gog_galaxy_integration"] = false,
                    ["epic_games_integration"] = false,

                    // --- Network & Downloads ---
                    ["download_path"] = DownloadDir,
                    ["aria2_port"] = 6800,
                    ["max_concurrent_downloads"] = 3,
                    ["max_download_speed"] = 0, // Unrestricted
                    ["max_upload_speed"] = 0,
                    ["proxy_enabled"] = false,
                    ["proxy_url"] = "",
                    ["proxy_type"] = "http",
                    ["proxy_user"] = "",
                    ["proxy_pass"] = "",
                    ["dns_over_https"] = "https://cloudflare-dns.com/dns-query",
                    ["check_for_updates"] = true,
                    ["torrent_client"] = "aria2",
                    ["qbittorrent_url"] = "http://localhost:8080",
                    ["qbittorrent_user"] = "admin",
                    ["qbittorrent_pass"] = "",

                    // --- Privacy & Security ---
                    ["adblock_enabled"] = true,
                    ["block_tracking"] = true,
                    ["block_telemetry"] = true,
                    ["clear_cache_on_exit"] = false,
                    ["clear_history_on_exit"] = false,
                    ["user_agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                    ["adblock_lists"] = "[\"https://raw.githubusercontent.com/uBlockOrigin/uAssets/master/filters/filters.txt\"]",

                    // --- Home Automation & Misc ---
                    ["wol_mac_address"] = "",
                    ["wol_broadcast_ip"] = "255.255.255.255",
                    ["wol_port"] = 9,
                    ["wol_targets"] = "[]",
                    ["run_first_run_wizard"] = true,
                    ["selected_packages"] = "[]",
                    ["last_fm_key"] = "",

                    // --- Performance ---
                    ["max_memory_cache"] = 512, // MB
                    ["preload_images"] = true,
                    ["low_power_mode"] = false,
                    ["gpu_rendering"] = true,

                    // --- Launcher Paths ---
                    ["hydra_exe_path"] = "",
                    ["stremio_exe_path"] = "",
                    ["browser_exe_path"] = "",
                    ["gplay_exe_path"] = "",
                    ["weather_location"] = "Slatina, Romania",

                    // --- Audio Visualizer ---
                    ["visualizer_enabled"] = true,
                    ["visualizer_color"] = "#7c3aed",
                    ["visualizer_height"] = 40.0,
                    ["visualizer_opacity"] = 0.6,
                };

                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        var saved = JsonConvert.DeserializeObject<Dictionary<string, object?>>(
                            File.ReadAllText(ConfigPath));
                        if (saved != null)
                            foreach (var kvp in saved)
                                _data[kvp.Key] = kvp.Value;
                    }
                    catch { }
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try { File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
                catch { }
            }
        }

        public static T? Get<T>(string key, T? defaultVal = default)
        {
            lock (_lock)
            {
                if (_data.TryGetValue(key, out var val) && val != null)
                {
                    try { return (T?)Convert.ChangeType(val, typeof(T)); }
                    catch { }
                }
                return defaultVal;
            }
        }

        public static string GetString(string key, string def = "") =>
            Get<string>(key, def) ?? def;

        public static bool GetBool(string key, bool def = false) =>
            Get<bool>(key, def);

        public static int GetInt(string key, int def = 0) =>
            Get<int>(key, def);

        public static double GetDouble(string key, double def = 0) =>
            Get<double>(key, def);

        public static event Action<double>? VolumeChanged;

        public static void Set(string key, object? value)
        {
            lock (_lock) { _data[key] = value; }
            Save();
            
            if (key == "volume" && value is double d)
            {
                VolumeChanged?.Invoke(d);
            }
        }

        public static void ResetAll()
        {
            lock (_lock)
            {
                try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); } catch { }
                Load();
            }
        }
    }
}
