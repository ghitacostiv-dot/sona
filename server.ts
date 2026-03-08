/**
 * Nexus Torrent Streaming Server
 * Runs on port 3004, proxied via Vite at /api/*
 *
 * Converts BitTorrent infoHash → real-time HTTP video stream
 * with Range header support (so the browser <video> can seek).
 */

import express from "express";
import type { Request, Response } from "express";
// @ts-ignore
import WebTorrent from "webtorrent";
import TorrentSearchApi from "torrent-search-api";
import { pipeline } from "stream";
import { promisify } from "util";
import ffmpeg from "fluent-ffmpeg";
import ffmpegInstaller from "@ffmpeg-installer/ffmpeg";
import ffprobeInstaller from "@ffprobe-installer/ffprobe";
import mime from "mime-types";
import os from "os";
import path from "path";

ffmpeg.setFfmpegPath(ffmpegInstaller.path);
ffmpeg.setFfprobePath(ffprobeInstaller.path);

// Enable all available public tracker providers for comprehensive searching (1000+ sources capacity)
const providers = TorrentSearchApi.getProviders();
for (const provider of providers) {
    try {
        TorrentSearchApi.enableProvider(provider.name);
    } catch (e) { }
}

const app = express();
const PORT = 3004;

// CORS – allow the Vite dev server at :3003
app.use((_req, res, next) => {
    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Headers", "Range, Content-Type, Accept");
    res.setHeader("Access-Control-Expose-Headers", "Content-Range, Accept-Ranges, Content-Length");
    next();
});

// Debug all incoming requests
app.use((req, _res, next) => {
    console.log(`[API] ${req.method} ${req.url}`);
    next();
});


const TORRENT_DIR = path.join(os.tmpdir(), "nexus-streaming");

const client = new WebTorrent({
    maxConns: 500, // Increased for better peer discovery in small-swarm scenarios
    tracker: true,
    dht: true,
    lsd: true, // Local Service Discovery
    // Use stable root path for all downloads
    path: TORRENT_DIR,
    destroyStoreOnDestroy: false // Keep pieces if app restarts
});

// Watch client-level events
client.on('error', (err: any) => console.error('[client] Fatal webtorrent error:', err.message));
client.on('warning', (warn: any) => console.warn('[client] Webtorrent warning:', warn.message));


// Map of infoHash → webtorrent Torrent object (cache so we don't re-add)
const torrents: Map<string, any> = new Map();

// Utility: get or add a torrent, returns the torrent object once ready
function getOrAddTorrent(uriOrHash: string): Promise<any> {
    let infoHash = uriOrHash.toLowerCase();
    let finalMagnet = "";
    let trackers: string[] = []; // Define trackers here for broader scope

    // If it's a full magnet link, extract the hash for our internal map
    if (uriOrHash.startsWith("magnet:?")) {
        const match = uriOrHash.match(/xt=urn:btih:([a-fA-F0-9]+)/i);
        if (match) infoHash = match[1].toLowerCase();
        finalMagnet = uriOrHash;
        // Extract trackers from magnet URI if present, otherwise rely on DHT/LSD
        const trackerMatches = uriOrHash.match(/tr=([^&]+)/g);
        if (trackerMatches) {
            trackers = trackerMatches.map(m => decodeURIComponent(m.substring(3)));
        }
    } else {
        // Extensive list of trackers for maximum peer discovery
        trackers = [
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://tracker.openbittorrent.com:6969/announce",
            "udp://9.rarbg.to:2920/announce",
            "udp://exodus.desync.com:6969/announce",
            "udp://tracker.torrent.eu.org:451/announce",
            "udp://tracker.moeking.me:6969/announce",
            "udp://tracker.cyberia.is:6969/announce",
            "udp://open.stealth.si:80/announce",
            "udp://tracker.zerobytes.xyz:1337/announce",
            "udp://tracker.tiny-vps.com:6969/announce",
            "udp://p4p.arenabg.com:1337/announce",
            "udp://retracker.lanta-net.ru:2710/announce",
            "udp://ipv4.tracker.harry.lu:80/announce",
            "udp://valakas.eu.org:6969/announce",
            "wss://tracker.openwebtorrent.com",
            "wss://tracker.btorrent.xyz"
        ];
        const trs = trackers.map(tr => `&tr=${encodeURIComponent(tr)}`).join('');
        finalMagnet = `magnet:?xt=urn:btih:${infoHash}${trs}`;
    }

    return new Promise((resolve, reject) => {
        const existing = torrents.get(infoHash);
        if (existing) {
            if (existing.ready) return resolve(existing);
            existing.once("ready", () => resolve(existing));
            existing.once("error", reject);
            return;
        }

        console.log(`[torrent] NEW: adding magnet for ${infoHash}`);

        try {
            const t = client.add(finalMagnet, {
                path: path.join(TORRENT_DIR, infoHash), // Use infoHash for sub-directory
                announce: trackers, // Pass explicit trackers
                destroyStoreOnDestroy: false
            });

            t.infoHash = infoHash; // Ensure our reference is lowercase
            torrents.set(infoHash, t);

            console.log(`[torrent] Instance created for ${infoHash}. Waiting for metadata...`);

            // Announce immediately - don't wait for metadata to START seeking peers
            // @ts-ignore
            const onReady = () => {
                console.log(`[torrent] READY: ${t.name} (Peers: ${t.numPeers}, Hash: ${infoHash})`);

                // DESELECT ALL first - prevent background bandwidth waste
                t.deselect(0, (t.pieces ? t.pieces.length - 1 : 0), false);

                const movieFile = t.files.reduce((a: any, b: any) => (a.length > b.length ? a : b));
                if (movieFile) {
                    console.log(`[torrent] Target file: ${movieFile.name} (${(movieFile.length / 1024 / 1024).toFixed(1)} MB)`);

                    // Prioritize the START of the file aggressively (Sequential Step 1)
                    movieFile.select();
                    // We select only the first 10MB to start with
                    const pieceLen = t.pieceLength || 1024 * 1024;
                    const startPieces = Math.ceil((10 * 1024 * 1024) / pieceLen);
                    t.critical(0, Math.min(startPieces, (t.pieces ? t.pieces.length - 1 : 0)));
                    t.select(0, Math.min(startPieces * 2, (t.pieces ? t.pieces.length - 1 : 0)), 10);
                }

                // Periodic progress log
                const logInterval = setInterval(() => {
                    if (t.destroyed) { clearInterval(logInterval); return; }
                    if (t.downloadSpeed > 0) {
                        console.log(`[torrent] ${t.name} | Speed: ${(t.downloadSpeed / 1024 / 1024).toFixed(2)} MB/s | Peers: ${t.numPeers} | Progress: ${(t.progress * 100).toFixed(1)}%`);
                    }
                }, 5000);

                resolve(t);
            };

            t.once("ready", onReady);
            t.once("error", (err: Error) => {
                console.error(`[torrent] ERROR for ${infoHash}:`, err.message);
                torrents.delete(infoHash);
                reject(err);
            });

            // Extended timeout to 45s - some DHT lookups take time
            setTimeout(() => {
                if (!t.ready) {
                    console.warn(`[torrent] TIMEOUT getting metadata for ${infoHash}. Destroying...`);
                    torrents.delete(infoHash);
                    t.destroy();
                    reject(new Error("Torrent metadata timeout - no peers found. Try a different source."));
                }
            }, 45000);
        } catch (e: any) {
            console.error(`[torrent] EXCEPTION in client.add:`, e.message);
            reject(e);
        }
    });
}

// Health check
app.get("/api/health", (_req, res) => {
    res.json({ ok: true, torrents: torrents.size });
});

// List active torrents
app.get("/api/torrents", (_req, res) => {
    const list = Array.from(torrents.entries()).map(([hash, t]) => ({
        infoHash: hash,
        name: t.name,
        progress: t.progress,
        downloadSpeed: t.downloadSpeed,
        numPeers: t.numPeers ?? t.swarm?.numPeers ?? 0,
        ready: t.ready,
        files: t.files?.map((f: any) => ({ name: f.name, length: f.length, index: t.files.indexOf(f) })),
    }));
    res.json(list);
});

// Mock movies for home screen
app.get("/api/movies", (_req, res) => {
    res.json([
        { id: 'tt1375666', title: 'Inception', type: 'movie', poster: 'https://image.tmdb.org/t/p/w500/oYuS0VV65rP7Bf2S6A0HwzB99Bv.jpg', year: '2010', rating: '8.8' },
        { id: 'tt0133093', title: 'The Matrix', type: 'movie', poster: 'https://image.tmdb.org/t/p/w500/f89U3Y9S9SdyzqH9GvY870S9biC.jpg', year: '1999', rating: '8.7' },
        { id: 'tt0816692', title: 'Interstellar', type: 'movie', poster: 'https://image.tmdb.org/t/p/w500/gEU2QniE6E77NI6lCU6MxlSaba7.jpg', year: '2014', rating: '8.6' },
        { id: 'tt0944947', title: 'Game of Thrones', type: 'series', poster: 'https://image.tmdb.org/t/p/w500/7WsyChvRStvT0tO2EOvNi6P90Ym.jpg', year: '2011', rating: '9.2' },
        { id: 'tt0903747', title: 'Breaking Bad', type: 'series', poster: 'https://image.tmdb.org/t/p/w500/ggm8bbub6o6S1M0Y7ZpbiC1v6ia.jpg', year: '2008', rating: '9.5' },
        { id: 'tt4154756', title: 'Avengers: Infinity War', type: 'movie', poster: 'https://image.tmdb.org/t/p/w500/7WsyChvRStvT0tO2EOvNi6P90Ym.jpg', year: '2018', rating: '8.4' },
        { id: 'tt15348164', title: 'Oppenheimer', type: 'movie', poster: 'https://image.tmdb.org/t/p/w500/8GxvA9zDZp0GmwvRhEPca27UCL0.jpg', year: '2023', rating: '8.4' }
    ]);
});

// Scraper endpoint (proxies to integrated TorrentSearchApi)
app.get("/api/scrape/:query", async (req, res) => {
    const { query } = req.params;
    const { type } = req.query;
    console.log(`[scraper] Searching for: ${query} (type: ${type})`);
    try {
        const results = await TorrentSearchApi.search(query, type === 'movie' ? 'Movies' : 'All', 20);
        // Map to Stremio-like streams for the frontend
        const streams = results.map((r: any) => ({
            name: r.provider,
            title: `${r.title}\nSize: ${r.size}\nPeers: ${r.peers || r.seeders || 0}`,
            infoHash: (r.infoHash || r.hash || "").toLowerCase(),
            magnet: r.magnet,
            url: r.magnet || (r.infoHash ? `magnet:?xt=urn:btih:${r.infoHash}` : undefined)
        })).filter(s => s.infoHash || s.url);
        res.json(streams);
    } catch (e: any) {
        console.error(`[scraper] Error searching:`, e.message);
        res.status(500).json({ error: e.message });
    }
});

// Network diagnostics endpoint
app.get("/api/test-network", async (_req, res) => {
    const results: any = { time: new Date().toISOString() };
    try {
        const start = Date.now();
        const resp = await fetch("https://google.com", { signal: AbortSignal.timeout(5000) });
        results.google = { ok: resp.ok, status: resp.status, time: Date.now() - start };
    } catch (e: any) { results.google = { ok: false, error: e.message }; }

    try {
        const start = Date.now();
        const resp = await fetch("https://torrentio.strem.fun/manifest.json", { signal: AbortSignal.timeout(5000) });
        results.torrentio = { ok: resp.ok, status: resp.status, time: Date.now() - start };
    } catch (e: any) { results.torrentio = { ok: false, error: e.message }; }

    res.json(results);
});

// Preload a torrent (start fetching metadata and connecting to peers) without waiting
app.get("/api/preload/:infoHash", (req, res) => {
    const infoHash = req.params.infoHash.toLowerCase();
    try {
        const existing = torrents.get(infoHash);
        if (existing) {
            return res.json({ ok: true, status: existing.ready ? "ready" : "loading", infoHash: existing.infoHash });
        }

        // Use unified getter to ensure same trackers and disk storage are used
        getOrAddTorrent(infoHash)
            .then(() => { /* preload successful */ })
            .catch(err => console.error(`[preload] Failed for ${infoHash}:`, err.message));

        return res.json({ ok: true, status: "loading", infoHash });
    } catch (e: any) {
        return res.status(500).json({ ok: false, error: e?.message || "Failed to preload" });
    }
});

// Add by full magnet URI (query param ?uri=...)
app.get("/api/add-magnet", (req, res) => {
    const magnetUri = req.query.uri as string;
    if (!magnetUri) return res.status(400).json({ error: "Missing URI" });

    // Extract infoHash to handle case-sensitivity across backend/frontend
    let infoHash = "";
    try {
        const m = magnetUri.match(/xt=urn:btih:([a-fA-F0-9]+)/i);
        if (m) infoHash = m[1].toLowerCase();
    } catch { }

    getOrAddTorrent(magnetUri)
        .then(t => res.json({ ok: true, infoHash: t.infoHash, name: t.name, status: "ready" }))
        .catch(err => res.status(500).json({ ok: false, error: err.message }));
});

// Remove / stop a torrent and delete the downloaded files
app.delete("/api/torrents/:infoHash", (req, res) => {
    const { infoHash } = req.params;
    const t = torrents.get(infoHash);
    if (!t) return res.status(404).json({ error: "Not found" });

    t.destroy({ destroyStore: true }, () => {
        torrents.delete(infoHash);
        res.json({ ok: true });
    });
});

/**
 * Proxy: Raw streaming with custom headers
 * GET /api/proxy/raw?u=<encoded-url>&h=<base64-json-headers>
 * Passes through Range requests and upstream response headers.
 */
app.get("/api/proxy/raw", async (req: Request, res: Response) => {
    try {
        const u = (req.query.u as string) || "";
        const h = (req.query.h as string) || "";
        if (!u) return res.status(400).json({ error: "Missing u" });

        let headers: Record<string, string> = {};
        try { headers = h ? JSON.parse(Buffer.from(h, "base64").toString("utf-8")) : {}; } catch { }
        // Sensible defaults to improve compatibility
        const ua = headers["User-Agent"] || headers["user-agent"] || "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";
        const accept = headers["Accept"] || headers["accept"] || "application/vnd.apple.mpegurl,application/x-mpegURL,video/*;q=0.9,*/*;q=0.8";
        const acceptLang = headers["Accept-Language"] || headers["accept-language"] || "en-US,en;q=0.9";
        headers["User-Agent"] = ua;
        headers["Accept"] = accept;
        headers["Accept-Language"] = acceptLang;
        if ((headers["Referer"] || headers["referer"]) && !headers["Origin"]) {
            try { headers["Origin"] = new URL(headers["Referer"] || headers["referer"] as string).origin; } catch { }
        }
        // Forward Range header if present
        if (req.headers.range) headers["Range"] = req.headers.range as string;

        const upstream = await fetch(u, { headers, signal: AbortSignal.timeout(20000) });
        res.status(upstream.status);

        // Pass through essential headers
        const passHeaders = ["content-type", "content-length", "accept-ranges", "content-range"];
        for (const [key, value] of upstream.headers.entries()) {
            if (passHeaders.includes(key.toLowerCase())) res.setHeader(key, value);
        }
        res.setHeader("Access-Control-Allow-Origin", "*");
        res.setHeader("Access-Control-Expose-Headers", "Content-Range, Accept-Ranges, Content-Length");

        const body = upstream.body;
        if (!body) return res.end();
        const pipe = promisify(pipeline);
        await pipe(body as any, res as any);
    } catch (err: any) {
        res.status(502).json({ error: "Proxy raw failed", detail: err?.message || "Unknown error" });
    }
});

/**
 * Proxy: HLS manifest rewriting
 * GET /api/proxy/manifest?u=<encoded-url>&h=<base64-json-headers>
 * Rewrites segment and nested playlist URLs to route via /api/proxy/raw
 */
app.get("/api/proxy/manifest", async (req: Request, res: Response) => {
    try {
        const u = (req.query.u as string) || "";
        const h = (req.query.h as string) || "";
        if (!u) return res.status(400).json({ error: "Missing u" });

        let headers: Record<string, string> = {};
        try { headers = h ? JSON.parse(Buffer.from(h, "base64").toString("utf-8")) : {}; } catch { }

        const upstream = await fetch(u, { headers, signal: AbortSignal.timeout(20000) });
        if (!upstream.ok) {
            return res.status(upstream.status).json({ error: `Upstream ${upstream.status}` });
        }
        const text = await upstream.text();
        const base = new URL(u);
        const baseDir = new URL("./", base).toString();
        const headerParam = Buffer.from(JSON.stringify(headers)).toString("base64");

        const rewritten = text.split("\n").map(line => {
            const trimmed = line.trim();
            // Rewrite KEY and MAP URIs inside tags
            if (trimmed.startsWith("#EXT-X-KEY")) {
                // Replace URI=".."
                return trimmed.replace(/URI="([^"]+)"/i, (_m, g1) => {
                    const isAbs = /^https?:\/\//i.test(g1);
                    const target = isAbs ? new URL(g1).toString() : new URL(g1, baseDir).toString();
                    const prox = `/api/proxy/raw?u=${encodeURIComponent(target)}&h=${encodeURIComponent(headerParam)}`;
                    return `URI="${prox}"`;
                });
            }
            if (trimmed.startsWith("#EXT-X-MAP")) {
                return trimmed.replace(/URI="([^"]+)"/i, (_m, g1) => {
                    const isAbs = /^https?:\/\//i.test(g1);
                    const target = isAbs ? new URL(g1).toString() : new URL(g1, baseDir).toString();
                    const prox = `/api/proxy/raw?u=${encodeURIComponent(target)}&h=${encodeURIComponent(headerParam)}`;
                    return `URI="${prox}"`;
                });
            }
            if (!trimmed || trimmed.startsWith("#")) return line;
            try {
                const isAbsolute = /^https?:\/\//i.test(trimmed);
                const target = isAbsolute ? new URL(trimmed).toString() : new URL(trimmed, baseDir).toString();
                // Route via raw proxy
                return `/api/proxy/raw?u=${encodeURIComponent(target)}&h=${encodeURIComponent(headerParam)}`;
            } catch {
                return line;
            }
        }).join("\n");

        res.setHeader("Content-Type", upstream.headers.get("content-type") || "application/vnd.apple.mpegurl");
        res.setHeader("Access-Control-Allow-Origin", "*");
        res.send(rewritten);
    } catch (err: any) {
        res.status(502).json({ error: "Proxy manifest failed", detail: err?.message || "Unknown error" });
    }
});

/**
 * Native IPTV M3U parser
 * Fetches the global IPTV playlist and converts it into MetaPreview objects
 */
app.get("/api/iptv", async (_req, res) => {
    try {
        const response = await fetch("https://iptv-org.github.io/iptv/index.country.m3u", { signal: AbortSignal.timeout(15000) });
        if (!response.ok) throw new Error("Failed to fetch IPTV m3u");
        const m3uData = await response.text();

        const lines = m3uData.split('\n');
        const channels: any[] = [];
        let currentChannel: any = null;

        for (const line of lines) {
            const trim = line.trim();
            if (trim.startsWith("#EXTINF:")) {
                currentChannel = {};
                const idMatch = trim.match(/tvg-id="([^"]+)"/);
                const logoMatch = trim.match(/tvg-logo="([^"]+)"/);
                const groupMatch = trim.match(/group-title="([^"]+)"/);

                if (idMatch) currentChannel.id = idMatch[1];
                if (logoMatch) currentChannel.logo = logoMatch[1];
                if (groupMatch) currentChannel.group = groupMatch[1];

                const commaIndex = trim.lastIndexOf(',');
                if (commaIndex !== -1) {
                    currentChannel.name = trim.substring(commaIndex + 1).trim();
                }
            } else if (trim && !trim.startsWith("#") && currentChannel) {
                currentChannel.url = trim;

                // Construct standard MetaPreview object
                channels.push({
                    id: currentChannel.id || `iptv_${Math.random().toString(36).substring(7)}`,
                    type: "tv",
                    name: currentChannel.name || "Unknown Channel",
                    poster: currentChannel.logo,
                    background: currentChannel.logo,
                    posterShape: "landscape",
                    genres: currentChannel.group ? [currentChannel.group] : [],
                    description: `Live TV Channel${currentChannel.group ? ` • ${currentChannel.group}` : ""}`,
                    // Embed the raw stream URL deeply for the stream resolver
                    _iptvUrl: currentChannel.url
                });
                currentChannel = null;
            }
        }

        // Return a max of 2000 active channels to prevent memory issues in frontend
        res.json(channels.filter(c => c.name && c._iptvUrl).slice(0, 2000));
    } catch (err: any) {
        console.error("IPTV fetch error:", err);
        res.status(500).json({ error: "Failed to load IPTV channels" });
    }
});

/**
 * Native Magnet Scripter
 * Takes a plain-text search query and aggressively scrubs enabled torrent indexers.
 */
app.get("/api/scrape/:query", async (req, res) => {
    const { query } = req.params;
    try {
        // Return cached Master Scraper results instantly (no network delay)
        const isAnime = query.toLowerCase().includes('anime') || req.query.type === 'anime';

        const searchQ = encodeURIComponent(query.toLowerCase().replace(/[^a-z0-9]+/g, '-'));
        const searchRaw = encodeURIComponent(query);

        const animeSites = [
            { provider: "HiAnime", url: `https://hianime.to/search?keyword=${searchRaw}` },
            { provider: "9Anime TV", url: `https://9animetv.to/search?keyword=${searchRaw}` },
            { provider: "Miruro", url: `https://www.miruro.to/search?keyword=${searchRaw}` },
            { provider: "Anitaku", url: `https://anitaku.io/search.html?keyword=${searchRaw}` },
            { provider: "Anikai", url: `https://anikai.to/search?keyword=${searchRaw}` },
            { provider: "GoGoAnime", url: `https://wvv.gogoanime.org.vc/search.html?keyword=${searchRaw}` },
            { provider: "AniGo", url: `https://anigo.to/search?keyword=${searchRaw}` },
            { provider: "AniWatch", url: `https://aniwatchtv.to/search?keyword=${searchRaw}` },
            { provider: "UniqueStream", url: `https://anime.uniquestream.net/?s=${searchRaw}` },
            { provider: "AniWorld", url: `https://aniworld.to/search?q=${searchRaw}` },
            { provider: "123Animes", url: `https://w1.123animes.ru/search?keyword=${searchRaw}` },
            { provider: "9anime", url: `https://9anime.to/search?keyword=${searchRaw}` }
        ];

        const movieSites = [
            { provider: "Vidplay", url: `https://vidplay.top/search?keyword=${searchRaw}` },
            { provider: "LookMovie2", url: `https://www.lookmovie2.to/movies/search/?q=${searchRaw}` },
            { provider: "Putlocker", url: `https://putlocker.pe/search/${searchQ}` },
            { provider: "YesMovies", url: `https://ww1.yesmovies.ag/search/${searchQ}` },
            { provider: "WatchSeries", url: `https://watchseries.pe/search/${searchQ}` },
            { provider: "PrimeWire", url: `https://primewire.mov/search/${searchQ}` },
            { provider: "StreamM4U", url: `https://streamm4u.com.co/search/${searchQ}` },
            { provider: "MoviesFree", url: `https://moviesfree.cv/search/${searchQ}` },
            { provider: "Soap2Day", url: `https://ww3.soap2dayhdz.com/search/${searchQ}` },
            { provider: "SFlix", url: `https://sflix.ps/search/${searchQ}` },
            { provider: "TheFlixer", url: `https://theflixertv.to/search/${searchQ}` },
            { provider: "MyFlixerz", url: `https://myflixerz.to/search/${searchQ}` },
            { provider: "HDToday", url: `https://hdtodayz.to/search/${searchQ}` },
            { provider: "FlixHQ", url: `https://flixhq.to/search/${searchQ}` },
            { provider: "HuraWatch", url: `https://hurawatch.cc/search/${searchQ}` },
            { provider: "NunFlix", url: `https://nunflix.li/search/${searchQ}` },
            { provider: "RidoMovies", url: `https://ridomovies.tv/search/${searchQ}` },
            { provider: "123Movies", url: `https://ww8.123moviesfree.net/search/?q=${searchRaw}` },
            { provider: "Cineby", url: `https://www.cineby.gd/search/${searchQ}` },
            { provider: "FMovies", url: `https://fmovies.ps/search/${searchQ}` },
            { provider: "GoMovies", url: `http://gomovies.gg/search/${searchQ}` },
            { provider: "OnionPlay", url: `https://onionplay.cx/search/${searchQ}` },
            { provider: "YFlix", url: `https://yflix.to/search/${searchQ}` },
            // Torrent-focused providers (resolved by local scraper service)
            { provider: "Torrent Bay", url: `https://torrentbay.to/search?q=${searchRaw}` },
            { provider: "Torrent Bay Plus", url: `https://torrentbay.plus/search?q=${searchRaw}` },
            { provider: "1337x", url: `https://1337x.to/search/${searchQ}/1/` },
            { provider: "The Pirate Bay", url: `https://thepiratebay.org/search.php?q=${searchRaw}` }
        ];

        const scraperSites = isAnime ? animeSites : movieSites;

        const scraperResults = scraperSites.map(site => ({
            name: `Master Scraper\n${site.provider}`,
            title: `Direct HTTP Stream\n⚡ Instant Playback (Native)`,
            url: `sona-scrape://${site.url}`, // The UI will intercept this and postMessage to C#
            _nativeSortPriority: 6,
            _nativeSeeds: 9999
        }));

        // Start torrent search in background with timeout to avoid blocking
        const torrentSearchPromise = TorrentSearchApi.search(query, "All", 20) // Reduced from 100 to 20 for speed
            .catch(() => []);

        // Return scraper results immediately for instant UI feedback
        const results = await Promise.race([
            torrentSearchPromise,
            new Promise(resolve => setTimeout(() => resolve([]), 2000)) // Fallback after 2s
        ]);

        // Only process top 5 torrent results for speed
        const topResults = Array.isArray(results) ? results.slice(0, 5) : [];
        const streams = await Promise.allSettled(topResults.map(async (t: any) => {
            try {
                let magnet = t.magnet;
                if (!magnet || !magnet.includes("xt=urn:btih:")) {
                    const magnetPromise = TorrentSearchApi.getMagnet(t);
                    magnet = await Promise.race([
                        magnetPromise,
                        new Promise(resolve => setTimeout(() => resolve(null), 1000)) // 1s timeout per magnet
                    ]);
                }

                if (!magnet) return null;

                // Extract infoHash from magnet link
                const match = magnet.match(/xt=urn:btih:([a-zA-Z0-9]+)/i);
                const infoHash = match ? match[1].toLowerCase() : null;
                if (!infoHash) return null;

                // Inject trackers to magnet if it's too short
                if (!magnet.includes("&tr=")) {
                    magnet += [
                        "udp://tracker.opentrackr.org:1337/announce",
                        "udp://tracker.openbittorrent.com:6969/announce",
                        "udp://9.rarbg.to:2920/announce",
                        "udp://tracker.torrent.eu.org:451/announce"
                    ].map(tr => `&tr=${encodeURIComponent(tr)}`).join('');
                }

                // Try to infer quality from title
                const title = t.title.toUpperCase();
                let qualityPriority = 0;
                let qualityLabel = "SD";
                if (title.includes("2160P") || title.includes("4K")) { qualityPriority = 4; qualityLabel = "4K"; }
                else if (title.includes("1080P")) { qualityPriority = 3; qualityLabel = "1080p"; }
                else if (title.includes("720P")) { qualityPriority = 2; qualityLabel = "720p"; }
                else if (title.includes("HDRIP") || title.includes("WEBRIP")) { qualityPriority = 1; qualityLabel = "HD"; }

                return {
                    name: `Native Scraper\n${qualityLabel} | ${t.provider}`,
                    title: `${t.title}\n👥 ${t.seeds || 0} Seeders | 💾 ${t.size}`,
                    infoHash,
                    behaviorHints: { bingeGroup: `native-${qualityPriority}` },
                    _nativeSortPriority: qualityPriority,
                    _nativeSeeds: parseInt(t.seeds) || 0
                };
            } catch (e) {
                return null;
            }
        }));

        const validStreams = [...scraperResults, ...streams]
            .filter((s: any) => s !== null && s.status !== 'rejected')
            .map((s: any) => s.status === 'fulfilled' ? s.value : s)
            .sort((a: any, b: any) => {
                // 1. Sort by Quality (4K > 1080p > 720p > others) and master scraper priority
                if (a._nativeSortPriority !== b._nativeSortPriority) return b._nativeSortPriority - a._nativeSortPriority;
                // 2. Fallback to sorting by number of seeders
                return b._nativeSeeds - a._nativeSeeds;
            });

        res.json(validStreams);
    } catch (err: any) {
        console.error("Native Scraper Error:", err);
        // Fallback to empty stream list if scraping utterly fails
        res.json([]);
    }
});

/**
 * Stream endpoint
 * GET /api/stream/:infoHash/:fileIdx
 *
 * Supports Range requests so <video> seeking works properly.
 */
app.get("/api/stream/:infoHash/:fileIdx", async (req: Request, res: Response) => {
    const infoHash = req.params.infoHash.toLowerCase();
    const { fileIdx } = req.params;
    console.log(`[streaming] INCOMING START: ${infoHash} | fileIdx: ${fileIdx}`);
    const fileIndex = parseInt(fileIdx, 10);
    const forceTranscode = String(req.query.transcode || "") === "1";

    try {
        console.log(`[streaming] Resolving torrent: ${infoHash}...`);
        const torrent = await getOrAddTorrent(infoHash);
        console.log(`[streaming] Torrent resolved: ${torrent.name || infoHash}`);

        const file = isNaN(fileIndex)
            ? torrent.files.reduce((a: any, b: any) => (a.length > b.length ? a : b)) // largest file
            : torrent.files[fileIndex] ?? torrent.files.reduce((a: any, b: any) => (a.length > b.length ? a : b));

        if (!file) {
            return res.status(404).json({ error: "File not found in torrent" });
        }

        const fileLength = file.length;
        const rangeHeader = req.headers.range;

        const { range } = req.headers;

        // Comprehensive check for formats that browsers (Chrome/Electron/Edge) often struggle with
        const filename = file.name.toLowerCase();
        const nonWebReady =
            filename.endsWith(".mkv") || filename.endsWith(".avi") ||
            filename.endsWith(".ts") || filename.endsWith(".wmv") ||
            filename.endsWith(".flv") || filename.endsWith(".vob") ||
            filename.endsWith(".mpg") || filename.endsWith(".mpeg") ||
            filename.endsWith(".m4v") || filename.endsWith(".3gp") ||
            filename.endsWith(".f4v");

        const suspiciousCodecs =
            filename.includes("x265") || filename.includes("hevc") ||
            filename.includes("h265") || filename.includes("10bit") ||
            filename.includes("av1") || filename.includes("hi10p") ||
            filename.includes("ac3") || filename.includes("dts") ||
            filename.includes("eac3") || filename.includes("opus") ||
            filename.includes("truehd");

        // If it's NOT a plain MP4 or WebM, it's safer to transcode
        const isNotWebNative = !filename.endsWith(".mp4") && !filename.endsWith(".webm") && !filename.endsWith(".ogv");

        const needsTranscode = nonWebReady || suspiciousCodecs || isNotWebNative;

        const startSeconds = Number(req.query.start || 0);
        const audioTrack = req.query.audio !== undefined ? Number(req.query.audio) : -1;
        const isVlc = String(req.query.vlc) === "1" || req.headers["user-agent"]?.includes("VLC");

        // If it's VLC, we can often skip transcoding to save CPU and preserve seeking
        const finalNeedsTranscode = isVlc ? false : (needsTranscode || forceTranscode);

        if (finalNeedsTranscode) {
            console.log(`[streaming] Transcoding: ${file.name} | Start: ${startSeconds}s | Audio: ${audioTrack}`);
            res.writeHead(200, {
                "Content-Type": "video/mp4",
                "Cache-Control": "no-cache",
                "X-Content-Type-Options": "nosniff"
            });

            // We use file.createReadStream() - but FFMPEG can seek much better if we let it
            // However, to save bandwidth/peers, we could try to start the readSteam at an offset
            // if we had a bitrate estimate. For now, we rely on FFMPEG's seekInput.
            const rawStream = file.createReadStream();
            const command = ffmpeg(rawStream)
                .videoCodec('libx264')
                .audioCodec('aac')
                .outputFormat('mp4')
                .outputOptions([
                    '-movflags frag_keyframe+empty_moov+faststart+default_base_moof',
                    '-preset ultrafast',
                    '-tune zerolatency',
                    '-pix_fmt yuv420p',
                    '-metadata:s:a:0 language=eng',
                    '-threads 0', // Auto use all cores
                    '-sn' // Skip subtitles for faster transcoding (handled via separate API)
                ]);

            if (startSeconds > 0) {
                command.seekInput(startSeconds);
            }

            // Audio track selection logic
            if (audioTrack >= 0) {
                command.outputOptions([
                    '-map 0:v:0',           // First video track
                    `-map 0:a:${audioTrack}` // Selected audio track
                ]);
            } else {
                command.outputOptions([
                    '-map 0:v:0?',
                    '-map 0:a:m:language:eng?',
                    '-map 0:a:0?'
                ]);
            }

            command
                .on('error', (err) => {
                    if (!err.message.includes('Output stream closed') && !err.message.includes('SIGKILL')) {
                        console.error('FFmpeg error:', err.message);
                    }
                })
                .pipe(res, { end: true });

            req.on('close', () => {
                try {
                    // Force kill FFMPEG when client disconnects
                    (command as any).kill('SIGKILL');
                } catch { }
                rawStream.destroy();
            });
            return;
        }

        if (!range) {
            const mimeType = mime.lookup(file.name) || "video/mp4";
            res.writeHead(200, {
                "Content-Length": file.length,
                "Content-Type": mimeType,
            });
            file.createReadStream().pipe(res);
            return;
        }

        const parts = range.replace(/bytes=/, "").split("-");
        const start = parseInt(parts[0], 10);
        const end = parts[1] ? parseInt(parts[1], 10) : fileLength - 1;
        const chunkSize = (end - start) + 1;
        try {
            const pieceLen = torrent.pieceLength || 0;
            if (pieceLen > 0) {
                // PURE SEQUENTIAL FLOW (Request: piece by piece)
                const windowStart = Math.max(0, Math.floor(start / pieceLen));
                const windowEnd = Math.min(torrent.pieces.length - 1, Math.floor((end + 32 * 1024 * 1024) / pieceLen)); // 32MB ahead

                // High priority on immediate pieces
                torrent.select(windowStart, windowEnd, 10);
                torrent.critical(windowStart, Math.min(windowStart + 5, windowEnd));

                // Keep a small buffer ahead but don't download the whole file
                // Deselect everything beyond 100MB to focus peers on "piece by piece" downloading
                const cutoff = Math.min(torrent.pieces.length - 1, windowStart + Math.ceil((100 * 1024 * 1024) / pieceLen));
                torrent.deselect(cutoff, torrent.pieces.length - 1, false);
            }
        } catch { }

        const mimeType = mime.lookup(file.name) || "video/mp4";
        res.writeHead(206, {
            "Content-Range": `bytes ${start}-${end}/${fileLength}`,
            "Accept-Ranges": "bytes",
            "Content-Length": chunkSize,
            "Content-Type": mimeType,
        });

        const stream = file.createReadStream({ start, end });
        console.log(`[streaming] Chunk serving: ${start}-${end} (Length: ${chunkSize}) | Peers: ${torrent.numPeers}`);

        stream.pipe(res);
        req.on('close', () => {
            try {
                stream.destroy();
            } catch { }
        });
    } catch (err: any) {
        console.error("[stream] Error:", err.message);
        if (!res.headersSent) {
            res.status(500).json({ error: err.message || "Streaming failed" });
        }
    }
});

/**
 * Transcode remote direct video URLs to browser-friendly MP4/H.264/AAC
 * GET /api/stream/http?u=<encoded-url>&h=<base64-json-headers>
 */
app.get("/api/stream/http", async (req: Request, res: Response) => {
    try {
        const u = (req.query.u as string) || "";
        const h = (req.query.h as string) || "";
        if (!u) return res.status(400).json({ error: "Missing u" });
        let headers: Record<string, string> = {};
        try { headers = h ? JSON.parse(Buffer.from(h, "base64").toString("utf-8")) : {}; } catch { }
        const ua = headers["User-Agent"] || headers["user-agent"] || "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";
        headers["User-Agent"] = ua;
        if ((headers["Referer"] || headers["referer"]) && !headers["Origin"]) {
            try { headers["Origin"] = new URL(headers["Referer"] || headers["referer"] as string).origin; } catch { }
        }
        const upstream = await fetch(u, { headers, signal: AbortSignal.timeout(20000) });
        if (!upstream.ok || !upstream.body) {
            return res.status(upstream.status || 502).json({ error: "Upstream failed" });
        }
        res.writeHead(200, {
            "Content-Type": "video/mp4",
            "Accept-Ranges": "none",
        });
        const input = upstream.body as any;
        const ff = ffmpeg(input)
            .videoCodec('libx264')
            .audioCodec('aac')
            .outputFormat('mp4')
            .outputOptions([
                '-movflags frag_keyframe+empty_moov+faststart',
                '-preset ultrafast',
                '-tune zerolatency',
                '-pix_fmt yuv420p',
                '-probesize 32',
                '-analyzeduration 0',
                '-map 0:v:0?',
                '-map 0:a:0?',
            ])
            .on('error', (err) => {
                if (!res.headersSent) {
                    res.status(500).end();
                }
                try { upstream.body?.cancel(); } catch { }
                console.error('FFmpeg http transcode error:', err.message);
            });
        ff.pipe(res, { end: true });
    } catch (err: any) {
        console.error("[stream/http] Error:", err?.message);
        if (!res.headersSent) res.status(500).json({ error: "HTTP transcode failed" });
    }
});

/**
 * Transcode remote HLS playlists (.m3u8) to MP4/H.264/AAC
 * GET /api/stream/hls?u=<encoded-url>&h=<base64-json-headers>
 */
app.get("/api/stream/hls", async (req: Request, res: Response) => {
    try {
        const u = (req.query.u as string) || "";
        const h = (req.query.h as string) || "";
        if (!u) return res.status(400).json({ error: "Missing u" });
        let headers: Record<string, string> = {};
        try { headers = h ? JSON.parse(Buffer.from(h, "base64").toString("utf-8")) : {}; } catch { }
        const ua = headers["User-Agent"] || headers["user-agent"] || "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";
        const referer = headers["Referer"] || headers["referer"] || "";
        const origin = headers["Origin"] || (referer ? new URL(referer).origin : "");
        res.writeHead(200, { "Content-Type": "video/mp4", "Accept-Ranges": "none" });
        const ff = ffmpeg(u)
            .inputOptions([
                `-user_agent ${ua}`,
                ...(referer ? [`-headers`, `Referer: ${referer}`] : []),
                ...(origin ? [`-headers`, `Origin: ${origin}`] : []),
            ])
            .videoCodec('libx264')
            .audioCodec('aac')
            .outputFormat('mp4')
            .outputOptions([
                '-movflags frag_keyframe+empty_moov+faststart',
                '-preset ultrafast',
                '-tune zerolatency',
                '-pix_fmt yuv420p',
                '-probesize 32',
                '-analyzeduration 0',
                '-map 0:v:0?',
                '-map 0:a:0?',
            ])
            .on('error', (err) => {
                if (!res.headersSent) {
                    res.status(500).end();
                }
                console.error('FFmpeg HLS transcode error:', err.message);
            });
        ff.pipe(res, { end: true });
    } catch (err: any) {
        console.error("[stream/hls] Error:", err?.message);
        if (!res.headersSent) res.status(500).json({ error: "HLS transcode failed" });
    }
});

/**
 * Transcode remote DASH manifests (.mpd) to MP4/H.264/AAC
 * GET /api/stream/dash?u=<encoded-url>&h=<base64-json-headers>
 */
app.get("/api/stream/dash", async (req: Request, res: Response) => {
    try {
        const u = (req.query.u as string) || "";
        const h = (req.query.h as string) || "";
        if (!u) return res.status(400).json({ error: "Missing u" });
        let headers: Record<string, string> = {};
        try { headers = h ? JSON.parse(Buffer.from(h, "base64").toString("utf-8")) : {}; } catch { }
        const ua = headers["User-Agent"] || headers["user-agent"] || "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0 Safari/537.36";
        const referer = headers["Referer"] || headers["referer"] || "";
        const origin = headers["Origin"] || (referer ? new URL(referer).origin : "");
        res.writeHead(200, { "Content-Type": "video/mp4", "Accept-Ranges": "none" });
        const ff = ffmpeg(u)
            .inputOptions([
                `-user_agent ${ua}`,
                ...(referer ? [`-headers`, `Referer: ${referer}`] : []),
                ...(origin ? [`-headers`, `Origin: ${origin}`] : []),
            ])
            .videoCodec('libx264')
            .audioCodec('aac')
            .outputFormat('mp4')
            .outputOptions([
                '-movflags frag_keyframe+empty_moov+faststart',
                '-preset ultrafast',
                '-tune zerolatency',
                '-pix_fmt yuv420p',
                '-probesize 32',
                '-analyzeduration 0',
                '-map 0:v:0?',
                '-map 0:a:0?',
            ])
            .on('error', (err) => {
                if (!res.headersSent) {
                    res.status(500).end();
                }
                console.error('FFmpeg DASH transcode error:', err.message);
            });
        ff.pipe(res, { end: true });
    } catch (err: any) {
        console.error("[stream/dash] Error:", err?.message);
        if (!res.headersSent) res.status(500).json({ error: "DASH transcode failed" });
    }
});
/**
 * Probe a video file for audio tracks and subtitles
 * GET /api/probe/:infoHash/:fileIdx
 */
app.get("/api/probe/:infoHash/:fileIdx", async (req: Request, res: Response) => {
    const { infoHash, fileIdx } = req.params;
    const fileIndex = parseInt(fileIdx, 10);
    try {
        const torrent = await getOrAddTorrent(infoHash);
        const file = torrent.files[fileIndex] || torrent.files.find((f: any) => f.name.match(/\.(mp4|mkv|avi|ts|m4v)$/i));
        if (!file) return res.status(404).json({ error: "File not found" });

        // Pipe a small chunk (first 2MB) to ffprobe
        const stream = file.createReadStream({ start: 0, end: 2 * 1024 * 1024 });
        ffmpeg(stream).ffprobe((err, data) => {
            stream.destroy();
            if (err) return res.status(500).json({ error: "Probe failed", detail: err.message });

            const audioTracks = data.streams
                .filter(s => s.codec_type === 'audio')
                .map((s, idx) => ({
                    id: s.index,
                    index: idx,
                    language: s.tags?.language || 'und',
                    title: s.tags?.title || `Track ${idx + 1}`,
                    codec: s.codec_name
                }));

            const subtitles = data.streams
                .filter(s => s.codec_type === 'subtitle')
                .map((s, idx) => ({
                    id: s.index,
                    index: idx,
                    language: s.tags?.language || 'und',
                    title: s.tags?.title || `Subtitle ${idx + 1}`,
                    codec: s.codec_name,
                    isInternal: true
                }));

            res.json({ audioTracks, subtitles });
        });
    } catch (e: any) {
        res.status(500).json({ error: "Failed to probe", detail: e.message });
    }
});

/**
 * Extract an internal subtitle track
 * GET /api/subtitles/:infoHash/:fileIdx/:trackId
 */
app.get("/api/subtitles/:infoHash/:fileIdx/:trackId", async (req, res) => {
    const { infoHash, fileIdx, trackId } = req.params;
    try {
        const torrent = await getOrAddTorrent(infoHash);
        const file = torrent.files[parseInt(fileIdx)];
        const input = file.createReadStream();

        res.setHeader("Content-Type", "text/vtt");
        ffmpeg(input)
            .map(`0:s:${trackId}`)
            .outputFormat('webvtt')
            .on('error', () => res.end())
            .pipe(res, { end: true });
    } catch {
        res.status(500).end();
    }
});

app.listen(PORT, () => {
    console.log(`✅ Nexus streaming server running at http://localhost:${PORT}`);
    console.log(`   Health: http://localhost:${PORT}/api/health`);
});

export default app;
