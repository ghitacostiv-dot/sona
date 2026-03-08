using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SONA.Services
{
    public class TrackedDownload
    {
        public string Title { get; set; } = "";
        public string SourceName { get; set; } = "";
        public string MagnetUrl { get; set; } = "";
        public DateTime StartDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Active"; // Active, Completed, Cancelled
    }

    public static class DownloadManager
    {
        private static List<TrackedDownload> _downloads = new();
        private static readonly string _savePath = Path.Combine(AppConfig.DataDir, "downloads_tracking.json");

        static DownloadManager()
        {
            Load();
        }

        public static void AddDownload(string title, string sourceName, string magnetUrl)
        {
            _downloads.Add(new TrackedDownload { Title = title, SourceName = sourceName, MagnetUrl = magnetUrl });
            Save();
        }

        public static List<TrackedDownload> GetActiveDownloads() => _downloads.Where(d => d.Status == "Active").ToList();

        public static void MarkAsCompleted(string title)
        {
            var dl = _downloads.FirstOrDefault(d => d.Title == title && d.Status == "Active");
            if (dl != null)
            {
                dl.Status = "Completed";
                Save();
            }
        }

        private static void Load()
        {
            if (File.Exists(_savePath))
            {
                try { _downloads = JsonConvert.DeserializeObject<List<TrackedDownload>>(File.ReadAllText(_savePath)) ?? new(); } catch { }
            }
        }

        private static void Save()
        {
            try 
            { 
                Directory.CreateDirectory(AppConfig.DataDir);
                File.WriteAllText(_savePath, JsonConvert.SerializeObject(_downloads)); 
            } catch { }
        }
    }
}
