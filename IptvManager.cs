using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SONA.Services
{
    public class TvChannelInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Country { get; set; } = "";
        public string LogoUrl { get; set; } = "";
        public string StreamUrl { get; set; } = "";
        public EpgProgram? CurrentProgram { get; set; }
    }

    public class EpgProgram
    {
        public string Title { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime Stop { get; set; }
        public double ProgressPercent => (DateTime.Now - Start).TotalSeconds / (Stop - Start).TotalSeconds * 100;
    }

    public static class IptvManager
    {
        // ── Primary index (all channels by country group) ──────────────────
        private const string IndexM3u = "https://iptv-org.github.io/iptv/index.m3u";

        // ── Category-specific playlists (iptv-org) ─────────────────────────
        private static readonly (string Category, string Url)[] CategoryPlaylists = new[]
        {
            ("📰 News",          "https://iptv-org.github.io/iptv/categories/news.m3u"),
            ("⚽ Sports",        "https://iptv-org.github.io/iptv/categories/sports.m3u"),
            ("🎬 Movies",        "https://iptv-org.github.io/iptv/categories/movies.m3u"),
            ("🎵 Music",         "https://iptv-org.github.io/iptv/categories/music.m3u"),
            ("👶 Kids",          "https://iptv-org.github.io/iptv/categories/kids.m3u"),
            ("📺 Entertainment", "https://iptv-org.github.io/iptv/categories/entertainment.m3u"),
            ("🌍 Documentary",   "https://iptv-org.github.io/iptv/categories/documentary.m3u"),
            ("🛡 Education",     "https://iptv-org.github.io/iptv/categories/education.m3u"),
            ("🏛 Legislative",   "https://iptv-org.github.io/iptv/categories/legislative.m3u"),
            ("🌐 General",       "https://iptv-org.github.io/iptv/categories/general.m3u"),
        };

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

        // ── Category display order ─────────────────────────────────────────
        public static readonly string[] CategoryOrder = new[]
        {
            "📰 News", "⚽ Sports", "🎬 Movies", "🎵 Music",
            "👶 Kids", "📺 Entertainment", "🌍 Documentary",
            "🛡 Education", "🏛 Legislative", "🌐 General", "📡 Other"
        };

        /// <summary>
        /// Loads channels from all category playlists in parallel, then deduplicates.
        /// </summary>
        public static async Task<List<TvChannelInfo>> GetChannelsAsync()
        {
            var tasks = CategoryPlaylists.Select(p => LoadFromUrlAsync(p.Url, p.Category));
            var results = await Task.WhenAll(tasks);

            var all = results.SelectMany(r => r).ToList();

            // Deduplicate by stream URL (keep first seen)
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<TvChannelInfo>();
            foreach (var ch in all)
            {
                if (seen.Add(ch.StreamUrl))
                    deduped.Add(ch);
            }

            return deduped.Where(c => !string.IsNullOrEmpty(c.Name) && !string.IsNullOrEmpty(c.StreamUrl)).ToList();
        }

        /// <summary>
        /// Loads the main country-indexed playlist (larger but less categorized). Used as fallback.
        /// </summary>
        public static async Task<List<TvChannelInfo>> GetChannelsByCountryAsync()
        {
            return await LoadFromUrlAsync(IndexM3u, "");
        }

        private static async Task<List<TvChannelInfo>> LoadFromUrlAsync(string url, string defaultCategory)
        {
            var channels = new List<TvChannelInfo>();
            try
            {
                var m3uData = await _http.GetStringAsync(url);
                channels = ParseM3u(m3uData, defaultCategory);
            }
            catch { }
            return channels;
        }

        private static List<TvChannelInfo> ParseM3u(string m3uData, string defaultCategory)
        {
            var channels = new List<TvChannelInfo>();
            var lines = m3uData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            TvChannelInfo? current = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r').Trim();
                if (line.StartsWith("#EXTINF:"))
                {
                    current = new TvChannelInfo();
                    var info = line.Substring(8);

                    current.Id = ExtractAttr(info, "tvg-id");
                    current.LogoUrl = ExtractAttr(info, "tvg-logo");
                    current.Country = ExtractAttr(info, "tvg-country");

                    var grp = ExtractAttr(info, "group-title");
                    current.Category = MapCategory(grp, defaultCategory);

                    var commaIdx = info.LastIndexOf(',');
                    if (commaIdx >= 0 && commaIdx < info.Length - 1)
                        current.Name = info.Substring(commaIdx + 1).Trim();
                }
                else if (!line.StartsWith("#") && current != null && line.Length > 5)
                {
                    current.StreamUrl = line;
                    if (string.IsNullOrEmpty(current.Category))
                        current.Category = "📡 Other";
                    channels.Add(current);
                    current = null;
                }
            }
            return channels;
        }

        private static string ExtractAttr(string info, string attr)
        {
            var key = $"{attr}=\"";
            var idx = info.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            idx += key.Length;
            var end = info.IndexOf('"', idx);
            if (end < 0) return "";
            return info.Substring(idx, end - idx).Trim();
        }

        /// Maps a raw group-title to one of our canonical categories.
        private static string MapCategory(string raw, string defaultCategory)
        {
            if (!string.IsNullOrEmpty(defaultCategory)) return defaultCategory;
            if (string.IsNullOrEmpty(raw)) return "📡 Other";

            var r = raw.ToLowerInvariant();
            if (r.Contains("news") || r.Contains("stire") || r.Contains("nachrichten")) return "📰 News";
            if (r.Contains("sport") || r.Contains("football") || r.Contains("soccer")) return "⚽ Sports";
            if (r.Contains("movie") || r.Contains("film") || r.Contains("cinema")) return "🎬 Movies";
            if (r.Contains("music") || r.Contains("muzic") || r.Contains("muzik")) return "🎵 Music";
            if (r.Contains("kid") || r.Contains("child") || r.Contains("cartoon") || r.Contains("junior")) return "👶 Kids";
            if (r.Contains("entertain") || r.Contains("general")) return "📺 Entertainment";
            if (r.Contains("doc") || r.Contains("nature") || r.Contains("history")) return "🌍 Documentary";
            if (r.Contains("edu") || r.Contains("learn") || r.Contains("school")) return "🛡 Education";
            if (r.Contains("legisl") || r.Contains("govern") || r.Contains("politic")) return "🏛 Legislative";
            return "🌐 General";
        }

        // ── EPG Loader (unchanged) ─────────────────────────────────────────
        private const string EpgUrl = "https://raw.githubusercontent.com/iptv-org/epg/master/guides/ro/guide.xml";

        public static async Task LoadEpgAsync(List<TvChannelInfo> channels)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var xmlData = await client.GetStringAsync(EpgUrl);
                var doc = System.Xml.Linq.XDocument.Parse(xmlData);
                var now = DateTime.UtcNow;
                var dict = new Dictionary<string, EpgProgram>();

                foreach (var p in doc.Root?.Elements("programme") ?? Enumerable.Empty<System.Xml.Linq.XElement>())
                {
                    var startStr = p.Attribute("start")?.Value;
                    var stopStr = p.Attribute("stop")?.Value;
                    var channelId = p.Attribute("channel")?.Value;
                    if (startStr != null && stopStr != null && channelId != null)
                    {
                        var start = ParseXmltvDate(startStr);
                        var stop = ParseXmltvDate(stopStr);
                        if (now >= start && now <= stop)
                        {
                            var title = p.Element("title")?.Value ?? "Unknown Program";
                            dict[channelId] = new EpgProgram { Title = title, Start = start, Stop = stop };
                        }
                    }
                }
                foreach (var c in channels)
                    if (!string.IsNullOrEmpty(c.Id) && dict.TryGetValue(c.Id, out var prog))
                    {
                        prog.Start = prog.Start.ToLocalTime();
                        prog.Stop = prog.Stop.ToLocalTime();
                        c.CurrentProgram = prog;
                    }
            }
            catch { }
        }

        private static DateTime ParseXmltvDate(string s)
        {
            try
            {
                if (s.Length >= 14)
                {
                    var dt = new DateTime(int.Parse(s[..4]), int.Parse(s.Substring(4, 2)),
                        int.Parse(s.Substring(6, 2)), int.Parse(s.Substring(8, 2)),
                        int.Parse(s.Substring(10, 2)), int.Parse(s.Substring(12, 2)), DateTimeKind.Utc);
                    if (s.Length > 15)
                    {
                        var sign = s[15] == '+' ? 1 : -1;
                        dt -= new TimeSpan(int.Parse(s.Substring(16, 2)), int.Parse(s.Substring(18, 2)), 0) * sign;
                    }
                    return dt;
                }
            }
            catch { }
            return DateTime.UtcNow;
        }
    }
}
