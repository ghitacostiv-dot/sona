using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SONA.Services;

namespace SONA
{
    public class RadioStation
    {
        [JsonProperty("stationuuid")] public string StationUuid { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("url")] public string Url { get; set; } = "";
        [JsonProperty("url_resolved")] public string UrlResolved { get; set; } = "";
        [JsonProperty("country")] public string Country { get; set; } = "";
        [JsonProperty("countrycode")] public string CountryCode { get; set; } = "";
        [JsonProperty("language")] public string Language { get; set; } = "";
        [JsonProperty("tags")] public string Tags { get; set; } = "";
        [JsonProperty("favicon")] public string Favicon { get; set; } = "";
        [JsonProperty("bitrate")] public int Bitrate { get; set; }
        [JsonProperty("votes")] public int Votes { get; set; }
    }

    public class RadioPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly ContentControl _contentArea = new();
        private TextBox _searchBox = new();
        private Border _loadingOverlay = new();
        private RadioButton _tabHome = new();
        private RadioButton _tabSearch = new();
        private RadioButton _tabFavorites = new();
        private RadioButton _tabHarmony = new();
        private StackPanel _resultsPanel = new();
        private bool _isPlayingRadio = false;

        private readonly List<RadioStation> _favorites = new();
        private readonly string _favoritesPath = Path.Combine(AppConfig.DataDir, "radio_favorites.json");
        private static readonly HttpClient _http = new();
        private const string API_BASE = "https://de1.api.radio-browser.info/json";

        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0xf5, 0x9e, 0x0b)); // Amber

        public RadioPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            LoadFavorites();
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftDock = new DockPanel();

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 24, 0, 0) };
            var icon = IconHelper.Img("nav/radio", 44);
            icon.Margin = new Thickness(0, 0, 16, 0);
            headerStack.Children.Add(icon);

            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 12, 24, 0) };
            _tabHome = CreateTabButton("📻 Browse", true);
            _tabSearch = CreateTabButton("🔍 Search", false);
            _tabFavorites = CreateTabButton("❤ Favorites", false);
            _tabHarmony = CreateTabButton("🎵 Harmony", false);
            
            tabs.Children.Add(_tabHome);
            tabs.Children.Add(_tabSearch);
            tabs.Children.Add(_tabFavorites);
            tabs.Children.Add(_tabHarmony);

            _tabHome.Checked += (_, _) => { if (IsLoaded) ShowBrowseView(); };
            _tabSearch.Checked += (_, _) => { if (IsLoaded) ShowSearchView(); };
            _tabFavorites.Checked += (_, _) => { if (IsLoaded) ShowFavoritesView(); };
            _tabHarmony.Checked += async (_, _) => { if (IsLoaded) await ShowHarmonyView(); };

            leftDock.Children.Add(headerStack);
            DockPanel.SetDock(headerStack, Dock.Top);
            leftDock.Children.Add(tabs);
            DockPanel.SetDock(tabs, Dock.Top);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _contentArea;
            leftDock.Children.Add(scroll);

            rootGrid.Children.Add(leftDock);

            _loadingOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock { Text = "Tuning in...", Foreground = Brushes.White, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            rootGrid.Children.Add(_loadingOverlay);

            Content = rootGrid;

            _tabHome.Checked += (_, _) => { if (IsLoaded) ShowBrowseView(); };
            _tabSearch.Checked += (_, _) => { if (IsLoaded) ShowSearchView(); };
            _tabFavorites.Checked += (_, _) => { if (IsLoaded) ShowFavoritesView(); };

            ShowBrowseView();
        }

        private RadioButton CreateTabButton(string text, bool isActive)
        {
            return new RadioButton
            {
                Content = text,
                Style = (Style)Application.Current.FindResource("PageTab"),
                GroupName = "RadioTabs",
                IsChecked = isActive
            };
        }

        // --- BROWSE VIEW ---
        private void ShowBrowseView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };

            // Hero header
            var heroGrad = new Border { Height = 160, Padding = new Thickness(32), Margin = new Thickness(-24, -24, -24, 24) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0xf5, 0x9e, 0x0b), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x92, 0x40, 0x0e), 0.5));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            heroGrad.Background = grad;
            var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            heroStack.Children.Add(new TextBlock { Text = "📻 Radio", Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeights.Heavy });
            heroStack.Children.Add(new TextBlock { Text = "Thousands of stations worldwide • Powered by Radio Browser", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            heroGrad.Child = heroStack;
            mainStack.Children.Add(heroGrad);

            // Country Quick-Access
            mainStack.Children.Add(new TextBlock { Text = "Browse by Country", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) });
            var countryGrid = new UniformGrid { Columns = 4 };
            var countries = new[] {
                ("US", "United States"), ("GB", "United Kingdom"), ("DE", "Germany"),
                ("FR", "France"), ("JP", "Japan"), ("BR", "Brazil"),
                ("RO", "Romania"), ("IN", "India"), ("ES", "Spain"),
                ("IT", "Italy"), ("CA", "Canada"), ("AU", "Australia")
            };
            foreach (var (code, name) in countries)
            {
                var card = CreateCountryCard(CountryFlags.Get(code), name, code);
                countryGrid.Children.Add(card);
            }
            mainStack.Children.Add(countryGrid);

            // Top Voted Stations
            mainStack.Children.Add(new TextBlock { Text = "Top Voted Stations", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 32, 0, 16) });
            var topPanel = new StackPanel();
            mainStack.Children.Add(topPanel);
            _ = LoadTopStationsAsync(topPanel);

            _contentArea.Content = mainStack;
        }

        private Border CreateCountryCard(string flag, string name, string code)
        {
            var card = new Border
            {
                Height = 56, Margin = new Thickness(4), CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a)),
                Cursor = Cursors.Hand, Padding = new Thickness(12, 0, 12, 0)
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock { Text = flag, FontSize = 22, Margin = new Thickness(0, 0, 10, 0) });
            sp.Children.Add(new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            card.Child = sp;
            card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(0x28, 0x28, 0x28));
            card.MouseLeave += (_, _) => card.Background = new SolidColorBrush(Color.FromRgb(0x1a, 0x1a, 0x1a));
            card.MouseLeftButtonDown += async (_, _) => await SearchByCountry(code, name);
            return card;
        }

        private async Task LoadTopStationsAsync(StackPanel panel)
        {
            try
            {
                var response = await _http.GetStringAsync($"{API_BASE}/stations/topvote/20");
                var stations = JsonConvert.DeserializeObject<List<RadioStation>>(response) ?? new();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var s in stations) panel.Children.Add(BuildStationCard(s));
                });
            }
            catch { }
        }

        private async Task SearchByCountry(string code, string name)
        {
            _loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var response = await _http.GetStringAsync($"{API_BASE}/stations/bycountrycodeexact/{code}?limit=50&order=votes&reverse=true");
                var stations = JsonConvert.DeserializeObject<List<RadioStation>>(response) ?? new();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainStack = new StackPanel { Margin = new Thickness(24) };
                    var backBtn = new Button { Content = "← Back to Browse", Style = (Style)Application.Current.FindResource("DarkBtn"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
                    backBtn.Click += (_, _) => ShowBrowseView();
                    mainStack.Children.Add(backBtn);
                    mainStack.Children.Add(new TextBlock { Text = $"Radio Stations — {name}", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });
                    foreach (var s in stations) mainStack.Children.Add(BuildStationCard(s));
                    _contentArea.Content = mainStack;
                });
            }
            catch { }
            finally { _loadingOverlay.Visibility = Visibility.Collapsed; }
        }

        // --- SEARCH VIEW ---
        private void ShowSearchView()
        {
            var mainStack = new StackPanel();

            var border = new Border { Height = 180, Padding = new Thickness(32) };
            var searchGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            searchGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0x92, 0x40, 0x0e), 0.0));
            searchGrad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            border.Background = searchGrad;

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(new TextBlock { Text = "Search Radio Stations", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.Heavy });

            var searchRow = new DockPanel { Margin = new Thickness(0, 16, 0, 0), MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left };
            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 44, Width = 400, FontSize = 16,
                Padding = new Thickness(15, 0, 15, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a))
            };
            _searchBox.KeyDown += async (s, e) => { if (e.Key == Key.Enter) await DoSearch(_searchBox.Text); };

            var goBtn = new Button { Content = "🔍", Style = (Style)Application.Current.FindResource("AccentBtn"), Width = 40, Height = 40, Margin = new Thickness(8, 0, 0, 0), Background = _accentBrush };
            goBtn.Click += async (_, _) => await DoSearch(_searchBox.Text);
            DockPanel.SetDock(goBtn, Dock.Right);
            searchRow.Children.Add(goBtn);
            searchRow.Children.Add(_searchBox);
            stack.Children.Add(searchRow);
            border.Child = stack;
            mainStack.Children.Add(border);

            _resultsPanel = new StackPanel { Margin = new Thickness(24) };
            if (_resultsPanel.Parent is Panel p) p.Children.Remove(_resultsPanel);
            mainStack.Children.Add(_resultsPanel);

            _contentArea.Content = mainStack;
        }

        private async Task DoSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            _loadingOverlay.Visibility = Visibility.Visible;
            _resultsPanel.Children.Clear();

            try
            {
                var response = await _http.GetStringAsync($"{API_BASE}/stations/byname/{Uri.EscapeDataString(query)}?limit=50&order=votes&reverse=true");
                var stations = JsonConvert.DeserializeObject<List<RadioStation>>(response) ?? new();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (stations.Count == 0)
                    {
                        _resultsPanel.Children.Add(new TextBlock { Text = "No stations found.", Foreground = Brushes.Gray, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                        return;
                    }
                    foreach (var s in stations) _resultsPanel.Children.Add(BuildStationCard(s));
                });
            }
            catch { }
            finally { _loadingOverlay.Visibility = Visibility.Collapsed; }
        }

        // --- HARMONY VIEW ---
        private async Task ShowHarmonyView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };

            // Hero header
            var heroGrad = new Border { Height = 160, Padding = new Thickness(32), Margin = new Thickness(-24, -24, -24, 24) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x10, 0xb9, 0x81), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x05, 0x96, 0x69), 0.5));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            heroGrad.Background = grad;
            var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            heroStack.Children.Add(new TextBlock { Text = "🎵 Harmony Music", Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeights.Heavy });
            heroStack.Children.Add(new TextBlock { Text = "Integrated music streaming • Local library management", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            heroGrad.Child = heroStack;
            mainStack.Children.Add(heroGrad);

            // Check if Harmony is available
            if (!HarmonyService.IsRunning)
            {
                mainStack.Children.Add(new TextBlock { Text = "Starting Harmony Music service...", Foreground = Brushes.Gray, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                _contentArea.Content = mainStack;
                
                try
                {
                    await HarmonyService.StartAsync();
                    await ShowHarmonyView(); // Retry after starting
                    return;
                }
                catch
                {
                    mainStack.Children.Clear();
                    mainStack.Children.Add(new TextBlock { Text = "⚠️ Harmony Music is not available", Foreground = Brushes.OrangeRed, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
                    mainStack.Children.Add(new TextBlock { Text = "Make sure Harmony Music is installed in ~/Downloads/harmony-music", Foreground = Brushes.Gray, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                    _contentArea.Content = mainStack;
                    return;
                }
            }

            // Harmony is running - show embedded view
            var harmonyWebView = new Microsoft.Web.WebView2.Wpf.WebView2 { Source = new Uri("http://localhost:3000") };
            harmonyWebView.Margin = new Thickness(24);
            mainStack.Children.Add(harmonyWebView);
            _contentArea.Content = mainStack;
        }

        // --- FAVORITES VIEW ---
        private void ShowFavoritesView()
        {
            var mainStack = new StackPanel { Margin = new Thickness(24) };
            mainStack.Children.Add(new TextBlock { Text = "❤ Favorite Stations", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            if (_favorites.Count == 0)
            {
                mainStack.Children.Add(new TextBlock { Text = "No favorites yet. Click the ❤ icon on any station to add it here.", Foreground = Brushes.Gray, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) });
            }
            else
            {
                foreach (var s in _favorites) mainStack.Children.Add(BuildStationCard(s, isFavoriteView: true));
            }

            _contentArea.Content = mainStack;
        }

        // --- STATION CARD ---
        private Border BuildStationCard(RadioStation station, bool isFavoriteView = false)
        {
            var card = new Border
            {
                Height = 80, 
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 8), 
                Padding = new Thickness(12)
            };

            var dp = new DockPanel();

            // Right side: buttons
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var isFav = _favorites.Any(f => f.StationUuid == station.StationUuid);
            var favBtn = new Button
            {
                Content = isFav ? "❤" : "🤍",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                FontSize = 16, Cursor = Cursors.Hand, ToolTip = isFav ? "Remove from favorites" : "Add to favorites",
                Foreground = isFav ? new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)) : Brushes.White,
                Margin = new Thickness(0, 0, 8, 0)
            };
            favBtn.Click += (_, _) =>
            {
                if (_favorites.Any(f => f.StationUuid == station.StationUuid))
                    _favorites.RemoveAll(f => f.StationUuid == station.StationUuid);
                else
                    _favorites.Add(station);
                SaveFavorites();
                if (isFavoriteView) ShowFavoritesView();
            };
            rightStack.Children.Add(favBtn);

            var playBtn = new Button
            {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Margin = new Thickness(4, 0, 0, 0)
            };
            playBtn.Content = IconHelper.Img("player/play", 22);
            playBtn.Click += (_, _) => PlayStation(station);
            rightStack.Children.Add(playBtn);

            DockPanel.SetDock(rightStack, Dock.Right);
            dp.Children.Add(rightStack);

            // Left side: favicon + info
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal };

            var iconBorder = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(8), ClipToBounds = true };
            iconBorder.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
            if (!string.IsNullOrEmpty(station.Favicon))
            {
                try
                {
                    var img = new Image { Stretch = Stretch.UniformToFill };
                    img.Source = IconHelper.CreateSafe(new Uri(station.Favicon));
                    if (img.Source != null) iconBorder.Child = img;
                    else iconBorder.Child = new TextBlock { Text = "📻", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
                catch { iconBorder.Child = new TextBlock { Text = "📻", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
            }
            else
            {
                iconBorder.Child = new TextBlock { Text = "📻", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }
            leftStack.Children.Add(iconBorder);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            infoStack.Children.Add(new TextBlock { Text = station.Name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 500 });

            var subtitle = new List<string>();
            if (!string.IsNullOrEmpty(station.CountryCode)) subtitle.Add($"{CountryFlags.Get(station.CountryCode)} {station.Country}");
            else if (!string.IsNullOrEmpty(station.Country)) subtitle.Add($"🏳 {station.Country}");
            if (!string.IsNullOrEmpty(station.Language)) subtitle.Add(station.Language);
            if (station.Bitrate > 0) subtitle.Add($"{station.Bitrate} kbps");
            if (!string.IsNullOrEmpty(station.Tags))
            {
                var tags = station.Tags.Split(',').Take(3).Select(t => t.Trim()).Where(t => t.Length > 0);
                subtitle.AddRange(tags);
            }

            infoStack.Children.Add(new TextBlock { Text = string.Join(" • ", subtitle), Foreground = new SolidColorBrush(Color.FromRgb(0xb3, 0xb3, 0xb3)), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 500 });
            leftStack.Children.Add(infoStack);
            dp.Children.Add(leftStack);

            card.Child = dp;
            card.MouseEnter += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1a, 0x1a, 0x2e));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 59, 130, 246));
            };
            card.MouseLeave += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255));
            };

            return card;
        }

        private void PlayStation(RadioStation station)
        {
            var streamUrl = !string.IsNullOrEmpty(station.UrlResolved) ? station.UrlResolved : station.Url;
            if (string.IsNullOrEmpty(streamUrl))
            {
                MessageBox.Show("This station has no stream URL.", "SONA Radio", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _mainWindow.PlayAudio(streamUrl, station.Name, station.Country, station.Favicon);
            _isPlayingRadio = true;
        }

        public void Dispose()
        {
            if (_isPlayingRadio)
            {
                try
                {
                    _mainWindow.PlayerEngine.Stop();
                    _mainWindow.PlayerEngine.Source = null;
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.FindName("PlayerBar"); // Force visual update
                        var playerBar = mw.FindName("PlayerBar") as Border;
                        if (playerBar != null)
                        {
                            playerBar.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                catch { }
            }
        }

        private void SaveFavorites()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_favoritesPath)!);
                File.WriteAllText(_favoritesPath, JsonConvert.SerializeObject(_favorites, Formatting.Indented));
            }
            catch { }
        }

        private void LoadFavorites()
        {
            try
            {
                if (File.Exists(_favoritesPath))
                {
                    var json = File.ReadAllText(_favoritesPath);
                    var imported = JsonConvert.DeserializeObject<List<RadioStation>>(json);
                    if (imported != null) { _favorites.Clear(); _favorites.AddRange(imported); }
                }
            }
            catch { }
        }
    }
}
