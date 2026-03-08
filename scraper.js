/**
 * SONA Master Stream Scraper
 * ──────────────────────────
 * Uses Playwright to headlessly navigate streaming sites and intercept
 * .m3u8 / .mpd manifest URLs from network traffic (no fragile regex).
 *
 * API:
 *   POST /scrape       { source_url, timeout_ms? }  → { stream_url, headers, subtitles, provider }
 *   POST /scrape/multi { source_url, timeout_ms? }  → { streams: [...], best: {...} }
 *   GET  /health       → { status: "ok" }
 *   GET  /providers    → list of known provider configs
 */

'use strict';

const express = require('express');
const cors = require('cors');
const { chromium } = require('playwright');

const app = express();
const PORT = 3333;

app.use(cors());
app.use(express.json());

// ── Provider site configs ─────────────────────────────────────────────────────
// Each entry describes how to handle a given host so we can resolve the player
// more reliably (e.g. click a play button before intercepting).
const PROVIDER_CONFIGS = [
    // ── Movies / TV Sources (from SONA SourcesData) ───────────────────────
    { host: 'vidplay', clickSel: '.jw-icon-display, .play-button, [aria-label="Play"]', waitMs: 4000 },
    { host: 'lookmovie2', clickSel: '.jw-icon-display, #play-button', waitMs: 4000 },
    { host: 'flixhq', clickSel: '.jw-icon-display, .lnk-play', waitMs: 5000 },
    { host: 'sflix', clickSel: '.play-btn, .jw-icon-display', waitMs: 4000 },
    { host: 'theflixer', clickSel: '.jw-icon-display, .play-video', waitMs: 4000 },
    { host: 'myflixerz', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'hdtoday', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'hurawatch', clickSel: '.jw-icon-display, .play-btn', waitMs: 4000 },
    { host: 'cineby', clickSel: '.play-button, [class*="play"]', waitMs: 4000 },
    { host: 'nunflix', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'ridomovies', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'soap2day', clickSel: '.jw-icon-display, #sv-player', waitMs: 4000 },
    { host: 'streamm4u', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'gomovies', clickSel: '.jw-icon-display, .btn-play', waitMs: 5000 },
    { host: 'primewire', clickSel: '.jw-icon-display', waitMs: 5000 },
    { host: 'yesmovies', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'wmovies', clickSel: '.jw-icon-display', waitMs: 4000 },
    { host: 'hydrahd', clickSel: '.jw-icon-display, .play-btn', waitMs: 4000 },
    { host: '123moviesfree', clickSel: '.jw-icon-display', waitMs: 4000 },
    // ── Anime Sources ─────────────────────────────────────────────────────
    { host: 'hianime', clickSel: '#player-container .play-btn, .jw-icon-display', waitMs: 5000 },
    { host: '9animetv', clickSel: '.jw-icon-display, .btn-play', waitMs: 5000 },
    { host: 'miruro', clickSel: '.jw-icon-display, [class*="play"]', waitMs: 5000 },
    { host: 'anitaku', clickSel: '.jw-icon-display', waitMs: 5000 },
    { host: 'aniwatchtv', clickSel: '.jw-icon-display', waitMs: 5000 },
    { host: 'gogoanime', clickSel: '.jw-icon-display', waitMs: 5000 },
    // ── Live TV Sources ───────────────────────────────────────────────────
    { host: 'dlhd', clickSel: '.jw-icon-display, .play-btn', waitMs: 3000 },
    { host: 'ntvstream', clickSel: '.jw-icon-display', waitMs: 3000 },
    { host: 'iptv-web', clickSel: '.jw-icon-display', waitMs: 3000 },
    // ── Embed providers (no click needed — stream starts immediately) ──────
    { host: 'vidsrc', clickSel: null, waitMs: 6000 },
    { host: 'autoembed', clickSel: null, waitMs: 5000 },
    { host: '2embed', clickSel: null, waitMs: 5000 },
    { host: 'multiembed', clickSel: null, waitMs: 5000 },
    { host: 'embed.su', clickSel: null, waitMs: 5000 },
    { host: 'smashy', clickSel: null, waitMs: 5000 },
    { host: 'moviesapi', clickSel: null, waitMs: 5000 },
];

function getProviderConfig(url) {
    try {
        const host = new URL(url).hostname.toLowerCase();
        return PROVIDER_CONFIGS.find(p => host.includes(p.host)) || { clickSel: null, waitMs: 5000 };
    } catch { return { clickSel: null, waitMs: 5000 }; }
}

// ── Stream quality ranking ─────────────────────────────────────────────────────
function rankStream(url) {
    // Higher number = better quality / more likely direct stream
    if (url.includes('1080')) return 6;
    if (url.includes('720')) return 5;
    if (url.includes('480')) return 4;
    if (url.includes('master')) return 7; // HLS master playlist (best)
    if (url.includes('index')) return 5;
    if (url.includes('.m3u8') || url.includes('m3u')) return 6; // HLS (Natively boosted)
    if (url.includes('.mpd')) return 5; // DASH (Now supported via dash.js)
    return 1;
}

// ── Core scraper ───────────────────────────────────────────────────────────────
async function scrapeStream(sourceUrl, timeoutMs = 15000) {
    const config = getProviderConfig(sourceUrl);
    const effectiveTimeout = Math.max(timeoutMs, config.waitMs + 3000);

    let browser = null;
    const captured = []; // { url, headers, type }

    try {
        browser = await chromium.launch({
            headless: true,
            args: [
                '--no-sandbox',
                '--disable-setuid-sandbox',
                '--disable-blink-features=AutomationControlled',
                '--disable-web-security',
                '--autoplay-policy=no-user-gesture-required',
            ]
        });

        // Playwright's newContext(permissions) does NOT support "autoplay" — it causes "Unknown permission: autoplay".
        // Autoplay is enabled via launch args: --autoplay-policy=no-user-gesture-required.
        const contextOptions = {
            userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36',
            viewport: { width: 1280, height: 720 },
            ignoreHTTPSErrors: true,
        };
        const context = await browser.newContext(contextOptions);
        try {
            const origin = new URL(sourceUrl).origin;
            await context.setExtraHTTPHeaders({
                'Referer': sourceUrl,
                'Origin': origin,
                'User-Agent': contextOptions.userAgent
            });
        } catch {}

        // ── Network interception: catch all .m3u8 / .mpd requests ────────────
        await context.route('**/*', async (route) => {
            const req = route.request();
            const url = req.url();
            const resType = req.resourceType();

            const isStream = url.includes('.m3u8') || url.includes('.mpd') ||
                url.includes('manifest') || resType === 'media';

            if (isStream) {
                captured.push({
                    url,
                    headers: {
                        'Referer': req.headers()['referer'] || sourceUrl,
                        'User-Agent': req.headers()['user-agent'] || '',
                        'Origin': req.headers()['origin'] || ''
                    },
                    type: url.includes('.mpd') ? 'dash' : 'hls',
                    rank: rankStream(url)
                });
            }

            await route.continue();
        });

        const page = await context.newPage();

        // Inject anti-bot evasion before navigation
        await page.addInitScript(() => {
            Object.defineProperty(navigator, 'webdriver', { get: () => false });
            window.chrome = { runtime: {} };
        });

        // Navigate with timeout
        try {
            await page.goto(sourceUrl, {
                waitUntil: 'domcontentloaded',
                timeout: effectiveTimeout
            });
        } catch (navErr) {
            // Timeout on navigation is acceptable — the player may still load
        }

        // If it's a search page, we need to click the first valid search result!
        if (sourceUrl.includes('/search') || sourceUrl.includes('?keyword=') || sourceUrl.includes('?q=')) {
            try {
                // Wait briefly for client-side search results to render
                await page.waitForTimeout(2000);

                const clicked = await page.evaluate(() => {
                    const links = Array.from(document.querySelectorAll('a[href]'));
                    // Method 1: Look for explicit /movie, /watch, /tv links
                    for (const a of links) {
                        const href = a.href.toLowerCase();
                        if (href.includes('/movie') || href.includes('/watch') || href.includes('/series')
                            || href.includes('/episode') || href.includes('/tv') || href.includes('/film') || href.includes('/video')) {
                            // Exclude generic genre/menu links
                            if (!href.endsWith('/movies') && !href.endsWith('/tv-shows') && !href.endsWith('/series') && !href.endsWith('/episodes')) {
                                a.click();
                                return true;
                            }
                        }
                    }
                    // Method 2: Fallback to clicking the first image wrapped in an anchor (usually a poster)
                    const imgs = Array.from(document.querySelectorAll('a img'));
                    if (imgs.length > 0) {
                        const anchor = imgs[0].closest('a');
                        if (anchor) {
                            anchor.click();
                            return true;
                        }
                    }
                    return false;
                });

                if (clicked) {
                    console.log(`[scraper] Clicked search result on ${sourceUrl}`);
                    // Wait for navigation to complete before looking for play buttons
                    await page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 5000 }).catch(() => { });
                    await page.waitForTimeout(2000); // Give player time to settle
                }
            } catch (e) {
                console.log(`[scraper] Search click logic failed:`, e.message);
            }
        }

        // Try to click play button
        if (config.clickSel) {
            try {
                await page.waitForSelector(config.clickSel, { timeout: 5000 });
                await page.click(config.clickSel);
                await page.waitForLoadState('domcontentloaded', { timeout: 4000 }).catch(() => {});
                await page.waitForTimeout(2000);
            } catch {
                // No play button found — some players autostart
            }
        }

        // Wait for stream requests to fire
        await page.waitForTimeout(config.waitMs);

        // Also scan all frames (video players often load inside iframes)
        for (const frame of page.frames()) {
            try {
                const iframeSrc = frame.url();
                if (iframeSrc && iframeSrc !== sourceUrl && iframeSrc !== 'about:blank') {
                    // Check for stream URLs in the iframe's requests (already caught via route)
                    // Also check iframe DOM for video sources
                    const videoSrcs = await frame.evaluate(() => {
                        const vs = [];
                        document.querySelectorAll('video, source').forEach(el => {
                            if (el.src) vs.push(el.src);
                            if (el.currentSrc) vs.push(el.currentSrc);
                        });
                        return vs;
                    }).catch(() => []);

                    for (const src of videoSrcs) {
                        if (src && (src.includes('.m3u8') || src.includes('.mpd') || src.startsWith('http'))) {
                            captured.push({
                                url: src,
                                headers: { 'Referer': iframeSrc || sourceUrl, 'User-Agent': '' },
                                type: src.includes('.mpd') ? 'dash' : 'hls',
                                rank: rankStream(src) + 1 // Slight bonus for DOM-found sources
                            });
                        }
                    }
                }
            } catch { /* skip broken frames */ }
        }

        // Enrich captured entries with cookies and defaults before closing
        const contextCookies = async (u) => {
            try {
                const origin = new URL(u).origin;
                const cookies = await context.cookies(origin);
                if (!cookies || cookies.length === 0) return undefined;
                return cookies.map(c => `${c.name}=${c.value}`).join('; ');
            } catch { return undefined; }
        };
        for (const s of captured) {
            const ck = await contextCookies(s.url);
            if (ck) {
                s.headers = { ...(s.headers || {}), 'Cookie': ck };
            }
            s.headers = {
                ...(s.headers || {}),
                'Accept': 'application/vnd.apple.mpegurl,application/x-mpegURL,video/*;q=0.9,*/*;q=0.8',
                'Accept-Language': 'en-US,en;q=0.9'
            };
        }

        await browser.close();
        browser = null;

        // ── Process results ────────────────────────────────────────────────────
        // Deduplicate
        const seen = new Set();
        const unique = captured.filter(s => {
            if (seen.has(s.url)) return false;
            seen.add(s.url);
            return true;
        });

        if (unique.length === 0) {
            return { success: false, error: 'No stream URLs intercepted from ' + sourceUrl, streams: [] };
        }

        // Sort by quality rank descending
        unique.sort((a, b) => b.rank - a.rank);
        const best = unique[0];

        return {
            success: true,
            provider: new URL(sourceUrl).hostname,
            stream_url: best.url,
            stream_type: best.type,
            headers: best.headers,
            subtitles: [],   // TODO: intercept .vtt subtitle tracks
            streams: unique, // All found streams, ranked
        };

    } catch (err) {
        if (browser) { try { await browser.close(); } catch { } }
        return { success: false, error: err.message, streams: [] };
    }
}

// ── Retry wrapper ──────────────────────────────────────────────────────────────
async function scrapeWithRetry(sourceUrl, maxRetries = 2, timeoutMs = 15000) {
    let lastErr = null;
    for (let attempt = 0; attempt <= maxRetries; attempt++) {
        const result = await scrapeStream(sourceUrl, timeoutMs);
        if (result.success) return { ...result, attempts: attempt + 1 };
        lastErr = result.error;
        console.warn(`[scraper] Attempt ${attempt + 1} failed for ${sourceUrl}: ${lastErr}`);
        if (attempt < maxRetries) await new Promise(r => setTimeout(r, 1500 * (attempt + 1)));
    }
    return { success: false, error: lastErr, streams: [], attempts: maxRetries + 1 };
}

// ── API Routes ─────────────────────────────────────────────────────────────────

// POST /scrape — single best stream
app.post('/scrape', async (req, res) => {
    const { source_url, timeout_ms = 15000 } = req.body;
    if (!source_url) return res.status(400).json({ error: 'source_url is required' });

    console.log(`[scraper] Scraping: ${source_url}`);
    const result = await scrapeWithRetry(source_url, 1, timeout_ms);

    // We already have result.streams (sorted by rank)
    res.json(result);
});

// POST /scrape/multi — return all streams found (for quality selection)
app.post('/scrape/multi', async (req, res) => {
    const { source_url, timeout_ms = 20000 } = req.body;
    if (!source_url) return res.status(400).json({ error: 'source_url is required' });

    console.log(`[scraper] Multi-scraping: ${source_url}`);
    const result = await scrapeWithRetry(source_url, 1, timeout_ms);
    res.json(result);
});

// GET /health
app.get('/health', (_, res) => res.json({ status: 'ok', port: PORT, time: new Date().toISOString() }));

// GET /providers
app.get('/providers', (_, res) => res.json({ providers: PROVIDER_CONFIGS.map(p => p.host) }));

// ── Start ──────────────────────────────────────────────────────────────────────
app.listen(PORT, () => {
    console.log(`\n🕷️  SONA Master Stream Scraper running on http://localhost:${PORT}`);
    console.log(`   POST /scrape        { source_url } → best stream URL + headers`);
    console.log(`   POST /scrape/multi  { source_url } → all streams ranked by quality`);
    console.log(`   GET  /health        → health check\n`);
});
