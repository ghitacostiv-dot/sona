using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SONA;

namespace SONA.Services
{
    public static class TorrentStreamer
    {
        public static async Task<string?> StreamMagnetAsync(string magnetUrl, string title, Action<string> onStatusUpdate, string? fileIndex = null)
        {
            onStatusUpdate("Initializing torrent engine...");
            string gid = await Aria2Engine.AddDownloadAsync(magnetUrl, title, "Streaming", null, true, fileIndex);
            
            if (string.IsNullOrEmpty(gid))
            {
                onStatusUpdate("Failed to initialize engine.");
                return null;
            }

            onStatusUpdate("Discovering metadata and peers...");
            
            // Wait for file metadata to be available (files list populated)
            List<string> files = new();
            int attempts = 0;
            while (files.Count == 0 && attempts < 45)
            {
                files = await Aria2Engine.GetFilesAsync(gid);
                if (files.Count == 0) await Task.Delay(2000);
                attempts++;
            }

            if (files.Count == 0)
            {
                onStatusUpdate("Metadata timeout. Try a different source.");
                return null;
            }

            // Find the target file
            string? videoFile = null;
            if (!string.IsNullOrEmpty(fileIndex) && int.TryParse(fileIndex, out int idx) && idx > 0 && idx <= files.Count)
            {
                videoFile = files[idx - 1];
            }
            
            if (string.IsNullOrEmpty(videoFile))
            {
                videoFile = files
                    .Where(f => IsVideoFile(f))
                    .OrderByDescending(f => {
                        try { return File.Exists(f) ? new FileInfo(f).Length : 0; } catch { return 0; }
                    })
                    .FirstOrDefault();
            }

            if (string.IsNullOrEmpty(videoFile))
            {
                onStatusUpdate("No playable video found in source.");
                return null;
            }

            int bufferMb = Math.Clamp(AppConfig.GetInt("torrent_stream_buffer_mb", 10), 5, 50);
            onStatusUpdate($"Preparing buffer ({bufferMb} MB, step-by-step like a browser)...");
            
            long bufferBytes = (long)bufferMb * 1024 * 1024;
            int bufferAttempts = 0;
            while (bufferAttempts < 300) // ~10 mins max
            {
                long completed = await Aria2Engine.GetCompletedLengthAsync(gid);
                double mb = completed / 1024.0 / 1024.0;
                
                onStatusUpdate($"Buffering: {mb:0.0} MB / {bufferMb} MB");
                
                if (completed >= bufferBytes) break; 
                
                await Task.Delay(2000);
                bufferAttempts++;
            }

            onStatusUpdate("Cinema engine ready! Starting playback...");
            await Task.Delay(1000);
            return videoFile;
        }

        private static bool IsVideoFile(string path)
        {
            var ext = Path.GetExtension(path).ToLower();
            return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov" || ext == ".m4v";
        }
    }
}
