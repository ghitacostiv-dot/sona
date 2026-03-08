using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SONA.Services
{
    public static class MetadataFetcher
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Searches IGDB for game metadata using user's API credentials.
        /// </summary>
        public static async Task<JArray> SearchIgdbGamesAsync(string gameName)
        {
            try
            {
                var clientId = AppConfig.GetString("igdb_client_id");
                var secretUrl = AppConfig.GetString("igdb_client_secret");

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secretUrl))
                {
                    return new JArray();
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Add("Client-ID", clientId);
                request.Headers.Add("Authorization", $"Bearer {secretUrl}");

                string query = $"search \"{gameName}\"; fields name,cover.url,summary,rating,genres.name,videos.video_id,first_release_date,involved_companies.company.name; limit 10;";
                request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    return JArray.Parse(json);
                }
                return new JArray();
            }
            catch
            {
                return new JArray();
            }
        }

        public static async Task<JArray> GetIgdbCatalogAsync(string filter = "sort rating desc; where rating > 80;")
        {
            try
            {
                var clientId = AppConfig.GetString("igdb_client_id");
                var secretUrl = AppConfig.GetString("igdb_client_secret");
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secretUrl)) return new JArray();

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games");
                request.Headers.Add("Client-ID", clientId);
                request.Headers.Add("Authorization", $"Bearer {secretUrl}");

                string query = $"fields name,cover.url,summary,rating,genres.name,first_release_date,involved_companies.company.name; {filter} limit 10;";
                request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode) return JArray.Parse(await response.Content.ReadAsStringAsync());
            } catch { }
            return new JArray();
        }

        public static async Task<JObject> GetSteamGridDbArtworkAsync(string gameName)
        {
            var result = new JObject();
            try
            {
                var apiKey = AppConfig.GetString("steamgriddb_key");
                if (string.IsNullOrWhiteSpace(apiKey)) return result;

                string queryUrl = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameName)}";
                var searchRequest = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                searchRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

                var searchResp = await _httpClient.SendAsync(searchRequest);
                if (!searchResp.IsSuccessStatusCode) return result;

                var searchJson = await searchResp.Content.ReadAsStringAsync();
                var searchData = JObject.Parse(searchJson);
                var firstHit = searchData["data"]?.First;
                
                if (firstHit != null)
                {
                    string id = firstHit["id"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(id))
                    {
                        string gridsUrl = $"https://www.steamgriddb.com/api/v2/grids/game/{id}";
                        var gridReq = new HttpRequestMessage(HttpMethod.Get, gridsUrl);
                        gridReq.Headers.Add("Authorization", $"Bearer {apiKey}");
                        var gridResp = await _httpClient.SendAsync(gridReq);
                        
                        if (gridResp.IsSuccessStatusCode)
                        {
                            var gridsData = JObject.Parse(await gridResp.Content.ReadAsStringAsync());
                            result["grids"] = gridsData["data"];
                        }
                    }
                }
                return result;
            } catch { return result; }
        }

        public static async Task<JArray> SearchCinemetaAsync(string query, string type = "movie")
        {
            try
            {
                string url = $"https://v3-cinemeta.strem.io/catalog/{type}/top/search={Uri.EscapeDataString(query)}.json";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = JObject.Parse(await response.Content.ReadAsStringAsync());
                    return data["metas"] as JArray ?? new JArray();
                }
            } catch { }
            return new JArray();
        }

        public static async Task<JObject> GetCinemetaDetailsAsync(string type, string id)
        {
            try
            {
                string url = $"https://v3-cinemeta.strem.io/meta/{type}/{id}.json";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = JObject.Parse(await response.Content.ReadAsStringAsync());
                    return data["meta"] as JObject ?? new JObject();
                }
            } catch { }
            return new JObject();
        }

        public static async Task<JArray> GetCinemetaCatalogAsync(string type, string catalog = "top", string? extra = null)
        {
            try
            {
                string url = $"https://v3-cinemeta.strem.io/catalog/{type}/{catalog}.json";
                if (!string.IsNullOrEmpty(extra)) url = $"https://v3-cinemeta.strem.io/catalog/{type}/{catalog}/{extra}.json";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = JObject.Parse(await response.Content.ReadAsStringAsync());
                    return data["metas"] as JArray ?? new JArray();
                }
            } catch { }
            return new JArray();
        }

        public static async Task<JObject?> GetRandomHeroMediaAsync()
        {
            var rand = new Random();
            bool fetchMovie = rand.Next(2) == 0;

            if (fetchMovie)
            {
                var movies = await GetCinemetaCatalogAsync("movie", "top");
                if (movies != null && movies.Count > 0)
                {
                    var item = movies[rand.Next(movies.Count)] as JObject;
                    if (item != null)
                    {
                        item["type"] = "movie";
                        return item;
                    }
                }
            }
            else
            {
                var games = await GetIgdbCatalogAsync();
                if (games != null && games.Count > 0)
                {
                    var item = games[rand.Next(games.Count)] as JObject;
                    if (item != null)
                    {
                        item["type"] = "game";
                        return item;
                    }
                }
            }
            return null;
        }
    }
}
