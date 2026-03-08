using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SONA.Services
{
    public class LocalTrack
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Album { get; set; } = "";
        public string Genre { get; set; } = "";
        public string Duration { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    public static class LocalLibraryScanner
    {
        private static readonly string[] _supportedExtensions = { ".mp3", ".flac", ".m4a", ".wav", ".ogg" };
        private static List<LocalTrack> _cachedTracks = new();
        private static readonly string _cacheFile = Path.Combine(AppConfig.DataDir, "local_library.json");

        public static async Task<List<LocalTrack>> ScanAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return new List<LocalTrack>();

            return await Task.Run(() =>
            {
                var tracks = new List<LocalTrack>();
                try
                {
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                         .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()));

                    foreach (var file in files)
                    {
                        var info = new FileInfo(file);
                        var track = new LocalTrack
                        {
                            FileName = info.Name,
                            FilePath = file,
                            Title = Path.GetFileNameWithoutExtension(file),
                            Artist = "Unknown Artist",
                            Album = "Unknown Album"
                        };

                        // Basic metadata extraction could be added here if we had a library
                        // For now, we use the filename as a fallback
                        tracks.Add(track);
                    }

                    _cachedTracks = tracks;
                    SaveCache();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Scanner] Error scanning {path}: {ex.Message}");
                }
                return tracks;
            });
        }

        public static List<LocalTrack> GetCachedTracks()
        {
            if (_cachedTracks.Count == 0 && File.Exists(_cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(_cacheFile);
                    _cachedTracks = JsonConvert.DeserializeObject<List<LocalTrack>>(json) ?? new List<LocalTrack>();
                }
                catch { }
            }
            return _cachedTracks;
        }

        private static void SaveCache()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_cachedTracks, Formatting.Indented);
                File.WriteAllText(_cacheFile, json);
            }
            catch { }
        }
    }
}
