using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SONA.Services
{
    public class WatchEntry
    {
        public string ImdbId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Type { get; set; } = "movie"; // movie or series
        public string PosterUrl { get; set; } = "";
        public DateTime LastWatched { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public double Progress { get; set; } // 0 to 1
    }

    public static class WatchContinuityManager
    {
        private static List<WatchEntry> _history = new();
        private static readonly string _savePath = Path.Combine(AppConfig.DataDir, "watch_history.json");

        static WatchContinuityManager()
        {
            Load();
        }

        public static void AddOrUpdate(WatchEntry entry)
        {
            var existing = _history.FirstOrDefault(x => x.ImdbId == entry.ImdbId);
            if (existing != null)
            {
                existing.LastWatched = DateTime.Now;
                existing.Progress = entry.Progress;
                existing.Season = entry.Season;
                existing.Episode = entry.Episode;
            }
            else
            {
                entry.LastWatched = DateTime.Now;
                _history.Insert(0, entry);
            }

            // Keep only last 20 items
            if (_history.Count > 20) _history = _history.Take(20).ToList();
            Save();
        }

        public static List<WatchEntry> GetHistory() => _history.OrderByDescending(x => x.LastWatched).ToList();

        private static void Load()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    _history = JsonConvert.DeserializeObject<List<WatchEntry>>(json) ?? new List<WatchEntry>();
                }
            } catch { }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(AppConfig.DataDir);
                string json = JsonConvert.SerializeObject(_history, Formatting.Indented);
                File.WriteAllText(_savePath, json);
            } catch { }
        }
    }
}
