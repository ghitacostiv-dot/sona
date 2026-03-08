using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YoutubeExplode;
using YoutubeExplode.Search;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using SONA.Services;

namespace SONA
{
    public class HomePage : UserControl, IDisposable
    {
        private readonly Random _rnd = new();
        private DispatcherTimer? _clockTimer;
        private TextBlock? _clockText;
        private TextBlock? _dateText;
        private TextBlock? _weatherText;
        private WebView2? _hoverVideo; // Changed from MediaElement to WebView2
        private Border? _hoverVideoContainer;
        private TextBlock? _hoverVideoLabel;
        private bool _isHoverVideoPlaying = false;
        private string? _currentHoverCategory = null;
        private bool _isWebViewInitialized; // Added declaration
        private readonly YoutubeClient _yt = new();
        private System.Threading.CancellationTokenSource? _hoverCts;
        private bool _isPrefetching = false;

        // Cache for YouTube stream URLs per category key
        private readonly Dictionary<string, Uri> _ytCache = new();

        // YouTube search queries per category
        private static readonly Dictionary<string, string[]> YtSearchQueries = new()
        {
            ["tv"]      = new[] { "live news broadcast", "live TV channel stream", "breaking news live", "global news live stream" },
            ["stremio"] = new[] { "movie trailer 2024 official", "new movie trailer 2025", "blockbuster movie scenes 4k", "sci-fi movie trailer 2025", "thriller movie trailer official" },
            ["hydra"]   = new[] { "game trailer 2024 official", "new game trailer 2025", "AAA game cinematic trailer", "cyberpunk gaming trailer", "rpg game trailer 2025" },
            ["music"]   = new[] { "music video 2024 official", "popular music video", "trending music video", "top songs 2025 music video", "electronic music video 4k" },
            ["podcasts"]= new[] { "popular podcast episode clip", "best podcast moments", "lex fridman podcast clip", "joe rogan experience highlights" },
            ["hacking"] = new[] { "cybersecurity explained", "ethical hacking tutorial", "mr robot style hacking visual", "terminal hacking aesthetic", "cyber defense explained" },
            ["anime"]   = new[] { "anime trailer 2024", "new anime trailer 2025", "official anime cinematic", "trending anime trailer", "upcoming anime 2025" },
            ["radio"]   = new[] { "live radio broadcast", "radio studio setup", "on air radio station" },
            ["audiobooks"] = new[] { "audiobook recommendation 2024", "best audiobooks to listen to", "storytelling audio recording", "popular fiction audiobook clip" },
            ["links"]   = new[] { "internet resource guide", "best websites 2024", "useful websites you didn't know existed" },
            ["books"]   = new[] { "book review 2024", "popular book recommendations", "reading aesthetic", "best books of the year" },
            ["manga"]   = new[] { "manga recommendation", "best manga to read 2024", "manga review", "top manga panels" },
            ["comics"]  = new[] { "comic book review", "marvel dc comics explained", "best comic storylines" },
            ["othergames"] = new[] { "indie game trailer 2024", "osu gameplay", "rhythm game trailer", "casual game trailer" },
            ["courses"] = new[] { "online learning platform", "programming course tutorial", "educational tutorial", "learn to code 2024" },
            ["brave"]   = new[] { "web browser features", "internet surfing aesthetic", "browser extension review" },
            ["systools"]= new[] { "pc optimization guide", "windows utilities", "system tool review", "pc monitoring software" }
        };

        // Video folders per section (standby / local fallback)
        private static readonly string VidsBase = @"C:\Users\LionGhost\Downloads\Vids";
        private static readonly string StandbyFolder = Path.Combine(VidsBase, "Standby");
        private static readonly Dictionary<string, string> VideoFolders = new()
        {
            ["hydra"]   = Path.Combine(VidsBase, "Gaming Tab"),
            ["stremio"] = Path.Combine(VidsBase, "Movies Tab"),
            ["music"]   = Path.Combine(VidsBase, "Music Tab"),
        };

        public HomePage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            var root = new Grid();

            // ── Main Layout ───────────────────────────────────────────────
            var mainGrid = new Grid { Margin = new Thickness(12, 0, 32, 24) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title Banner
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Categories + Video

            // ── MASSIVE TITLE BANNER (Restored static image) ──────────────
            var titleImg = new Image 
            { 
                Source = IconHelper.CreateSafe(new Uri("pack://application:,,,/Resources/GUI/Tittle.png")),
                Height = 600,
                HorizontalAlignment = HorizontalAlignment.Center,
                Stretch = Stretch.Uniform,
                Opacity = 0,
                Margin = new Thickness(1000, -150, 0, -140), // Shifted ~500px right from center
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };
            
            titleImg.MouseEnter += (s, e) => {
                var anim = new DoubleAnimation(1.03, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                titleImg.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                titleImg.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };
            titleImg.MouseLeave += (s, e) => {
                var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                titleImg.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                titleImg.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            var fadeLogo = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(150) };
            titleImg.BeginAnimation(UIElement.OpacityProperty, fadeLogo);

            Grid.SetRow(titleImg, 0);
            Grid.SetColumnSpan(titleImg, 2);
            mainGrid.Children.Add(titleImg);

            // ── Clock & Weather — TOP RIGHT CORNER (floating overlay) ────
            var clockBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 20, 10, 40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 160, 80, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 10, 20, 10),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(32, 12, 0, 0)
            };
            
            var clockStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            _clockText = new TextBlock { Foreground = Brushes.White, FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            _weatherText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromArgb(200, 160, 200, 255)),
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "Fetching Weather...",
                Cursor = Cursors.Hand, ToolTip = "Click to change location"
            };
            _weatherText.MouseLeftButtonDown += (_, _) => ShowLocationPicker();
            timeStack.Children.Add(_clockText);
            timeStack.Children.Add(_weatherText);
            
            _dateText = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            clockStack.Children.Add(timeStack);
            clockStack.Children.Add(new Border { Width = 1, Height = 28, Background = new SolidColorBrush(Color.FromArgb(80, 160, 80, 255)), Margin = new Thickness(0, 0, 16, 0) });
            clockStack.Children.Add(_dateText);
            clockBox.Child = clockStack;
            Panel.SetZIndex(clockBox, 10);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();
            _ = LoadWeatherAsync();
            _ = PrefetchYoutubeUrls();

            var clockAnim = new DoubleAnimation(0.6, 1.0, TimeSpan.FromSeconds(1.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            clockBox.BeginAnimation(UIElement.OpacityProperty, clockAnim);

            // ── Left Column (Categories) ──────────────────────────────────
            var categoriesScroll = new ScrollViewer 
            { 
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, 
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var categoriesStack = new StackPanel { Margin = new Thickness(0,0,10,0) };
            Grid.SetRow(categoriesScroll, 1);
            Grid.SetColumn(categoriesScroll, 0);

            categoriesStack.Children.Add(new TextBlock { Text = "Media", Foreground = Brushes.Gray, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 0, 0, 8) });
            
            var mediaItems = new List<(string Title, string Desc, string Color, string NavKey, string Icon, string? VideoFolder, string[]? YtQueries)>
            {
                ("📺 LIVE TV",   "Global channels",            "#3b82f6", "tv",       "nav/tv",       null,                    YtSearchQueries.GetValueOrDefault("tv")),
                ("🎬 CINEMA",    "Movies & TV Series",         "#3b82f6", "stremio",  "nav/movies",   VideoFolders.GetValueOrDefault("stremio"), YtSearchQueries.GetValueOrDefault("stremio")),
                ("🟪 NEXUS",     "All-in-one streaming",       "#e50914", "nexus",    "nav/movies",   VideoFolders.GetValueOrDefault("stremio"), YtSearchQueries.GetValueOrDefault("stremio")),
                ("🟣 ANIME",     "Watch Anime & Links",        "#a855f7", "anime",    "nav/anime",     null,                    YtSearchQueries.GetValueOrDefault("anime")),
                ("🎵 MUSIC",     "Spotify, YT & Local",        "#1db954", "music",    "nav/music",    VideoFolders.GetValueOrDefault("music"),   YtSearchQueries.GetValueOrDefault("music")),
                ("📻 RADIO",     "Live Radio Stations",        "#f59e0b", "radio",    "nav/radio",     null,                    YtSearchQueries.GetValueOrDefault("radio")),
                ("🎙 PODCASTS",  "Audio shows",                "#14b8a6", "podcasts", "nav/podcasts", null,                    YtSearchQueries.GetValueOrDefault("podcasts")),
                ("🎧 AUDIOBOOKS", "Listen to stories",         "#0ea5e9", "audiobooks","nav/audiobooks", null,           YtSearchQueries.GetValueOrDefault("audiobooks")),
                ("🔗 LINKS",      "Useful Resources",           "#7c3aed", "links",    "nav/browser",  null,                    YtSearchQueries.GetValueOrDefault("links")),
            };
            foreach (var item in mediaItems) categoriesStack.Children.Add(CreateCompactCategoryCard(item.Title, item.Desc, item.Color, item.NavKey, item.Icon, item.VideoFolder, item.YtQueries));

            categoriesStack.Children.Add(new TextBlock { Text = "Reading", Foreground = Brushes.Gray, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 16, 0, 8) });
            var readingItems = new List<(string Title, string Desc, string Color, string NavKey, string Icon, string? VideoFolder, string[]? YtQueries)>
            {
                ("📚 BOOKS",    "Read books (Librum)",         "#f43f5e", "books",    "nav/books", null,                  YtSearchQueries.GetValueOrDefault("books")),
                ("📖 MANGA",    "Read manga (Mangayomi)",      "#ec4899", "manga",    "nav/manga", null,                  YtSearchQueries.GetValueOrDefault("manga")),
                ("🦸‍♂ COMICS",   "Read comics (YACReader)",     "#ef4444", "comics",   "nav/manga", null,                 YtSearchQueries.GetValueOrDefault("comics")),
            };
            foreach (var item in readingItems) categoriesStack.Children.Add(CreateCompactCategoryCard(item.Title, item.Desc, item.Color, item.NavKey, item.Icon, item.VideoFolder, item.YtQueries));

            categoriesStack.Children.Add(new TextBlock { Text = "Gaming & Apps", Foreground = Brushes.Gray, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 16, 0, 8) });
            var gamingItems = new List<(string Title, string Desc, string Color, string NavKey, string Icon, string? VideoFolder, string[]? YtQueries)>
            {
                ("🎮 GAMES",     "Hydra & Library",            "#16c60c", "hydra",    "nav/games",    VideoFolders.GetValueOrDefault("hydra"),   YtSearchQueries.GetValueOrDefault("hydra")),
                ("🕹 OTHERS",    "osu! & More",                "#d946ef", "othergames","nav/games",   null,                      YtSearchQueries.GetValueOrDefault("othergames")),
                ("🏫 COURSES",   "Online Learning",            "#8b5cf6", "courses",  "nav/programs", null,               YtSearchQueries.GetValueOrDefault("courses")),
                ("🌐 BROWSER",   "Web Surfing",                "#f97316", "brave",    "nav/browser",  null,                      YtSearchQueries.GetValueOrDefault("brave")),
                ("🛠 TOOLS",     "Apollo, Moonlight & more",   "#4ade80", "systools", "nav/programs", null,                   YtSearchQueries.GetValueOrDefault("systools")),
                ("🛡 Toolkit",   "Hacking & Network",          "#a855f7", "hacking",  "nav/hacking",  null,                    YtSearchQueries.GetValueOrDefault("hacking")),
                ("🛍 PLAY",      "Google Play apps",           "#22c55e", "gplay",    "categories/apps", null,                YtSearchQueries.GetValueOrDefault("links")),
                ("🔌 INTEGRATIONS","External services",        "#06b6d4", "integrations","categories/programs", null,         YtSearchQueries.GetValueOrDefault("links")),
            };
            foreach (var item in gamingItems) categoriesStack.Children.Add(CreateCompactCategoryCard(item.Title, item.Desc, item.Color, item.NavKey, item.Icon, item.VideoFolder, item.YtQueries));
            
            categoriesStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 24) });

            categoriesScroll.Content = categoriesStack;
            mainGrid.Children.Add(categoriesScroll);

            // ── News Panel (Bottom Right Corner) ────────────────────────
            var newsPanel = new StackPanel 
            { 
                VerticalAlignment = VerticalAlignment.Bottom, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 48, 20),
                Width = 400
            };
            newsPanel.Children.Add(CreateNewsCarousel());
            root.Children.Add(newsPanel);
            Panel.SetZIndex(newsPanel, 10);

            // ── Hover Video Preview (WebView2 for HTML5 + CSS Override) ───
            _hoverVideo = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false
            };
            _hoverVideo.CoreWebView2InitializationCompleted += (s, ev) =>
            {
                if (!ev.IsSuccess) return;
                _hoverVideo.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                _hoverVideo.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                
                // Fine-tuning for high performance
                _hoverVideo.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
            };
            
            _ = InitializeHoverWebViewAsync();

            _hoverVideoContainer = new Border
            {
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                Background = Brushes.Black, // Solid black background for the player box
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), // Brighter border for "on top" feel
                BorderThickness = new Thickness(2),
                Width = 720,
                Height = 405, // Fixed 16:9 
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(360, 120, 0, 0), // Next to categories
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 4, BlurRadius = 20, Opacity = 0.6,
                    Color = Color.FromRgb(0, 0, 0)
                }
            };

            var videoGrid = new Grid();
            videoGrid.Children.Add(_hoverVideo);
            // RELAXED FILTER: No more dimmer border on top of the video
            _hoverVideoLabel = new TextBlock
            {
                Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(14, 0, 0, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { ShadowDepth = 0, BlurRadius = 10, Color = Colors.Black }
            };
            videoGrid.Children.Add(_hoverVideoLabel);
            _hoverVideoContainer.Child = videoGrid;

            // Add to ROOT grid (not mainGrid) so it's ON TOP of everything including background filters
            Panel.SetZIndex(_hoverVideoContainer, 20);

            root.Children.Add(mainGrid);
            root.Children.Add(clockBox);
            root.Children.Add(_hoverVideoContainer); // On top of everything
            Content = root;

            Unloaded += (_, _) => {
                _clockTimer?.Stop();
            };
        }

        public void Dispose()
        {
            try { _hoverVideo?.Dispose(); } catch { }
        }

        private async Task InitializeHoverWebViewAsync()
        {
            if (_hoverVideo == null || _isWebViewInitialized) return;
            try
            {
                await WebViewService.InitializeWebViewAsync(_hoverVideo);
                _isWebViewInitialized = true;
            }
            catch { }
        }

        // ── Weather Location Picker ───────────────────────────────────────
        private void ShowLocationPicker()
        {
            var popup = new Window
            {
                Title = "Set Weather Location",
                Width = 400, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1a)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 160, 80, 255)),
                BorderThickness = new Thickness(1), ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Weather Settings", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) });

            panel.Children.Add(new TextBlock { Text = "City or Location:", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 0, 0, 6) });
            var locationBox = new TextBox
            {
                Text = AppConfig.GetString("weather_location", "Slatina, Romania"),
                Height = 36, FontSize = 15,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2a)),
                Foreground = Brushes.White, BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 160, 80, 255)),
                CaretBrush = Brushes.White
            };
            panel.Children.Add(locationBox);

            panel.Children.Add(new TextBlock { Text = "OpenWeather API Key (Optional):", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 12, 0, 6) });
            var keyBox = new TextBox
            {
                Text = AppConfig.GetString("openweather_key", ""),
                Height = 36, FontSize = 15,
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x2a)),
                Foreground = Brushes.White, BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 160, 80, 255)),
                CaretBrush = Brushes.White
            };
            panel.Children.Add(keyBox);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("DarkBtn"), Height = 36, Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            cancelBtn.Click += (_, _) => popup.Close();
            var saveBtn = new Button { Content = "Save & Refresh", Style = (Style)Application.Current.FindResource("AccentBtn"), Height = 36, Width = 130, Background = new SolidColorBrush(Color.FromRgb(0xa0, 0x50, 0xff)) };
            saveBtn.Click += (_, _) =>
            {
                var loc = locationBox.Text.Trim();
                var key = keyBox.Text.Trim();
                AppConfig.Set("weather_location", loc);
                AppConfig.Set("openweather_key", key);
                if (_weatherText != null) _weatherText.Text = "Updating...";
                _ = LoadWeatherAsync();
                popup.Close();
            };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(saveBtn);
            panel.Children.Add(btnRow);
            popup.Height = 320; // Increase height for extra field
            popup.Content = panel;
            popup.ShowDialog();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            if (_clockText != null) _clockText.Text = now.ToString("HH:mm");
            if (_dateText != null) _dateText.Text = now.ToString("dddd\nd MMM yyyy").ToUpper();
        }

        private async Task LoadWeatherAsync()
        {
            try
            {
                string location = AppConfig.GetString("weather_location", "Slatina, Romania");
                
                // First, geocode the location to get Lat/Lon using a free service (Nominatim)
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "SONA-App");
                client.Timeout = TimeSpan.FromSeconds(5);

                string geoUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(location)}&format=json&limit=1";
                var geoResponse = await client.GetStringAsync(geoUrl);
                
                // Very basic manual JSON parse to avoid dependencies
                if (geoResponse.Contains("\"lat\":\"") && geoResponse.Contains("\"lon\":\""))
                {
                    int latStart = geoResponse.IndexOf("\"lat\":\"") + 7;
                    int latEnd = geoResponse.IndexOf("\"", latStart);
                    string lat = geoResponse.Substring(latStart, latEnd - latStart);

                    int lonStart = geoResponse.IndexOf("\"lon\":\"") + 7;
                    int lonEnd = geoResponse.IndexOf("\"", lonStart);
                    string lon = geoResponse.Substring(lonStart, lonEnd - lonStart);

                    // Now fetch weather from OpenWeatherMap (requires key, falling back to Open-Meteo for coordinates if key missing)
                    string owKey = AppConfig.GetString("openweather_key", "");
                    string weatherUrl;
                    if (!string.IsNullOrEmpty(owKey))
                    {
                        weatherUrl = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={owKey}&units=metric";
                    }
                    else
                    {
                        // Fallback to open-meteo if no key provided
                        weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true";
                    }

                    var weatherResponse = await client.GetStringAsync(weatherUrl);
                    var weatherData = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(weatherResponse);

                    string temp = "";
                    string icon = "🌡️";

                    if (weatherData?["main"] != null) // OpenWeather format
                    {
                        var main = weatherData["main"];
                        temp = Math.Round((double)(main?["temp"] ?? 0)).ToString();
                        
                        var weatherArr = weatherData["weather"] as Newtonsoft.Json.Linq.JArray;
                        if (weatherArr != null && weatherArr.Count > 0)
                        {
                            var cond = weatherArr[0];
                            string desc = (string)(cond?["description"] ?? "");
                            string mainCond = (string)(cond?["main"] ?? "");
                            icon = GetOpenWeatherIcon(mainCond, desc);
                        }
                    }
                    else if (weatherData?["current_weather"] != null) // Open-Meteo format
                    {
                        var current = weatherData["current_weather"];
                        temp = Math.Round((double)(current?["temperature"] ?? 0)).ToString();
                        var code = (string)(current?["weathercode"] ?? "0");
                        icon = GetWeatherIcon(code);
                    }
                    
                    if (!string.IsNullOrEmpty(temp))
                    {
                        Dispatcher.Invoke(() => { 
                            if (_weatherText != null) _weatherText.Text = $"{icon} {temp}°C · {location}"; 
                        });
                        return;
                    }
                }
                
                // Fallback to wttr.in if geocoding/open-meteo fails
                string fallbackUrl = $"https://wttr.in/{Uri.EscapeDataString(location)}?format=%c+%t";
                var fbResponse = await client.GetStringAsync(fallbackUrl);
                if (!string.IsNullOrWhiteSpace(fbResponse))
                {
                    Dispatcher.Invoke(() => { if (_weatherText != null) _weatherText.Text = $"{fbResponse.Trim()} · {location}"; });
                }
            }
            catch
            {
                Dispatcher.Invoke(() => { if (_weatherText != null) _weatherText.Text = "Weather Unavailable · Click to set location"; });
            }
        }

        // ── YouTube Hover Video (STABLE — caches per category) ────────────
        private async void EnterCategory(string categoryName, string navKey, string? localFolder, string[]? queries)
        {
            if (_hoverVideo == null || _hoverVideoContainer == null) return;
            if (_currentHoverCategory == navKey) return;
            
            _hoverCts?.Cancel();
            _hoverCts = new System.Threading.CancellationTokenSource();
            var token = _hoverCts.Token;

            _isHoverVideoPlaying = true;
            _currentHoverCategory = navKey;

            if (Application.Current.MainWindow is MainWindow mw)
            {
                // mw.PauseBackgroundVideo(); // Removed to allow background video to keep playing
            }

            // Hide the cursor if needed or reduce window opacity of other elements
            _hoverVideoContainer.Visibility = Visibility.Visible;
            if (_hoverVideoLabel != null) _hoverVideoLabel.Text = $"▶  {categoryName} — Loading...";
            AnimateOpacity(_hoverVideoContainer, 1.0, 400);

            // Animate opacity faster for "on top" feel
            AnimateOpacity(_hoverVideoContainer, 1.0, 250);

            // Try YouTube
            if (navKey == "radio")
            {
                try 
                {
                    using var client = new HttpClient();
                    var response = await client.GetStringAsync("https://de1.api.radio-browser.info/json/stations/topvote/20");
                    var stations = JsonConvert.DeserializeObject<List<RadioStation>>(response);
                    if (stations != null && stations.Count > 0 && _isHoverVideoPlaying && token.IsCancellationRequested == false)
                    {
                        var s = stations[_rnd.Next(stations.Count)];
                        var streamUrl = !string.IsNullOrEmpty(s.UrlResolved) ? s.UrlResolved : s.Url;
                        if (Application.Current.MainWindow is MainWindow stationMw)
                        {
                            stationMw.PlayAudio(streamUrl, s.Name, s.Country, s.Favicon);
                            stationMw.SetPlayerBarVisibility(true); // Show playbar on hover
                            if (_hoverVideoLabel != null) _hoverVideoLabel.Text = $"📻  Playing: {s.Name}";
                        }
                    }
                } 
                catch { }
                return;
            }

            if (queries != null && queries.Length > 0)
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    var query = queries[_rnd.Next(queries.Length)];
                    var results = new List<VideoSearchResult>();
                    await foreach (var result in _yt.Search.GetVideosAsync(query, token))
                    {
                        results.Add(result);
                        if (results.Count >= 10) break; // More results to pick from randomly
                    }

                    if (token.IsCancellationRequested) return;

                    if (results.Count > 0)
                    {
                        var video = results[_rnd.Next(results.Count)];
                        var manifest = await _yt.Videos.Streams.GetManifestAsync(video.Id, token);
                        var streamInfo = manifest.GetMuxedStreams()
                            .Where(s => s.Container.Name.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                            .Where(s => s.VideoQuality.MaxHeight <= 480) // 480p is enough for preview and faster to buffer
                            .OrderByDescending(s => s.VideoQuality.MaxHeight)
                            .FirstOrDefault();

                        if (streamInfo != null && !token.IsCancellationRequested)
                        {
                            var uri = new Uri(streamInfo.Url);
                             _ytCache[navKey] = uri; // Still cache it

                             Dispatcher.Invoke(() =>
                             {
                                 if (token.IsCancellationRequested || _hoverVideo == null) return;
                                 
                                 string html = $@"
                                    <!DOCTYPE html>
                                    <html>
                                    <head>
                                        <style>
                                            body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: black; }}
                                            video {{ width: 100%; height: 100%; object-fit: cover; opacity: 0; transition: opacity 0.5s; }}
                                            video.ready {{ opacity: 1; }}
                                        </style>
                                    </head>
                                    <body>
                                        <video id='player' autoplay loop playsinline>
                                            <source src='{uri}' type='video/mp4'>
                                        </video>
                                        <script>
                                            var v = document.getElementById('player');
                                            v.oncanplay = () => v.classList.add('ready');
                                            v.volume = 0.8;
                                            v.play();
                                        </script>
                                    </body>
                                    </html>";

                                 _hoverVideo.NavigateToString(html);
                                 if (_hoverVideoLabel != null) _hoverVideoLabel.Text = $"▶  {video.Title}";
                             });
                             return;
                        }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch { }
            }

            // Fallback to local
            if (_isHoverVideoPlaying && _currentHoverCategory == navKey)
                Dispatcher.Invoke(() => PlayLocalHoverVideo(localFolder, categoryName));
        }

        private async Task PrefetchYoutubeUrls()
        {
            if (_isPrefetching) return;
            _isPrefetching = true;

            foreach (var category in YtSearchQueries)
            {
                if (_ytCache.ContainsKey(category.Key)) continue;

                try
                {
                    var results = new List<VideoSearchResult>();
                    await foreach (var result in _yt.Search.GetVideosAsync(category.Value[0]))
                    {
                        results.Add(result);
                        if (results.Count >= 2) break;
                    }

                    if (results.Count > 0)
                    {
                        // Cache the first one immediately
                        var video = results[0];
                        var manifest = await _yt.Videos.Streams.GetManifestAsync(video.Id);
                        var streamInfo = manifest.GetMuxedStreams()
                            .Where(s => s.Container.Name.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                            .Where(s => s.VideoQuality.MaxHeight <= 480)
                            .OrderByDescending(s => s.VideoQuality.MaxHeight)
                            .FirstOrDefault();

                        if (streamInfo != null)
                        {
                            _ytCache[category.Key] = new Uri(streamInfo.Url);
                        }
                    }
                }
                catch { }
                await Task.Delay(500); // Gentle pre-fetching
            }
            _isPrefetching = false;
        }

        private void PlayLocalHoverVideo(string? videoFolder, string categoryName)
        {
            if (_hoverVideo == null || _hoverVideoContainer == null) return;
            if (string.IsNullOrEmpty(videoFolder) || !Directory.Exists(videoFolder))
            {
                if (Directory.Exists(StandbyFolder))
                    videoFolder = StandbyFolder;
                else return;
            }

            var files = Directory.GetFiles(videoFolder, "*.mp4");
            if (files.Length == 0) return;

            var file = files[_rnd.Next(files.Length)];
            try
            {
                // For local videos, we can still use WebView2 with a file URI or NavigateToString
                string html = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background: black; }}
                            video {{ width: 100%; height: 100%; object-fit: cover; }}
                        </style>
                    </head>
                    <body>
                        <video id='player' autoplay loop playsinline>
                            <source src='{new Uri(file).AbsoluteUri}' type='video/mp4'>
                        </video>
                        <script>
                            var v = document.getElementById('player');
                            v.volume = 0.8;
                            v.play();
                            // Random seek for variety as before
                            v.onloadedmetadata = () => {{
                                if (v.duration > 10) v.currentTime = Math.random() * (v.duration - 10);
                            }};
                        </script>
                    </body>
                    </html>";

                _hoverVideo.NavigateToString(html);
                if (_hoverVideoLabel != null) _hoverVideoLabel.Text = $"▶  {categoryName}";
                AnimateOpacity(_hoverVideoContainer, 1.0, 200);
            }
            catch { }
        }

        private void LeaveCategory()
        {
            _hoverCts?.Cancel();
            _isHoverVideoPlaying = false;
            _currentHoverCategory = null;
            if (_hoverVideo == null || _hoverVideoContainer == null) return;
            
            AnimateOpacity(_hoverVideoContainer, 0, 150);
            
            // Hide after fade
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            hideTimer.Tick += (s, e) => {
                _isHoverVideoPlaying = false;
                if (_hoverVideo != null)
                {
                    _hoverVideo.Source = new Uri("about:blank");
                }
                _hoverVideoContainer.Visibility = Visibility.Collapsed;
                _hoverVideoContainer.Opacity = 0;
            
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    // mw.ResumeBackgroundVideo(); // Removed to allow background video to keep playing
                }
                
                ((DispatcherTimer)s).Stop(); // Explicitly stop the timer so it doesn't run forever
            };
            hideTimer.Start();
            
            // Immediately clear the WebView to stop audio/video
            try { 
                _hoverVideo.NavigateToString("<html><body style='background:black'></body></html>"); 
                // Stop radio audio if it was playing
                // Hide playbar if not on Radio page
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.PlayerEngine.Stop();
                    mw.PlayerEngine.Source = null;
                    mw.SetPlayerBarVisibility(false);
                }
            } catch { }
        }

        private Border CreateCompactCategoryCard(string title, string desc, string colorStr, string navKey, string iconPath, string? videoFolder, string[]? ytQueries)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr)!;
            var accentBrush = new SolidColorBrush(color);

            var card = new Border
            {
                Height = 68,
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 150, 50, 255)),
                Background = new SolidColorBrush(Color.FromArgb(60, 26, 11, 46)),
                Cursor = Cursors.Hand, ClipToBounds = true
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) }); // Icon column
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stripe = new Border { Background = accentBrush, Opacity = 0.5 };
            Grid.SetColumn(stripe, 0); grid.Children.Add(stripe);

            var icon = IconHelper.Img(iconPath, 28);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(icon, 1); grid.Children.Add(icon);

            var contentStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
            Grid.SetColumn(contentStack, 2);
            contentStack.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,2), Tag = "title" });
            contentStack.Children.Add(new TextBlock { Text = desc, Foreground = new SolidColorBrush(Color.FromArgb(160, 255,255,255)), FontSize = 11, TextWrapping = TextWrapping.Wrap });
            grid.Children.Add(contentStack);
            
            var arrowBlock = new TextBlock { Text = "→", Foreground = accentBrush, FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0), Opacity = 0 };
            Grid.SetColumn(arrowBlock, 2); grid.Children.Add(arrowBlock);
            card.Child = grid;

            var titleText = (TextBlock)contentStack.Children[0];

            card.MouseEnter += (_, _) =>
            {
                SoundService.PlayHover();
                titleText.Foreground = accentBrush;
                AnimateColor(card, Border.BackgroundProperty, Color.FromArgb(120, color.R, color.G, color.B), 200);
                AnimateColor(card, Border.BorderBrushProperty, color, 200);
                AnimateOpacity(stripe, 1.0, 200);
                AnimateOpacity(arrowBlock, 1.0, 200);
                EnterCategory(title, navKey, videoFolder, ytQueries);
            };

            card.MouseLeave += (_, _) =>
            {
                titleText.Foreground = Brushes.White;
                AnimateColor(card, Border.BackgroundProperty, Color.FromArgb(60, 26, 11, 46), 200);
                AnimateColor(card, Border.BorderBrushProperty, Color.FromArgb(80, 150, 50, 255), 200);
                AnimateOpacity(stripe, 0.5, 200);
                AnimateOpacity(arrowBlock, 0, 200);
                LeaveCategory();
            };

            card.MouseLeftButtonDown += (_, _) => {
                LeaveCategory();
                if (navKey == "exit") Application.Current.Shutdown();
                else if (Application.Current.MainWindow is MainWindow mw) mw.Navigate(navKey);
            };
            
            return card;
        }

        private StackPanel CreateSuggestionSection(string header, string colorStr, (string title, string desc, string navKey)[] items)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            section.Children.Add(new TextBlock { Text = header, Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(colorStr), FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });

            var itemsGrid = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach(var item in items)
            {
                var card = new Border
                {
                    Width = 125, Height = 55,
                    Margin = new Thickness(0, 0, 8, 8),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ClipToBounds = true
                };
                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(8) };
                textStack.Children.Add(new TextBlock { Text = item.title, Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2), TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                textStack.Children.Add(new TextBlock { Text = item.desc, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 10, TextAlignment = TextAlignment.Center });
                card.Child = textStack;

                card.MouseEnter += (_, _) => { SoundService.PlayHover(); card.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)); card.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); };
                card.MouseLeave += (_, _) => { card.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)); card.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)); };
                card.MouseLeftButtonDown += (_, _) => { if (Application.Current.MainWindow is MainWindow mw) mw.Navigate(item.navKey); };
                itemsGrid.Children.Add(card);
            }
            section.Children.Add(itemsGrid);
            return section;
        }

        private static string GetJsonValue(string json, string key)
        {
            try
            {
                int start = json.IndexOf($"\"{key}\":") + key.Length + 3;
                int end = json.IndexOfAny(new[] { ',', '}', ']' }, start);
                if (end < 0) return "";
                return json.Substring(start, end - start).Trim('\"', ':', ' ');
            }
            catch { return ""; }
        }

        private static string GetWeatherIcon(string code)
        {
            return code switch
            {
                "0" => "☀️",
                "1" or "2" or "3" => "⛅",
                "45" or "48" => "🌫️",
                "51" or "53" or "55" => "🌦️",
                "61" or "63" or "65" => "🌧️",
                "71" or "73" or "75" => "❄️",
                "80" or "81" or "82" => "🚿",
                "95" or "96" or "99" => "⛈️",
                _ => "🌡️"
            };
        }

        private static string GetOpenWeatherIcon(string main, string desc)
        {
            main = main.ToLower();
            desc = desc.ToLower();
            if (main.Contains("cloud")) return "☁️";
            if (main.Contains("rain") || main.Contains("drizzle")) return "🌧️";
            if (main.Contains("thunder")) return "⛈️";
            if (main.Contains("snow")) return "❄️";
            if (main.Contains("clear")) return "☀️";
            if (main.Contains("mist") || main.Contains("fog")) return "🌫️";
            return "🌡️";
        }

        private static void AnimateColor(Border el, DependencyProperty prop, Color to, int ms)
        {
            if (el.GetValue(prop) is SolidColorBrush currentBrush)
            {
                var newBrush = currentBrush.Clone();
                if (!newBrush.IsFrozen)
                {
                    newBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)));
                    el.SetValue(prop, newBrush);
                }
            }
        }

        private UIElement CreateNewsCarousel()
        {
            var isMinimized = true;
            
            var root = new Border
            {
                Width = 400,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 160, 80, 255)),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Margin = new Thickness(0)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header Section
            var headerBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Padding = new Thickness(16, 12, 12, 12) };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerBorder.Child = headerGrid;

            var headerTitle = new TextBlock
            {
                Text = "⚡ LATEST NEWS",
                Foreground = Brushes.White,
                FontSize = 11, FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.8
            };
            headerGrid.Children.Add(headerTitle);

            var toggleBtn = new Button { Content = "▲", Style = (Style)Application.Current.FindResource("DarkBtn"), Width = 30, Height = 24, FontSize = 10 };
            Grid.SetColumn(toggleBtn, 1);
            headerGrid.Children.Add(toggleBtn);
            
            Grid.SetRow(headerBorder, 0); 
            grid.Children.Add(headerBorder);

            // Content Section
            var contentRoot = new Grid { Visibility = Visibility.Collapsed, Height = 340, Margin = new Thickness(16) };
            contentRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(contentRoot, 1);
            grid.Children.Add(contentRoot);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var newsTitle = new TextBlock { Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) };
            var newsDesc = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(180, 255,255,255)), FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxHeight = 200, TextTrimming = TextTrimming.CharacterEllipsis };
            infoStack.Children.Add(newsTitle);
            infoStack.Children.Add(newsDesc);
            contentRoot.Children.Add(infoStack);

            var controls = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var prevBtn = new Button { Content = "◀", Style = (Style)Application.Current.FindResource("DarkBtn"), Width = 36, Height = 36 };
            var nextBtn = new Button { Content = "▶", Style = (Style)Application.Current.FindResource("DarkBtn"), Width = 36, Height = 36 };
            Grid.SetColumn(prevBtn, 0); Grid.SetColumn(nextBtn, 2);
            controls.Children.Add(prevBtn); controls.Children.Add(nextBtn);
            
            Grid.SetRow(controls, 1);
            contentRoot.Children.Add(controls);

            List<(string Title, string Desc)> newsItems = new();
            int currentIndex = 0;

            void UpdateDisplay()
            {
                if (newsItems.Count == 0) return;
                var item = newsItems[currentIndex];
                newsTitle.Text = item.Title;
                newsDesc.Text = item.Desc;
            }

            toggleBtn.Click += (_, _) =>
            {
                isMinimized = !isMinimized;
                toggleBtn.Content = isMinimized ? "▲" : "▼";
                contentRoot.Visibility = isMinimized ? Visibility.Collapsed : Visibility.Visible;
                root.Height = isMinimized ? double.NaN : 400; // Auto height when collapsed
            };

            prevBtn.Click += (_, _) => { if (newsItems.Count > 0) { currentIndex = (currentIndex - 1 + newsItems.Count) % newsItems.Count; UpdateDisplay(); } };
            nextBtn.Click += (_, _) => { if (newsItems.Count > 0) { currentIndex = (currentIndex + 1) % newsItems.Count; UpdateDisplay(); } };

            async Task LoadNews()
            {
                try
                {
                    using var client = new HttpClient();
                    // Using Algolia HN API for detailed story data
                    var response = await client.GetStringAsync("https://hn.algolia.com/api/v1/search?tags=front_page&hitsPerPage=20");
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(response);
                    var hits = data?["hits"] as Newtonsoft.Json.Linq.JArray;
                    
                    if (hits != null)
                    {
                        foreach (var hit in hits)
                        {
                            string title = (string)(hit?["title"] ?? "");
                            string url = (string)(hit?["url"] ?? "");
                            string author = (string)(hit?["author"] ?? "");
                            int points = (int)(hit?["points"] ?? 0);
                            
                            // Mock description if no text exists
                            string desc = $"By {author} · {points} points · {new Uri(url).Host}";
                            newsItems.Add((title, desc));
                        }
                    }
                    if (newsItems.Count > 0) Dispatcher.Invoke(UpdateDisplay);
                    else Dispatcher.Invoke(() => { newsTitle.Text = "No news found."; });
                }
                catch { Dispatcher.Invoke(() => { newsTitle.Text = "Failed to load news."; }); }
            }

            _ = LoadNews();
            root.Child = grid;
            return root;
        }

        private static void AnimateOpacity(UIElement el, double to, int ms)
        {
            el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));
        }
    }
}
