using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SONA.Services
{
    /// <summary>
    /// Lightweight HTTP server which exposes:
    ///   GET /api/stream/{infoHash}/{fileIndex}
    /// Streams the selected file progressively with basic Range support.
    /// </summary>
    public static class TorrentHttpServer
    {
        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;
        public static string BaseUrl => "http://localhost:3005";
        public static bool IsRunning => _listener != null && _listener.IsListening;

        public static Task StartAsync()
        {
            if (IsRunning) return Task.CompletedTask;
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public static void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
        }

        private static async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext? ctx = null;
                try { ctx = await _listener.GetContextAsync(); }
                catch { if (ct.IsCancellationRequested) break; continue; }
                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var res = ctx.Response;
                res.Headers["Access-Control-Allow-Origin"] = "*";

                if (req.HttpMethod == "GET" && req.Url != null && req.Url.AbsolutePath.StartsWith("/api/stream/"))
                {
                    var parts = req.Url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string infoHash = parts[2].ToLowerInvariant();
                        int fileIndex = 0;
                        if (parts.Length >= 4) int.TryParse(parts[3], out fileIndex);
                        var session = MonoTorrentStreamingService.GetSession(infoHash);
                        if (session == null || string.IsNullOrEmpty(session.FilePath) || !File.Exists(session.FilePath))
                        {
                            await WriteTextAsync(res, 404, "Stream not ready");
                            return;
                        }

                        string path = session.FilePath;
                        var fi = new FileInfo(path);
                        long totalLength = fi.Length; // May grow during download
                        string mime = GetMimeFromExt(Path.GetExtension(path));
                        res.ContentType = mime;
                        res.SendChunked = false;

                        long start = 0;
                        long end = Math.Max(0, totalLength - 1);

                        if (!string.IsNullOrEmpty(req.Headers["Range"]))
                        {
                            var rng = req.Headers["Range"]; // bytes=start-end
                            if (rng != null && rng.StartsWith("bytes="))
                            {
                                var segs = rng.Substring(6).Split('-', 2);
                                long.TryParse(segs[0], out start);
                                if (segs.Length > 1 && long.TryParse(segs[1], out var parsedEnd)) end = parsedEnd;
                                if (end <= 0 || end >= totalLength) end = totalLength - 1;
                                res.StatusCode = (int)HttpStatusCode.PartialContent;
                                res.Headers["Content-Range"] = $"bytes {start}-{end}/{totalLength}";
                            }
                        }
                        else
                        {
                            res.StatusCode = (int)HttpStatusCode.OK;
                        }

                        long remaining = (end - start + 1);
                        res.ContentLength64 = remaining;

                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        fs.Position = start;
                        byte[] buffer = new byte[64 * 1024];
                        while (remaining > 0)
                        {
                            // Wait for download if we reached current downloaded bytes
                            long downloaded = MonoTorrentStreamingService.GetDownloadedBytes(session);
                            long writable = Math.Min(remaining, downloaded - fs.Position);
                            if (writable <= 0)
                            {
                                await Task.Delay(300);
                                continue;
                            }
                            int toRead = (int)Math.Min(buffer.Length, writable);
                            int read = await fs.ReadAsync(buffer, 0, toRead);
                            if (read <= 0) break;
                            await res.OutputStream.WriteAsync(buffer, 0, read);
                            remaining -= read;
                        }
                        try { res.OutputStream.Flush(); } catch { }
                        res.OutputStream.Close();
                        return;
                    }
                }

                await WriteTextAsync(ctx.Response, 404, "Not Found");
            }
            catch
            {
                try { ctx.Response.Abort(); } catch { }
            }
        }

        private static async Task WriteTextAsync(HttpListenerResponse res, int code, string text)
        {
            res.StatusCode = code;
            res.ContentType = "text/plain";
            var bytes = Encoding.UTF8.GetBytes(text);
            res.ContentLength64 = bytes.Length;
            await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            res.OutputStream.Close();
        }

        private static string GetMimeFromExt(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".mp4": return "video/mp4";
                case ".mkv": return "video/x-matroska";
                case ".avi": return "video/x-msvideo";
                case ".mov": return "video/quicktime";
                case ".m4v": return "video/x-m4v";
                default: return "application/octet-stream";
            }
        }
    }
}
