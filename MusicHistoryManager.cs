using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SONA.Services
{
    public static class MusicHistoryManager
    {
        private static List<YTResult> _history = new();
        private static readonly string _savePath = Path.Combine(AppConfig.DataDir, "music_history.json");

        static MusicHistoryManager()
        {
            Load();
        }

        public static void Add(YTResult track)
        {
            var existing = _history.FirstOrDefault(x => x.WebpageUrl == track.WebpageUrl);
            if (existing != null) _history.Remove(existing);
            
            _history.Insert(0, track);
            if (_history.Count > 50) _history = _history.Take(50).ToList();
            Save();
        }

        public static List<YTResult> GetHistory() => _history.ToList();

        private static void Load()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    _history = JsonConvert.DeserializeObject<List<YTResult>>(json) ?? new List<YTResult>();
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
