using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SONA.Services;

namespace SONA.Services
{
    public class LinkItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Category { get; set; } = "Other";
    }

    public static class LinksService
    {
        private static List<LinkItem> _links = new();
        private static readonly string LinksPath = Path.Combine(AppConfig.DataDir, "links.json");
        private static readonly string SourcesPath = @"c:\Users\LionGhost\Desktop\sources.txt";

        static LinksService()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(LinksPath))
                {
                    var json = File.ReadAllText(LinksPath);
                    _links = JsonConvert.DeserializeObject<List<LinkItem>>(json) ?? new();
                }
                else if (File.Exists(SourcesPath))
                {
                    ImportFromTxt();
                }
            }
            catch { _links = new(); }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LinksPath)!);
                File.WriteAllText(LinksPath, JsonConvert.SerializeObject(_links, Formatting.Indented));
            }
            catch { }
        }

        public static List<LinkItem> GetAll()
        {
            // Inject Hydra Sources if not present (to ensure they are there for the user)
            if (!_links.Any(l => l.Category == "Hydra Sources"))
            {
                _links.Add(new LinkItem { Title = "Hydra Download Sources", Url = "https://hydralauncher.com/sources", Category = "Hydra Sources" });
                _links.Add(new LinkItem { Title = "Hydra Themes", Url = "https://hydralauncher.com/themes", Category = "Hydra Sources" });
                Save();
            }
            return _links;
        }

        public static void AddLink(string title, string url, string category)
        {
            _links.Add(new LinkItem { Title = title, Url = url, Category = category });
            Save();
        }

        public static void RemoveLink(LinkItem item)
        {
            _links.Remove(item);
            Save();
        }

        private static void ImportFromTxt()
        {
            _links.Clear();
            var lines = File.ReadAllLines(SourcesPath);
            int lineNum = 1;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                string url = "";
                string title = "";
                
                if (line.Contains(": http"))
                {
                    var parts = line.Split(new[] { ": " }, 2, StringSplitOptions.None);
                    title = parts[0].Trim();
                    url = parts[1].Trim();
                }
                else
                {
                    url = line.Trim();
                    // Extract domain as title
                    try 
                    {
                        var uri = new Uri(url);
                        title = uri.Host.Replace("www.", "");
                    }
                    catch { title = url; }
                }

                string category = GetCategoryFromLine(lineNum);
                _links.Add(new LinkItem { Title = title, Url = url, Category = category });
                lineNum++;
            }
            Save();
        }

        private static string GetCategoryFromLine(int line)
        {
            if (line <= 13) return "Games";
            if (line <= 60) return "Movies & TV";
            if (line <= 78) return "Anime";
            if (line <= 94) return "Manga & Comics";
            if (line <= 97) return "Mobile Apps";
            if (line <= 106) return "Books & Audio";
            if (line <= 112) return "Software";
            if (line <= 117) return "AI Chat";
            if (line <= 129) return "Torrents";
            return "Other";
        }

        public static List<string> GetCategories()
        {
            var cats = _links.Select(l => l.Category).Distinct().ToList();
            if (!cats.Contains("All")) cats.Insert(0, "All");
            return cats;
        }
    }
}
