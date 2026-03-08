using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SONA.Services;

namespace SONA
{
    public class SourcesPage : UserControl
    {
        private readonly string _category;
        private readonly MainWindow? _mainWindow;
        private List<SourceItem> _sources;
        private StackPanel _listPanel = new();
        private TextBox _filterBox = new();

        public SourcesPage(string category) : this(category, Application.Current.MainWindow as MainWindow) { }

        public SourcesPage(string category, MainWindow? mainWindow)
        {
            _category = category;
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            _sources = LoadSourcesForCategory(category);

            var root = new DockPanel();
            
            // THEMED HERO HEADER
            var (iconKey, name, accent, desc) = GetCategoryInfo(category);
            var accentColor = (Color)ColorConverter.ConvertFromString(accent);
            var accentBrush = new SolidColorBrush(accentColor);

            var header = new Border { Height = 160, Padding = new Thickness(32) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(accentColor, 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x14, 0x14, 0x14), 0.6));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            header.Background = grad;

            var headerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            
            // Header Row (Aligned Left)
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            
            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(IconHelper.Img(iconKey, 32));
            titleSp.Children.Add(new TextBlock { Text = $" {name}", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.Heavy, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 16, 0) });
            titleRow.Children.Add(titleSp);

            // Filter + Add button (Now following the title on the left)
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _filterBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 36, Width = 220,
                FontSize = 14,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(100, 0x2a, 0x2a, 0x2a))
            };
            _filterBox.TextChanged += (_, _) => RefreshCards();
            actionRow.Children.Add(_filterBox);

            var btnAdd = new Button { Content = "➕ Add", Style = (Style)Application.Current.FindResource("AccentBtn"), Background = accentBrush, Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(8, 0, 0, 0) };
            btnAdd.Click += (_, _) => AddSource();
            actionRow.Children.Add(btnAdd);

            titleRow.Children.Add(actionRow);

            headerStack.Children.Add(titleRow);
            headerStack.Children.Add(new TextBlock { Text = desc, Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), FontSize = 13, Margin = new Thickness(0, 8, 0, 0) });
            header.Child = headerStack;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // CONTENT
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainStack = new StackPanel { Margin = new Thickness(24) };

            if (_category == "torrents") mainStack.Children.Add(BuildQbtMagnetPanel(accentBrush));

            _listPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            mainStack.Children.Add(_listPanel);
            
            scroll.Content = mainStack;
            root.Children.Add(scroll);
            Content = root;
            RefreshCards();
        }

        private Border BuildQbtMagnetPanel(SolidColorBrush accent)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 24) };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Magnet Link Dispatcher", Foreground = accent, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
            var row = new DockPanel();
            var magnetBox = new TextBox { Style = (Style)Application.Current.FindResource("SearchBox"), Height = 36, Margin = new Thickness(0, 0, 12, 0), VerticalContentAlignment = VerticalAlignment.Center };
            var sendBtn = new Button { Content = "Send to qBittorrent", Style = (Style)Application.Current.FindResource("DarkBtn"), Height = 36, Width = 150 };
            sendBtn.Click += (_, _) => SendMagnetToQbittorrent(magnetBox.Text);
            DockPanel.SetDock(sendBtn, Dock.Right); row.Children.Add(sendBtn); row.Children.Add(magnetBox);
            panel.Children.Add(row); border.Child = panel; return border;
        }

        private void RefreshCards()
        {
            _listPanel.Children.Clear();
            var filter = _filterBox?.Text?.ToLower() ?? "";
            var filtered = string.IsNullOrEmpty(filter)
                ? _sources
                : _sources.Where(s => s.Name.ToLower().Contains(filter) || s.Description.ToLower().Contains(filter)).ToList();

            var (_, _, accent, _) = GetCategoryInfo(_category);
            var accentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent));

            var countLabel = new TextBlock { Text = $"{filtered.Count} sources available", Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0, 0, 0, 16) };
            _listPanel.Children.Add(countLabel);

            var wrap = new WrapPanel();
            foreach (var s in filtered) wrap.Children.Add(CreateSourceCard(s, accentBrush));
            _listPanel.Children.Add(wrap);
        }

        private Border CreateSourceCard(SourceItem source, SolidColorBrush accent)
        {
            var card = new Border { Width = 280, Height = 120, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 16, 16), Padding = new Thickness(16), Cursor = Cursors.Hand };
            card.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            card.BorderThickness = new Thickness(1);
            var dp = new DockPanel();

            // Icon
            var iconBorder = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(8), Background = accent, Margin = new Thickness(0, 0, 12, 0) };
            iconBorder.Child = new TextBlock { Text = source.Icon, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(iconBorder, Dock.Left);
            dp.Children.Add(iconBorder);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = source.Name, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
            info.Children.Add(new TextBlock { Text = source.Description, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            dp.Children.Add(info);
            
            card.Child = dp;
            card.MouseEnter += (_, _) => { card.SetResourceReference(Border.BackgroundProperty, "BtnHover"); card.BorderBrush = accent; };
            card.MouseLeave += (_, _) => { 
                card.SetResourceReference(Border.BackgroundProperty, "Bg3Brush"); 
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush"); 
            };
            card.MouseLeftButtonDown += (_, _) => _mainWindow?.NavigateToBrowserWithUrl(source.Url);
            return card;
        }

        private void AddSource()
        {
            var dlg = new AddSourceDialog();
            if (dlg.ShowDialog() == true && dlg.Result != null) { _sources.Add(dlg.Result); RefreshCards(); }
        }

        private static List<SourceItem> LoadSourcesForCategory(string category) => DefaultSources.All.TryGetValue(category, out var s) ? new List<SourceItem>(s) : new List<SourceItem>();

        private void SendMagnetToQbittorrent(string magnet) { /* Original Logic */ }

        private static (string IconKey, string Name, string AccentColor, string Description) GetCategoryInfo(string key) => key switch
        {
            "movies" => ("nav/movies", "Movies", "#e50914", "Stream and discover movies from multiple sources"),
            "music" => ("nav/music", "Music", "#1db954", "Music streaming and discovery"),
            "games" => ("nav/games", "Games", "#00c853", "Game sources, repacks, and mods"),
            "torrents" => ("nav/torrents", "Torrents", "#ef4444", "Torrent search engines and trackers"),
            "anime" => ("nav/anime", "Anime", "#a855f7", "Watch anime from the best streaming sites"),
            "manga" => ("nav/manga", "Manga", "#ec4899", "Manga readers, comics, and webtoons"),
            "radio" => ("nav/radio", "Radio", "#f59e0b", "Radio stations from around the world"),
            "books" => ("nav/books", "Books & eBooks", "#f59e0b", "Free books, audiobooks, and ebook libraries"),
            "podcasts" => ("nav/podcasts", "Podcasts", "#14b8a6", "Podcast platforms and directories"),
            "audiobooks" => ("nav/audiobooks", "Audiobooks", "#8b5cf6", "Free audiobook libraries and streaming"),
            "tv" => ("nav/tv", "Live TV", "#3b82f6", "Live TV channels and IPTV services"),
            "programs" => ("nav/programs", "Programs", "#6366f1", "Software downloads and package managers"),
            "ai" => ("nav/ai", "AI Chat", "#06b6d4", "AI assistants and chat platforms"),
            "recipes" => ("nav/recipes", "Recipes", "#f97316", "Cooking recipes and food inspiration"),
            "modapks" => ("nav/modapks", "Mod APKs", "#10b981", "Modded Android APKs and app stores"),
            "sounds" => ("nav/home", "Sound Effects", "#8b5cf6", "Sound effect libraries and sound buttons"),
            "social" => ("nav/home", "Social & Chat", "#5865f2", "Discord, WhatsApp, and social platforms"),
            _ => ("nav/home", key, "#6366f1", "Browse sources")
        };
    }

    public class AddSourceDialog : Window
    {
        public SourceItem? Result { get; private set; }
        private readonly TextBox _nameBox, _urlBox, _descBox;

        public AddSourceDialog()
        {
            Title = "Add Custom Source"; Width = 400; Height = 320; Background = new SolidColorBrush(Color.FromRgb(0x0f, 0x0f, 0x0f));
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
            AllowsTransparency = true; BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)); BorderThickness = new Thickness(1);

            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Add New Source", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });
            
            _nameBox = CreateField(panel, "Source Name");
            _urlBox = CreateField(panel, "Source URL");
            _descBox = CreateField(panel, "Short Description");

            var bts = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var add = new Button { Content = "Add Source", Style = (Style)Application.Current.FindResource("AccentBtn"), Height = 36, Background = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xf1)) };
            add.Click += (_, _) => { if (!string.IsNullOrEmpty(_nameBox.Text)) { Result = new SourceItem { Name = _nameBox.Text, Url = _urlBox.Text, Description = _descBox.Text, Icon = "🌐" }; DialogResult = true; } };
            DockPanel.SetDock(add, Dock.Right); bts.Children.Add(add);
            
            var cancel = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("DarkBtn"), Height = 36, Width = 100, Margin = new Thickness(0, 0, 12, 0) };
            cancel.Click += (_, _) => DialogResult = false;
            bts.Children.Add(cancel);
            panel.Children.Add(bts); Content = panel;
        }

        private TextBox CreateField(StackPanel p, string placeholder)
        {
            p.Children.Add(new TextBlock { Text = placeholder, Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
            var tb = new TextBox { Style = (Style)Application.Current.FindResource("SearchBox"), Height = 36, Margin = new Thickness(0, 0, 0, 16), VerticalContentAlignment = VerticalAlignment.Center };
            p.Children.Add(tb); return tb;
        }
    }
}
