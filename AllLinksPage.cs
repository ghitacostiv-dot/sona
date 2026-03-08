using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SONA.Services;

namespace SONA
{
    public class AllLinksPage : UserControl
    {
        private readonly MainWindow _mainWindow;
        private readonly TextBox _searchBox;
        private readonly WrapPanel _cardsPanel;
        private readonly WrapPanel _categoryTabs;
        private readonly List<SourceItem> _customSources = new();
        private string _activeCategory = "all";
        private const string CustomLinksFile = "custom_links.json";

        // Display names and accent colors per category
        private static readonly Dictionary<string, (string Name, string Icon, string Color)> CategoryMeta = new()
        {
            ["all"]        = ("All Links",       "🌐", "#6366f1"),
            ["movies"]     = ("Movies & TV",     "🎬", "#e50914"),
            ["music"]      = ("Music",           "🎵", "#1db954"),
            ["games"]      = ("Games",           "🎮", "#00c853"),
            ["anime"]      = ("Anime",           "🟣", "#a855f7"),
            ["manga"]      = ("Manga & Comics",  "📖", "#ec4899"),
            ["books"]      = ("Books & eBooks",  "📚", "#f59e0b"),
            ["torrents"]   = ("Torrents",        "🌊", "#ef4444"),
            ["tv"]         = ("Live TV",         "📺", "#3b82f6"),
            ["radio"]      = ("Radio",           "📻", "#f59e0b"),
            ["podcasts"]   = ("Podcasts",        "🎙", "#14b8a6"),
            ["audiobooks"] = ("Audiobooks",      "🎙", "#8b5cf6"),
            ["programs"]   = ("Programs",        "💾", "#6366f1"),
            ["modapks"]    = ("Mod APKs",        "📱", "#10b981"),
            ["ai"]         = ("AI Chat",         "🤖", "#06b6d4"),
            ["social"]     = ("Social & Chat",   "💬", "#5865f2"),
            ["recipes"]    = ("Recipes",         "🍳", "#f97316"),
            ["sounds"]     = ("Sounds",          "🔊", "#8b5cf6"),
        };

        public AllLinksPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            LoadCustomLinks();

            var root = new DockPanel();

            // ─── HERO HEADER ───────────────────────────────────────────────
            var header = new Border { Height = 140, Padding = new Thickness(32) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x63, 0x66, 0xf1), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x14, 0x14, 0x14), 0.6));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            header.Background = grad;

            var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            var titleRow = new DockPanel();
            
            var titleSp = new StackPanel { Orientation = Orientation.Horizontal };
            titleSp.Children.Add(new TextBlock { Text = "🔗", FontSize = 28, VerticalAlignment = VerticalAlignment.Center });
            titleSp.Children.Add(new TextBlock { Text = " All Links", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.Heavy, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            titleRow.Children.Add(titleSp);

            // Search box
            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 36, Width = 280,
                FontSize = 14,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(100, 0x2a, 0x2a, 0x2a))
            };
            _searchBox.TextChanged += (_, _) => RefreshCards();
            DockPanel.SetDock(_searchBox, Dock.Right);
            titleRow.Children.Insert(0, _searchBox);

            headerStack.Children.Add(titleRow);
            
            var totalCount = DefaultSources.All.Values.Sum(v => v.Count) + _customSources.Count;
            headerStack.Children.Add(new TextBlock { Text = $"{totalCount} links across {CategoryMeta.Count - 1} categories — sorted by type", Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), FontSize = 13, Margin = new Thickness(0, 8, 0, 0) });

            // Add Link Button
            var btnAdd = new Button 
            { 
                Content = "➕ Add Link", 
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Height = 36, Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 12, 0)
            };
            btnAdd.Click += (_, _) => ShowAddLinkDialog();
            DockPanel.SetDock(btnAdd, Dock.Right);
            titleRow.Children.Insert(0, btnAdd);

            header.Child = headerStack;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // ─── CATEGORY TABS ─────────────────────────────────────────────
            _categoryTabs = new WrapPanel { Margin = new Thickness(24, 8, 24, 8) };
            
            foreach (var kvp in CategoryMeta)
            {
                var key = kvp.Key;
                var meta = kvp.Value;
                var tabColor = (Color)ColorConverter.ConvertFromString(meta.Color);
                
                var tab = new Button
                {
                    Height = 32,
                    Padding = new Thickness(16, 0, 16, 0),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand,
                    Background = key == "all" ? new SolidColorBrush(tabColor) : new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Tag = key
                };

                var tabContent = new StackPanel { Orientation = Orientation.Horizontal };
                tabContent.Children.Add(new TextBlock { Text = meta.Icon, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
                tabContent.Children.Add(new TextBlock { Text = $" {meta.Name}", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
                tab.Content = tabContent;

                tab.Click += (s, _) =>
                {
                    _activeCategory = key;
                    // Update tab visuals
                    foreach (Button b in _categoryTabs.Children)
                    {
                        var bKey = (string)b.Tag;
                        var bMeta = CategoryMeta.ContainsKey(bKey) ? CategoryMeta[bKey] : CategoryMeta["all"];
                        var bColor = (Color)ColorConverter.ConvertFromString(bMeta.Color);
                        b.Background = bKey == _activeCategory ? new SolidColorBrush(bColor) : new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e));
                    }
                    RefreshCards();
                };

                _categoryTabs.Children.Add(tab);
            }
            
            DockPanel.SetDock(_categoryTabs, Dock.Top);
            root.Children.Add(_categoryTabs);

            // ─── CARDS AREA ────────────────────────────────────────────────
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) };
            _cardsPanel = new WrapPanel { Margin = new Thickness(24, 16, 24, 24) };
            scroll.Content = _cardsPanel;
            root.Children.Add(scroll);

            Content = root;
            RefreshCards();
        }

        private void RefreshCards()
        {
            _cardsPanel.Children.Clear();
            var filter = _searchBox?.Text?.Trim().ToLower() ?? "";
            
            // Gather sources to display
            var categoriesToShow = _activeCategory == "all" 
                ? DefaultSources.All.Keys.ToList()
                : new List<string> { _activeCategory };

            foreach (var cat in categoriesToShow)
            {
                if (!DefaultSources.All.ContainsKey(cat)) continue;
                var sources = DefaultSources.All[cat];
                
                var filtered = string.IsNullOrEmpty(filter) 
                    ? sources 
                    : sources.Where(s => s.Name.ToLower().Contains(filter) || s.Description.ToLower().Contains(filter) || s.Url.ToLower().Contains(filter)).ToList();
                
                if (filtered.Count == 0) continue;

                var meta = CategoryMeta.ContainsKey(cat) ? CategoryMeta[cat] : (Name: "Other", Icon: "🌐", Color: "#6366f1");
                var accentColor = (Color)ColorConverter.ConvertFromString(meta.Color);

                // Add section header when showing "all"
                if (_activeCategory == "all")
                {
                    var sectionHeader = new Border { Width = 1200, Margin = new Thickness(0, 16, 0, 8) };
                    var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
                    headerSp.Children.Add(new TextBlock { Text = meta.Icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
                    headerSp.Children.Add(new TextBlock { Text = $" {meta.Name}", Foreground = new SolidColorBrush(accentColor), FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                    headerSp.Children.Add(new TextBlock { Text = $"  ({filtered.Count})", Foreground = Brushes.Gray, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
                    sectionHeader.Child = headerSp;
                    _cardsPanel.Children.Add(sectionHeader);
                }

                foreach (var s in filtered)
                {
                    _cardsPanel.Children.Add(CreateLinkCard(s, accentColor));
                }

                // Add custom sources for this category
                var customFiltered = _customSources.Where(s => (_activeCategory == "all" || s.Description.StartsWith($"[{cat}]")) &&
                    (string.IsNullOrEmpty(filter) || s.Name.ToLower().Contains(filter) || s.Url.ToLower().Contains(filter)));
                
                foreach (var s in customFiltered)
                {
                    _cardsPanel.Children.Add(CreateLinkCard(s, accentColor));
                }
            }

            if (_cardsPanel.Children.Count == 0)
            {
                _cardsPanel.Children.Add(new TextBlock { Text = "No links found matching your search.", Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0, 32, 0, 0) });
            }
        }

        private Border CreateLinkCard(SourceItem source, Color accent)
        {
            var card = new Border
            {
                Width = 260, Height = 90,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(14),
                Cursor = Cursors.Hand
            };
            card.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var dp = new DockPanel();

            // Left accent icon
            var iconBorder = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = new TextBlock
            {
                Text = source.Icon,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(iconBorder, Dock.Left);
            dp.Children.Add(iconBorder);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = source.Name,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 170
            });
            info.Children.Add(new TextBlock
            {
                Text = source.Description,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 170,
                Margin = new Thickness(0, 2, 0, 0)
            });
            // Show domain
            try
            {
                var uri = new Uri(source.Url);
                info.Children.Add(new TextBlock
                {
                    Text = uri.Host,
                    Foreground = new SolidColorBrush(accent),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            catch { }

            dp.Children.Add(info);
            card.Child = dp;

            var accentBrush = new SolidColorBrush(accent);
            card.MouseEnter += (_, _) => { card.SetResourceReference(Border.BackgroundProperty, "BtnHover"); card.BorderBrush = accentBrush; };
            card.MouseLeave += (_, _) => { 
                card.SetResourceReference(Border.BackgroundProperty, "Bg3Brush"); 
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush"); 
            };
            card.MouseLeftButtonDown += (_, _) => _mainWindow.NavigateToBrowserWithUrl(source.Url);

            return card;
        }

        private void ShowAddLinkDialog()
        {
            var dlg = new AddSourceDialog();
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                // We use description to store the category mapping for custom links in this simple implementation
                dlg.Result.Description = $"[{(_activeCategory == "all" ? "movies" : _activeCategory)}] {dlg.Result.Description}";
                _customSources.Add(dlg.Result);
                SaveCustomLinks();
                RefreshCards();
            }
        }

        private void LoadCustomLinks()
        {
            try
            {
                if (System.IO.File.Exists(CustomLinksFile))
                {
                    var json = System.IO.File.ReadAllText(CustomLinksFile);
                    var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SourceItem>>(json);
                    if (list != null) _customSources.AddRange(list);
                }
            }
            catch { }
        }

        private void SaveCustomLinks()
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_customSources);
                System.IO.File.WriteAllText(CustomLinksFile, json);
            }
            catch { }
        }
    }
}
