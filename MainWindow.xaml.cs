using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SONA.Services;
using SONA.Pages;

namespace SONA
{
    public partial class MainWindow : Window
    {
        public const string AppVersion = "1.0.0";
        private string _currentPage = "home";
        private bool _isPlaying = false;
        private string _currentTitle = "";
        private DispatcherTimer _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private Dictionary<string, System.Windows.Controls.Primitives.ToggleButton> _navButtons = new();
        private readonly Dictionary<string, UserControl> _pageCache = new();
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private bool _suppressHistory = false;
        public string? PendingBrowserUrl { get; set; }
        private bool _isFullscreen = false;

        public void NavigateToBrowser(string url)
        {
            PendingBrowserUrl = url;
            Navigate("browser");
        }

        public void NavigateToEmbeddedApp(string appId)
        {
            _currentPage = appId;
            if (_pageCache.TryGetValue(appId, out var cachedPage))
            {
                PageHost.Content = cachedPage;
            }
            else
            {
                var newPage = new EmbeddedAppPage(appId, this);
                _pageCache[appId] = newPage;
                PageHost.Content = newPage;
            }
            ShowSidebar(true);
        }

        private DispatcherTimer _bgVideoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        private Random _rnd = new Random();
        private string? _currentBgVideoPath;
        private static readonly string VidsBase = @"C:\Users\LionGhost\Downloads\Vids";
        private static readonly string StandbyFolder = Path.Combine(VidsBase, "Standby");
        private static readonly string SidebarVidsFolder = Path.Combine(VidsBase, "SONA");

        private DispatcherTimer? _sidebarBannerSwapTimer;
        private int _currentSidebarBannerIndex = 0;
        private bool _sidebarAActive = true;
        private static readonly string[] SidebarBannerVideos = new[]
        {
            @"C:\Users\LionGhost\Downloads\Vids\SONA\sona-league-of-legends-moewalls-com.mp4",
            @"C:\Users\LionGhost\Downloads\Vids\SONA\gwen-league-of-legends-moewalls-com.mp4"
        };

        private bool _isDraggingSlider = false;

        public Func<Task>? OnNextTrack { get; set; }
        public Func<Task>? OnPrevTrack { get; set; }

        public bool IsStaging => false;
        public bool IsShuffleEnabled { get; set; } = false;
        public MediaElement PlayerEngine => AudioPlayer;

        private bool _bgAActive = true;
        private string? _nextBgVideoPath;

        public MainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += (s, e) =>
            {
                if (_isFullscreen && e.Key == Key.F11) { ToggleFullscreen(false); e.Handled = true; }
            };
            BuildNavigation();
            LoadBackground();

            // ── Monitor-aware sizing ──────────────────────────────────────
            ApplyMonitorSize();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
                Dispatcher.Invoke(ApplyMonitorSize);

            // Enable hardware acceleration
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

            Closing += (_, e) =>
            {
                HarmonyService.Stop();
                NexusService.Stop();
                try { ScraperService.Stop(); } catch { }
                try { TorrentHttpServer.Stop(); } catch { }
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= (_, _) => { };
                
                // Dispose cached pages at shutdown
                foreach (var page in _pageCache.Values)
                {
                    if (page is IDisposable disposable)
                        disposable.Dispose();
                }

                try
                {
                    foreach (var proc in Process.GetProcessesByName("msedgewebview2"))
                        try { proc.Kill(); } catch { }
                }
                catch { }
            };

            _statsTimer.Tick += (_, _) => UpdateStats();
            _statsTimer.Start();

            AudioPlayer.MediaEnded += async (_, _) =>
            {
                if (OnNextTrack != null) await OnNextTrack();
            };

            try { Icon = IconHelper.CreateSafe(new Uri("pack://application:,,,/SONA.ico")); } catch { }

            _bgVideoTimer.Tick += (_, _) => PlayRandomStandbyVideo();
            
            // Only loop if the player is still active (has a source assigned)
            BgVideoA.MediaEnded += (_, _) => { if (BgVideoA.Source != null) { BgVideoA.Position = TimeSpan.Zero; BgVideoA.Play(); } };
            BgVideoB.MediaEnded += (_, _) => { if (BgVideoB.Source != null) { BgVideoB.Position = TimeSpan.Zero; BgVideoB.Play(); } };
            
            BgVideoA.MediaFailed += (s, e) => Debug.WriteLine($"BgVideoA Error: {e.ErrorException?.Message}");
            BgVideoB.MediaFailed += (s, e) => Debug.WriteLine($"BgVideoB Error: {e.ErrorException?.Message}");

            InitSidebarVideos();
            
            // Allow launching directly into Nexus via --nexus command line arg
            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains("--nexus"))
                Navigate("nexus");
            else
                Navigate("home");

            UpdateHistoryButtons();
        }

        private void ApplyMonitorSize()
        {
            // Get the working area of the monitor where the window currently lives
            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle != IntPtr.Zero
                    ? new System.Windows.Interop.WindowInteropHelper(this).Handle
                    : System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle
            );

            // Convert from device pixels to WPF device-independent units
            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
            double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;

            double w = screen.WorkingArea.Width  / scaleX;
            double h = screen.WorkingArea.Height / scaleY;

            if (WindowState == WindowState.Normal)
            {
                Width  = w;
                Height = h;
                Left   = screen.WorkingArea.Left   / scaleX;
                Top    = screen.WorkingArea.Top     / scaleY;
            }

            // Always keep MaxWidth/MaxHeight in sync so maximising works correctly
            MaxWidth  = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
        }

        /// <summary>True OS fullscreen: covers entire screen (including taskbar), stays on top like a browser.</summary>
        public bool IsFullscreen => _isFullscreen;

        public void ToggleFullscreen(bool enter)
        {
            _isFullscreen = enter;
            if (enter)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Normal;
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Topmost = true;
                if (TitleBar != null) TitleBar.Visibility = Visibility.Collapsed;
                if (SidebarColumn != null) SidebarColumn.Width = new GridLength(0);
                // keep UI minimal; no fullscreen notice banner
            }
            else
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
                Topmost = false;
                if (TitleBar != null) TitleBar.Visibility = Visibility.Visible;
                if (SidebarColumn != null) SidebarColumn.Width = new GridLength(180);
                // ensure notice remains collapsed
            }
            UpdateVisualizerVisibility();
        }

        public void RefreshBackground() => LoadBackground();

        private void InitSidebarVideos()
        {
            if (SidebarVideoA == null || SidebarVideoB == null) return;

            // Only the active sidebar player loops; the inactive one is stopped with null source
            SidebarVideoA.MediaEnded += (s, e) => { if (_sidebarAActive) { SidebarVideoA.Position = TimeSpan.Zero; SidebarVideoA.Play(); } };
            SidebarVideoB.MediaEnded += (s, e) => { if (!_sidebarAActive) { SidebarVideoB.Position = TimeSpan.Zero; SidebarVideoB.Play(); } };

            StartSidebarVideo(SidebarVideoA, 0);
            // SidebarVideoB is intentionally left with no source until it is needed for the first swap
            _sidebarBannerSwapTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            _sidebarBannerSwapTimer.Tick += (_, _) => SwapSidebarVideo();
            _sidebarBannerSwapTimer.Start();
        }

        private void StartSidebarVideo(MediaElement el, int index)
        {
            if (index >= SidebarBannerVideos.Length) index = 0;
            var path = SidebarBannerVideos[index];
            if (!File.Exists(path)) return;
            el.Source = new Uri(path, UriKind.Absolute);
            el.Play();
        }

        public void SetBackgroundVideo(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            BgVideoA.Source = new Uri(path);
            BgVideoA.Play();
            BgVideoContainer.Opacity = 1;
        }

        public void PauseBackgroundVideo()
        {
            try
            {
                // Only pause the currently playing players (the others have null sources already)
                var activeBg = _bgAActive ? BgVideoA : BgVideoB;
                activeBg.Pause();
                var activeSidebar = _sidebarAActive ? SidebarVideoA : SidebarVideoB;
                activeSidebar.Pause();
                BgVideoContainer.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        public void ResumeBackgroundVideo()
        {
            try
            {
                BgVideoContainer.Visibility = Visibility.Visible;
                // Only resume the active player — the inactive one has a null source
                var activeBg = _bgAActive ? BgVideoA : BgVideoB;
                if (activeBg.Source != null) activeBg.Play();
                var activeSidebar = _sidebarAActive ? SidebarVideoA : SidebarVideoB;
                if (activeSidebar.Source != null) activeSidebar.Play();
            }
            catch { }
        }

        private void SwapSidebarVideo()
        {
            _currentSidebarBannerIndex = (_currentSidebarBannerIndex + 1) % SidebarBannerVideos.Length;

            if (_sidebarAActive)
            {
                // Load and fade in B, fade out A — then stop A
                StartSidebarVideo(SidebarVideoB, _currentSidebarBannerIndex);
                AnimateSidebarVideo(SidebarVideoB, 1.0, 1500);
                var fadeOutA = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(1500));
                fadeOutA.Completed += (_, _) => { try { SidebarVideoA.Stop(); SidebarVideoA.Source = null; } catch { } };
                SidebarVideoA.BeginAnimation(UIElement.OpacityProperty, fadeOutA);
            }
            else
            {
                // Load and fade in A, fade out B — then stop B
                StartSidebarVideo(SidebarVideoA, _currentSidebarBannerIndex);
                AnimateSidebarVideo(SidebarVideoA, 1.0, 1500);
                var fadeOutB = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(1500));
                fadeOutB.Completed += (_, _) => { try { SidebarVideoB.Stop(); SidebarVideoB.Source = null; } catch { } };
                SidebarVideoB.BeginAnimation(UIElement.OpacityProperty, fadeOutB);
            }
            _sidebarAActive = !_sidebarAActive;
        }

        private void AnimateSidebarVideo(UIElement el, double to, int ms)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            el.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void LoadBackground()
        {
            var bg = AppConfig.GetString("background_image", "");
            if (!string.IsNullOrEmpty(bg) && File.Exists(bg))
            {
                try { BgImage.Source = IconHelper.CreateSafe(new Uri(bg)); } catch { }
            }
        }

        private void BuildNavigation()
        {
            NavPanel.Children.Clear();
            _navButtons.Clear();

            foreach (var item in NavItems)
            {
                var btn = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Style = (Style)FindResource("NavBtn"),
                    Tag = item.Key,
                    IsChecked = item.Key == _currentPage
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var iconImg = IconHelper.Img(item.Icon, 20);
                if (item.Icon.StartsWith("GUI/"))
                {
                    iconImg.Width = 32;
                    iconImg.Height = 28;
                    iconImg.Stretch = Stretch.UniformToFill;
                    iconImg.Clip = new RectangleGeometry 
                    { 
                        Rect = new Rect(0, 0, 48, 28),
                        RadiusX = 4,
                        RadiusY = 4
                    };
                }
                sp.Children.Add(iconImg);
                sp.Children.Add(new TextBlock { Text = item.Label, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                
                btn.Content = sp;
                btn.Click += (s, e) => Navigate((string)((FrameworkElement)s).Tag);
                btn.MouseEnter += (s, e) => SoundService.PlayHover();

                var cm = new ContextMenu();
                cm.Items.Add(new MenuItem { Header = "Settings", Icon = new TextBlock { Text = "⚙️" } });
                ((MenuItem)cm.Items[0]).Click += (s, e) => Navigate("settings");
                
                var relaunchItem = new MenuItem { Header = "Relaunch App", Icon = new TextBlock { Text = "🔄" } };
                relaunchItem.Click += (s, e) => Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                relaunchItem.Click += (s, e) => Application.Current.Shutdown();
                cm.Items.Add(relaunchItem);

                var remapItem = new MenuItem { Header = "Remap App", Icon = new TextBlock { Text = "📍" } };
                remapItem.Click += (s, e) => Navigate("apps");
                cm.Items.Add(remapItem);

                cm.Items.Add(new MenuItem { Header = "Pin to Top", Icon = new TextBlock { Text = "📌" } });
                ((MenuItem)cm.Items[3]).Click += (s, e) => Topmost = !Topmost;

                cm.Items.Add(new MenuItem { Header = "Open New Window", Icon = new TextBlock { Text = "🪟" } });
                cm.Items.Add(new MenuItem { Header = "Check Updates", Icon = new TextBlock { Text = "✨" } });
                cm.Items.Add(new MenuItem { Header = "App Info", Icon = new TextBlock { Text = "ℹ️" } });

                btn.ContextMenu = cm;

                NavPanel.Children.Add(btn);
                _navButtons[item.Key] = btn;
            }
        }

        private void TopNavLeft_Click(object sender, RoutedEventArgs e)
        {
            var scroller = (ScrollViewer)FindName("TopNavScroll");
            if (scroller == null) return;
            double step = 220;
            scroller.ScrollToHorizontalOffset(Math.Max(0, scroller.HorizontalOffset - step));
        }

        private void TopNavRight_Click(object sender, RoutedEventArgs e)
        {
            var scroller = (ScrollViewer)FindName("TopNavScroll");
            if (scroller == null) return;
            double step = 220;
            scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset + step);
        }

        public void Navigate(string key)
        {
            // If navigating away from Nexus, completely destroy it as requested
            if (_currentPage == "nexus" || _currentPage == "nexus-anime")
            {
                if (_pageCache.TryGetValue(_currentPage, out var pageToRemove))
                {
                    if (pageToRemove is IDisposable d) try { d.Dispose(); } catch { }
                    _pageCache.Remove(_currentPage);
                }
            }
            else if (PageHost.Content is IDisposable disposablePage && !(_pageCache.ContainsValue((UserControl)PageHost.Content)))
            {
                // Only dispose if it's NOT in the cache (transient pages like VideoPlayerPage)
                try { disposablePage.Dispose(); } catch { }
            }

            if (_currentPage != key)
            {
                SoundService.PlaySwipe();
            }

            _currentPage = key;
            foreach (var kvp in _navButtons)
                kvp.Value.IsChecked = kvp.Key == key;
            if (!_suppressHistory)
            {
                if (_historyIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
                }
                if (_historyIndex == -1 || _history[_historyIndex] != key)
                {
                    _history.Add(key);
                    _historyIndex = _history.Count - 1;
                }
            }
            UpdateHistoryButtons();

            Title = $"SONA — {NavItems.FirstOrDefault(n => n.Key == key).Label ?? key}";

            // Global Background Videos logic
            if (key != "video" && key != "browser")
            {
                var folder = key switch
                {
                    "hydra" => Path.Combine(VidsBase, "Gaming Tab"),
                    "stremio" => Path.Combine(VidsBase, "Movies Tab"),
                    "music" => Path.Combine(VidsBase, "Music Tab"),
                    _ => StandbyFolder
                };
                PlayRandomVideoFromFolder(folder);
                _bgVideoTimer.Start();
            }
            else
            {
                _bgVideoTimer.Stop();
                FadeOutBgVideo();
            }

            var navItem = NavItems.FirstOrDefault(n => n.Key == key);

            if (navItem.IsWeb || key == "browser")
            {
                ShowSidebar(true);
                PlayerBar.Visibility = Visibility.Collapsed;
                if (key == "browser")
                {
                    PageHost.Visibility = Visibility.Visible;
                    if (_pageCache.TryGetValue("browser", out var cachedBrowser))
                    {
                        PageHost.Content = cachedBrowser;
                        if (!string.IsNullOrEmpty(PendingBrowserUrl))
                        {
                            ((BrowserHostPage)cachedBrowser).Navigate(PendingBrowserUrl);
                        }
                    }
                    else
                    {
                        var newBrowser = new BrowserHostPage(this, PendingBrowserUrl);
                        _pageCache["browser"] = newBrowser;
                        PageHost.Content = newBrowser;
                    }
                    ClearPendingBrowserUrl();
                    return;
                }
            }

            if (key == "video")
            {
                ShowSidebar(false);
                PlayerBar.Visibility = Visibility.Collapsed;
                return;
            }

            ShowSidebar(true);
            PlayerBar.Visibility = (key == "radio" && _isPlaying) ? Visibility.Visible : Visibility.Collapsed;

            var sourceCategories = new[] { "anime", "cartoons", "comedy", "sports", "xxx" };
            if (sourceCategories.Contains(key))
            {
                PageHost.Visibility = Visibility.Visible;
                PageHost.Content = new SourcesPage(key, this);
                return;
            }

            PageHost.Visibility = Visibility.Visible;

            if (_pageCache.TryGetValue(key, out var cachedPage))
            {
                PageHost.Content = cachedPage;
            }
            else
            {
                UserControl newPage = key switch
                {
                    "home" => new HomePage(),
                    "apps" => new InstalledAppsPage(),
                    "music" => new MusicPage(this),
                    "music-hub" => new MusicHubPage(this),
                    "harmony" => new HarmonyMusicPage(this),
                    "radio" => new RadioPage(this),
                    "tv" => new TvPage(this),
                    "hydra" => new HydraPage(this),
                    "movies" => new MoviesHubPage(this),
                    "stremio" => new StremioPage(this),
                    "nexus" => new NexusPage(this),
                    "nexus-anime" => new NexusPage(this, "/anime"),
                    "hacking" => new EthicalHackingPage(this),
                    "retro" => new RetroPage(),
                    "install" => new InstallPage(),
                    "debloat" => new DebloatPage(),
                    "settings" => new SettingsPage(this),
                    "links" => new LinksPage(this),
                    "library" => new LibraryPage(),

                    // New integrated app placeholders mapped directly
                    "manga" => new EmbeddedAppPage("mangayomi", this),
                    "books" => new EmbeddedAppPage("librum", this),
                    "comics" => new EmbeddedAppPage("yacreader", this),
                    "podcasts" => new EmbeddedAppPage("poddr", this),
                    "audiobooks" => new EmbeddedAppPage("audiobookshelf", this),
                    "courses" => new BrowserHostPage(this, "https://www.learnhouse.app/"),
                    "brave" => new EmbeddedAppPage("brave", this),
                    "gplay" => new EmbeddedAppPage("gplay", this),
                    "integrations" => new IntegrationsPage(),
                    "systools" => new SystemToolsPage(this),
                    "othergames" => new OtherGamesPage(this),
                    "archive" => new BrowserHostPage(this, "https://archive.org/"),

                    _ => (UserControl)CreatePlaceholder(key)
                };

                // Cache these pages to preserve state
                _pageCache[key] = newPage;
                PageHost.Content = newPage;
            }

            UpdateVisualizerVisibility();
        }

        public void UpdateVisualizerVisibility()
        {
            Dispatcher.Invoke(() => {
                bool enabled = AppConfig.GetBool("visualizer_enabled", true);
                bool correctPage = (_currentPage == "home" || _currentPage == "music");
                bool visible = enabled && correctPage && !_isFullscreen;

                if (visible)
                {
                    MainVisualizer.Visibility = Visibility.Visible;
                    MainVisualizer.Start();
                }
                else
                {
                    MainVisualizer.Visibility = Visibility.Collapsed;
                    MainVisualizer.Stop();
                }
            });
        }

        public void PlayVideo(string url, string title, ScrapeHeaders? headers = null)
        {
            Dispatcher.Invoke(() => {
                _isPlaying = true;
                _currentTitle = title;
                ShowSidebar(false);
                PlayerBar.Visibility = Visibility.Collapsed;
                PageHost.Content = new VideoPlayerPage(url, title, this, headers);
            });
        }

        public void SetPlayerBarVisibility(bool visible)
        {
            Dispatcher.Invoke(() => {
                PlayerBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public void PlayAudio(string url, string title, string artist, string art)
        {
            Dispatcher.Invoke(() => {
                try {
                    _isPlaying = true;
                    _currentTitle = title;
                    PlayerBar.Visibility = Visibility.Visible;
                    PlayerTitle.Text = title;
                    PlayerArtist.Text = artist;
                    try { PlayPauseIcon.Source = IconHelper.Img("player/pause", 18).Source; } catch { }
                    try { PlayerArt.Source = IconHelper.CreateSafe(new Uri(art)); } catch { }
                    
                    AudioPlayer.Source = null; // Clear first to avoid resource locks
                    AudioPlayer.Source = new Uri(url);
                    AudioPlayer.Play();
                } catch (Exception ex) {
                    Debug.WriteLine($"PlayAudio Error: {ex.Message}");
                }
            });
        }

        public void NavigateToBrowserWithUrl(string url)
        {
            SetPendingBrowserUrl(url);
            Navigate("browser");
        }

        public void SetPendingBrowserUrl(string url) => PendingBrowserUrl = url;
        public void ClearPendingBrowserUrl() => PendingBrowserUrl = null;

        private void ShowSidebar(bool show)
        {
            SidebarBorder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(PageHost, show ? 1 : 0);
            Grid.SetColumnSpan(PageHost, show ? 1 : 2);
            var restoreBtn = (Button)FindName("SidebarRestoreBtn");
            if (restoreBtn != null) restoreBtn.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            var hideBtn = (System.Windows.Controls.Primitives.ToggleButton)FindName("BtnSideHide");
            if (hideBtn != null) hideBtn.IsChecked = false;
            // Resize audio visualizer with layout
            Grid.SetColumn(MainVisualizer, show ? 1 : 0);
            Grid.SetColumnSpan(MainVisualizer, show ? 1 : 2);
        }

        private void UpdateStats()
        {
            StatsLabel.Text = DateTime.Now.ToString("HH:mm | ddd, MMM dd");

            if (_isPlaying && AudioPlayer.NaturalDuration.HasTimeSpan && !_isDraggingSlider)
            {
                ProgressSlider.Maximum = AudioPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                ProgressSlider.Value = AudioPlayer.Position.TotalSeconds;

                CurrentTimeLabel.Text = AudioPlayer.Position.ToString(@"m\:ss");
                TotalTimeLabel.Text = AudioPlayer.NaturalDuration.TimeSpan.ToString(@"m\:ss");
            }
        }

        private void PlayRandomVideoFromFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var files = Directory.GetFiles(folderPath, "*.mp4");
                if (files.Length > 0)
                {
                    string newVideo = _currentBgVideoPath ?? "";
                    if (files.Length > 1)
                    {
                        do { newVideo = files[_rnd.Next(files.Length)]; } while (newVideo == _currentBgVideoPath);
                    }
                    else { newVideo = files[0]; }
                    PlayBgVideo(newVideo);
                }
                else { FadeOutBgVideo(); }
            }
            else { FadeOutBgVideo(); }
        }

        private void PlayRandomStandbyVideo() => PlayRandomVideoFromFolder(StandbyFolder);

        private async void PlayBgVideo(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            if (_currentBgVideoPath == path) return;

            _currentBgVideoPath = path;

            var targetPlayer = _bgAActive ? BgVideoB : BgVideoA;
            var currentPlayer = _bgAActive ? BgVideoA : BgVideoB;

            // 1. Load and buffer in background — start the new player at opacity 0
            targetPlayer.Source = new Uri(path, UriKind.Absolute);
            targetPlayer.Opacity = 0;
            targetPlayer.Play();

            // Wait for the player to buffer a few frames before revealing it
            await Task.Delay(800);

            // 2. Crossfade: fade in the new player
            AnimateOpacity(targetPlayer, 1.0, 1200);

            // Fade out the old player, then STOP it so the GPU is released immediately
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(1200));
            fadeOut.Completed += (_, _) =>
            {
                try
                {
                    currentPlayer.Stop();
                    currentPlayer.Source = null; // Release decoder and GPU resources
                }
                catch { }
            };
            currentPlayer.BeginAnimation(UIElement.OpacityProperty, fadeOut);

            _bgAActive = !_bgAActive;

            if (BgVideoContainer.Opacity < 0.1) AnimateOpacity(BgVideoContainer, 0.45, 1000);
            BgVideoDimmer.Visibility = Visibility.Visible;
        }

        private void FadeOutBgVideo()
        {
            _currentBgVideoPath = null;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            anim.Completed += (s, e) =>
            {
                if (_currentBgVideoPath == null)
                {
                    try
                    {
                        BgVideoA.Stop(); BgVideoA.Source = null;
                        BgVideoB.Stop(); BgVideoB.Source = null;
                        BgVideoDimmer.Visibility = Visibility.Collapsed;
                    }
                    catch { }
                }
            };
            BgVideoContainer.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private static void AnimateOpacity(UIElement el, double to, int ms)
        {
            el.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)));
        }

        private void BtnSideBack_Click(object sender, RoutedEventArgs e)
        {
            if (_historyIndex > 0)
            {
                _suppressHistory = true;
                _historyIndex--;
                var key = _history[_historyIndex];
                Navigate(key);
                _suppressHistory = false;
                UpdateHistoryButtons();
            }
        }

        private void BtnSideForward_Click(object sender, RoutedEventArgs e)
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
            {
                _suppressHistory = true;
                _historyIndex++;
                var key = _history[_historyIndex];
                Navigate(key);
                _suppressHistory = false;
                UpdateHistoryButtons();
            }
        }

        private void UpdateHistoryButtons()
        {
            var backBtn = (System.Windows.Controls.Primitives.ToggleButton)FindName("BtnSideBack");
            var fwdBtn = (System.Windows.Controls.Primitives.ToggleButton)FindName("BtnSideForward");
            if (backBtn != null) backBtn.IsEnabled = _historyIndex > 0;
            if (fwdBtn != null) fwdBtn.IsEnabled = (_historyIndex >= 0 && _historyIndex < _history.Count - 1);
        }

        private void BtnSideHide_Click(object sender, RoutedEventArgs e)
        {
            ShowSidebar(false);
        }

        private void BtnRestoreSidebar_Click(object sender, RoutedEventArgs e)
        {
            ShowSidebar(true);
        }

        private bool _sidebarExpanded = false;
        private void BtnSideExpand_Click(object sender, RoutedEventArgs e)
        {
            _sidebarExpanded = !_sidebarExpanded;
            SidebarColumn.Width = new GridLength(_sidebarExpanded ? 260 : 180);
            var btn = (System.Windows.Controls.Primitives.ToggleButton)sender;
            var text = ((StackPanel)btn.Content).Children[1] as TextBlock;
            if (text != null) text.Text = _sidebarExpanded ? "Shrink Menu" : "Expand Menu";
        }

        private void LeftHoverZone_MouseEnter(object sender, MouseEventArgs e)
        {
            if (SidebarBorder.Visibility != Visibility.Visible)
            {
                var btn = (Button)FindName("SidebarRestoreBtn");
                if (btn != null) btn.Visibility = Visibility.Visible;
            }
        }

        private void LeftHoverZone_MouseLeave(object sender, MouseEventArgs e)
        {
            if (SidebarBorder.Visibility != Visibility.Visible)
            {
                var btn = (Button)FindName("SidebarRestoreBtn");
                if (btn != null) btn.Visibility = Visibility.Collapsed;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Disabled dragging as requested: "Make the entire app not movable"
            // if (e.ChangedButton == MouseButton.Left)
            //     DragMove();
        }

        public double GetSidebarWidth() 
        {
            var col = (FrameworkElement)this.FindName("SidebarCol");
            return col?.ActualWidth ?? 250;
        }

        public double GetHeaderHeight() 
        {
            var tb = (FrameworkElement)this.FindName("TitleBar");
            return tb?.ActualHeight ?? 32;
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) 
        {
            SoundService.PlayButton();
            WindowState = WindowState.Minimized;
        }

        private void BtnMax_Click(object sender, RoutedEventArgs e) 
        {
            SoundService.PlayButton();
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SoundService.PlayButton();
            Close();
        }

        private void BtnSideExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BtnSideSettings_Click(object sender, RoutedEventArgs e)
        {
            Navigate("settings");
        }

        private void BtnSideSettings_MouseEnter(object sender, MouseEventArgs e)
        {
            SoundService.PlayHover();
        }

        private void BtnSideExit_MouseEnter(object sender, MouseEventArgs e)
        {
            SoundService.PlayHover();
        }

        // Sidebar Home Banner events
        private void SidebarHomeBanner_Click(object sender, MouseButtonEventArgs e)
        {
            Navigate("home");
        }

        private void SidebarHomeBanner_Enter(object sender, MouseEventArgs e)
        {
            SoundService.PlayHover();
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.08, TimeSpan.FromMilliseconds(125))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            SidebarHomeBanner.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
            SidebarHomeBanner.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);
        }

        private void SidebarHomeBanner_Leave(object sender, MouseEventArgs e)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(125))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            SidebarHomeBanner.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim);
            SidebarHomeBanner.RenderTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim);
        }

        // --- Player Bar Logic ---
        private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = true;
        }

        private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPlaying && AudioPlayer.NaturalDuration.HasTimeSpan)
            {
                AudioPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
                CurrentTimeLabel.Text = AudioPlayer.Position.ToString(@"m\:ss");
            }
            _isDraggingSlider = false;
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
            {
                CurrentTimeLabel.Text = TimeSpan.FromSeconds(ProgressSlider.Value).ToString(@"m\:ss");
            }
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (OnPrevTrack != null) await OnPrevTrack();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (OnNextTrack != null) await OnNextTrack();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPlaying) return; // Ignore if completely stopped/empty

            // Basic toggle
            if (PlayPauseIcon.Source.ToString().Contains("pause"))
            {
                AudioPlayer.Pause();
                try { PlayPauseIcon.Source = IconHelper.Img("player/play", 18).Source; } catch { }
            }
            else
            {
                AudioPlayer.Play();
                try { PlayPauseIcon.Source = IconHelper.Img("player/pause", 18).Source; } catch { }
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AudioPlayer != null)
            {
                AudioPlayer.Volume = VolumeSlider.Value / 100.0;
                
                // Mute icon logic
                if (VolumeIcon != null)
                {
                    try
                    {
                        if (VolumeSlider.Value == 0)
                            VolumeIcon.Source = IconHelper.Img("player/mute", 15).Source;
                        else
                            VolumeIcon.Source = IconHelper.Img("player/volume", 15).Source;
                    }
                    catch { }
                }
            }
        }

        private UIElement CreatePlaceholder(string name)
        {
            return new Border
            {
                Child = new TextBlock
                {
                    Text = $"{name} page coming soon...",
                    Foreground = Brushes.Gray,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static readonly (string Icon, string Label, string Key, bool IsWeb)[] NavItems =
        {
            ("categories/movies",             "Movies",     "movies",   false),
            ("categories/games",              "Games",      "hydra",    false),
            ("categories/anime",              "Anime",      "nexus-anime", false), 
            ("categories/music",              "Music",      "music-hub",   false),
            ("categories/manga",              "Manga",      "manga",    false),
            ("categories/books",              "Books",      "books",    false),
            ("categories/comics",             "Comics",     "comics",   false),
            ("player/volume",                 "Music",      "harmony",  false),
            ("categories/podcasts",           "Podcasts",   "podcasts", false),
            ("categories/audiobooks",         "Audiobooks", "audiobooks", false),
            ("categories/programs",           "Online Courses", "courses", false),
            ("nav/radio",                     "Radio",      "radio",    false),
            ("categories/tv",                 "Live TV",    "tv",       false),
            ("nav/browser",                   "Browser",    "brave",    false),
            ("categories/apps",               "Google Play","gplay",    false),
            ("categories/programs",           "Integrations","integrations",false),
            ("nav/hacking",                   "Hacking",    "hacking",  false),
            ("nav/home",                      "Retro",      "retro",    false),
            ("nav/games",                     "Other Games","othergames",false),
            ("categories/sounds",             "Archive",    "archive",  false),
            ("categories/system",             "System Tools","systools", false),
            ("nav/debloat",                   "Debloat",    "debloat",  false),
            ("nav/install",                   "Installs",   "install",  false),
            // Settings moved to footer, but kept in key map for navigation logic
            ("nav/links",                     "Links",      "links",    false),
            ("categories/apps",               "Library",    "library",  false)
        };
        
        // Settings is still a NavItem in the sense that we can navigate to it
        private static readonly (string Icon, string Label, string Key, bool IsWeb) SettingsNavItem = ("nav/settings", "Settings", "settings", false);
    }
}
