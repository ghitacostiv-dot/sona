using System;
using System.Collections.Generic;

namespace SONA.Services
{
    /// <summary>
    /// Generates free embeddable streaming URLs for movies and TV series using IMDB/TMDB IDs.
    /// Supports 12+ providers with priority ordering and fallback chaining.
    /// </summary>
    public static class StreamingService
    {
        // ── Embed Providers (in priority order) ─────────────────────────────
        public static readonly List<EmbedProvider> Providers = new()
        {
            new("VidSrc.xyz",     "vidsrc",       "https://vidsrc.xyz/embed"),
            new("VidSrc.me",      "vidsrcme",     "https://vidsrc.me/embed"),
            new("VidSrc.pro",     "vidsrcpro",    "https://vidsrc.pro/embed"),
            new("AutoEmbed",      "autoembed",    "https://autoembed.cc/embed"),
            new("2Embed",         "2embed",       "https://www.2embed.cc/embed"),
            new("MultiEmbed",     "multiembed",   "https://multiembed.mov"),
            new("Embed.su",       "embedsu",      "https://embed.su/embed"),
            new("SmashyStream",   "smashy",       "https://player.smashy.stream/movie"),
            new("MoviesAPI",      "moviesapi",    "https://moviesapi.club"),
            new("111Movies",      "111movies",    "https://111movies.com/movie"),
            new("NonStop",        "nonstop",      "https://non-stop.ru/embed"),
            new("Nontongo",       "nontongo",     "https://www.nontongo.win/embed"),
        };

        // ── Primary + Fallback (legacy compat) ──────────────────────────────
        public static string GetEmbedUrl(string imdbId, string type, int? season = null, int? episode = null)
            => BuildUrl(Providers[0].BaseUrl, imdbId, type, season, episode, Providers[0].Key);

        public static string GetFallbackEmbedUrl(string imdbId, string type, int? season = null, int? episode = null)
            => BuildUrl(Providers[4].BaseUrl, imdbId, type, season, episode, Providers[4].Key);

        /// <summary>
        /// Returns all available embed URLs for all providers, best-first.
        /// </summary>
        public static List<(string Name, string Url)> GetAllEmbedUrls(string imdbId, string type, int? season = null, int? episode = null)
        {
            var results = new List<(string, string)>();
            foreach (var p in Providers)
            {
                var url = BuildUrl(p.BaseUrl, imdbId, type, season, episode, p.Key);
                if (!string.IsNullOrEmpty(url))
                    results.Add((p.Name, url));
            }
            return results;
        }

        /// <summary>
        /// TMDB poster URL (no API key needed for image CDN).
        /// </summary>
        public static string GetPosterUrl(string posterPath, string size = "w342")
            => string.IsNullOrEmpty(posterPath) ? "" : $"https://image.tmdb.org/t/p/{size}{posterPath}";

        // ── Internal URL Builder ────────────────────────────────────────────
        private static string BuildUrl(string baseUrl, string imdbId, string type, int? season, int? episode, string providerKey)
        {
            if (string.IsNullOrWhiteSpace(imdbId)) return "";

            bool isSeries = type == "series" || type == "tv";

            return providerKey switch
            {
                "vidsrc"     => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}/{season}/{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                "vidsrcme"   => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv?imdb={imdbId}&season={season}&episode={episode}"
                                    : $"{baseUrl}/movie?imdb={imdbId}",

                "vidsrcpro"  => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}/{season}-{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                "autoembed"  => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}/{season}/{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                "2embed"     => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/{imdbId}?s={season}&e={episode}"
                                    : $"{baseUrl}/{imdbId}",

                "multiembed" => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/?video_id={imdbId}&tmdb=1&s={season}&e={episode}"
                                    : $"{baseUrl}/?video_id={imdbId}&tmdb=1",

                "embedsu"    => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}/{season}/{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                "smashy"     => $"{baseUrl}/{imdbId}",  // movies only for now

                "moviesapi"  => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}-{season}-{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                "111movies"  => $"{baseUrl}/{imdbId}",

                "nonstop"    => $"{baseUrl}/{imdbId}",

                "nontongo"   => isSeries && season.HasValue && episode.HasValue
                                    ? $"{baseUrl}/tv/{imdbId}/{season}/{episode}"
                                    : $"{baseUrl}/movie/{imdbId}",

                _ => $"{baseUrl}/{imdbId}"
            };
        }
    }

    public record EmbedProvider(string Name, string Key, string BaseUrl);
}
