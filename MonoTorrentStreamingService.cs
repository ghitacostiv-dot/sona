using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.PiecePicking;

namespace SONA.Services
{
    public class TorrentStreamSession
    {
        public string InfoHash { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int FileIndex { get; set; } = 0;
        public TorrentManager Manager { get; set; } = null!;
    }

    /// <summary>
    /// MonoTorrent engine wrapper configured for sequential streaming.
    /// Provides metadata resolution and buffer management.
    /// </summary>
    public static class MonoTorrentStreamingService
    {
        private static ClientEngine? _engine;
        private static readonly ConcurrentDictionary<string, TorrentStreamSession> _sessions = new();
        private static readonly object _lock = new();
        private static string _downloadDir = Path.Combine(Path.GetTempPath(), "SONA_Stream_MT");

        public static void EnsureInitialized()
        {
            if (_engine != null) return;
            Directory.CreateDirectory(_downloadDir);
            var settings = new EngineSettings();
            _engine = new ClientEngine(settings);
        }

        public static async Task<TorrentStreamSession?> StartStreamAsync(string magnetUri, int? preferredFileIndex, int bufferMb, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            EnsureInitialized();
            progress?.Report("Starting sequential torrent streaming…");

            MagnetLink magnet;
            try { magnet = MagnetLink.Parse(magnetUri); }
            catch { progress?.Report("Invalid magnet link"); return null; }

            var manager = await _engine!.AddAsync(magnet, _downloadDir);
            await manager.StartAsync();

            // Wait for metadata to get actual file names
            progress?.Report("Fetching metadata…");
            int metaWaits = 0;
            while (!manager.HasMetadata && metaWaits < 40 && !ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);
                metaWaits++;
            }
            if (!manager.HasMetadata)
            {
                progress?.Report("Metadata timeout");
                return null;
            }

            string infoHash = "";
            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(magnetUri, "xt=urn:btih:([a-zA-Z0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) infoHash = m.Groups[1].Value.ToLowerInvariant();
            }
            catch { }
            string display = manager.Torrent?.Name ?? "Unknown";

            // Pick target file
            var files = manager.Files;
            int targetIndex = 0;
            if (preferredFileIndex.HasValue && preferredFileIndex.Value >= 0 && preferredFileIndex.Value < files.Count)
                targetIndex = preferredFileIndex.Value;
            else
            {
                var ordered = files
                    .Select((f, idx) => new { f, idx })
                    .Where(x => IsVideoFile(x.f.Path))
                    .OrderByDescending(x => x.f.Length)
                    .ToList();
                if (ordered.Count > 0) targetIndex = ordered[0].idx;
                else targetIndex = 0;
            }

            var targetFile = files[targetIndex];
            string path = Path.Combine(_downloadDir, targetFile.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            progress?.Report($"Buffering {bufferMb} MB…");
            long bufferBytes = (long)Math.Clamp(bufferMb, 5, 50) * 1024 * 1024;
            int bufferWaits = 0;
            while (targetFile.BytesDownloaded() < bufferBytes && bufferWaits < 600 && !ct.IsCancellationRequested)
            {
                progress?.Report($"Buffered {targetFile.BytesDownloaded() / 1024 / 1024:0.0} MB / {bufferMb} MB");
                await Task.Delay(1000, ct);
                bufferWaits++;
            }

            var session = new TorrentStreamSession
            {
                InfoHash = infoHash,
                DisplayName = display,
                FilePath = path,
                FileIndex = targetIndex,
                Manager = manager
            };
            _sessions[infoHash] = session;
            return session;
        }

        public static TorrentStreamSession? GetSession(string infoHash)
            => _sessions.TryGetValue(infoHash.ToLowerInvariant(), out var s) ? s : null;

        public static long GetDownloadedBytes(TorrentStreamSession session)
            => session.Manager.Files[session.FileIndex].BytesDownloaded();

        private static bool IsVideoFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov" || ext == ".m4v";
        }
    }
}
