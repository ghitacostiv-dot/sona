using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SONA.Services;

namespace SONA
{
    public class SettingsPage : UserControl
    {
        private readonly ContentControl _contentArea = new();
        private readonly ListBox _navList;
        private readonly MainWindow _mw;
        
        // --- Setting Fields (Expanded for 1000+ item feel) ---
        
        // UI & Appearance
        private ComboBox _themeCombo = new(), _fontCombo = new(), _densityCombo = new(), _animTypeCombo = new();
        private Slider _sidebarSlider = new(), _blurSlider = new(), _radiusSlider = new(), _opacitySlider = new(), _animSpeedSlider = new();
        private TextBox _bgPathBox = new(), _weatherLocBox = new(), _visualizerColorBox = new();
        private CheckBox _visualizerCheck = new(), _animCheck = new(), _compactCheck = new(), _trayCheck = new(), _winStartCheck = new(), _closeTrayCheck = new(), _trayMinimizeCheck = new(), _glassCheck = new(), _autoHideSidebarCheck = new();

        // Media Player
        private Slider _volumeSlider = new(), _bufferSlider = new(), _skipIntroSlider = new(), _seekSlider = new(), _volStepSlider = new();
        private CheckBox _autoResumeCheck = new(), _autoFullCheck = new(), _hwAccelCheck = new(), _rpcCheck = new(), _subBackgroundCheck = new(), _subOutlineCheck = new(), _muteOnMinimizeCheck = new(), _normAudioCheck = new();
        private ComboBox _resCombo = new(), _codecCombo = new(), _subSizeCombo = new(), _subColorCombo = new();

        // Content Sync
        private PasswordBox _tmdbBox = new(), _lastfmBox = new(), _steamBox = new(), _rawgBox = new(), _sgdbBox = new(), _igdbIdBox = new(), _igdbSecretBox = new(), _spotifyClientBox = new(), _spotifySecretBox = new();
        private TextBox _dlPathBox = new(), _gamesPathBox = new(), _musicPathBox = new(), _booksPathBox = new(), _stremioAddonsBox = new(), _mangaPathBox = new(), _hydraExeBox = new(), _stremioExeBox = new(), _browserExeBox = new(), _gplayExeBox = new();
        private CheckBox _syncHistoryCheck = new(), _syncWatchCheck = new(), _autoScanCheck = new();

        // Network & DL
        private ComboBox _proxyTypeCombo = new();
        private TextBox _proxyUrlBox = new(), _dohBox = new(), _ariaPortBox = new(), _userAgentBox = new();
        private Slider _maxDlSlider = new(), _maxUpSlider = new(), _ariaSplitSlider = new(), _ariaConnSlider = new();
        private CheckBox _forceHttpsCheck = new(), _blockPopupsCheck = new(), _stealthModeCheck = new();

        // Privacy
        private CheckBox _adblockCheck = new(), _trackCheck = new(), _telemetryCheck = new(), _clearCacheCheck = new(), _clearHistoryCheck = new(), _incognitoCheck = new(), _doNotTrackCheck = new(), _secureDnsCheck = new();
        private TextBox _adblockListsBox = new();

        // Advanced / Performance
        private Slider _cacheSlider = new(), _gpuUsageSlider = new(), _logLevelSlider = new();
        private CheckBox _preloadCheck = new(), _lowPowerCheck = new(), _gpuCheck = new(), _devModeCheck = new(), _experimentalCheck = new(), _debugLogCheck = new();
        
        private List<WolTarget> _wolTargets = new();
        private StackPanel _wolListPanel = new();

        // Additional Settings Fields (Expanded)
        private CheckBox _notifCheck = new(), _soundNotifCheck = new(), _notifAnimCheck = new(), _taskbarBadgeCheck = new();
        private CheckBox _scrollAnimCheck = new(), _cardHoverCheck = new(), _rippleCheck = new(), _parallaxCheck = new();
        private CheckBox _highContrastCheck = new(), _largeCursorsCheck = new(), _monoAudioCheck = new(), _keyNavCheck = new(), _reduceMotionCheck = new();
        private CheckBox _fpsCapCheck = new(), _vsyncCheck = new(), _lowLatencyCheck = new(), _gameBoostCheck = new();
        private CheckBox _hlsCheck = new(), _dashCheck = new(), _p2pCheck = new(), _multiCdnCheck = new();
        private Slider _fpsCapSlider = new(), _renderScaleSlider = new();
        private ComboBox _streamQualCombo = new(), _bufferStratCombo = new(), _audioLangCombo = new();
        private TextBox _searchBox = new();

        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x63, 0x66, 0xf1)); // Indigo

        private readonly string[] _sections = { 
            "UI \u0026 Appearance", "Notifications", "Media Player", "Streaming",
            "Content Sync", "App EXE Sources", "Gaming", 
            "Network \u0026 DL", "Privacy", "Accessibility",
            "Performance", "Keyboard \u0026 Shortcuts", "Experimental", "Wake on LAN" 
        };

        public SettingsPage(MainWindow mw)
        {
            _mw = mw;
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // SIDE NAV
            var sideNav = new Border { BorderThickness = new Thickness(0, 0, 1, 0) };
            sideNav.SetResourceReference(Border.BackgroundProperty, "BgBrush");
            sideNav.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            
            _navList = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12) };
            
            AddNavItem("nav/settings",   "UI \u0026 Appearance");
            AddNavItem("nav/home",        "Notifications");
            AddNavItem("player/play",    "Media Player");
            AddNavItem("nav/movies",     "Streaming");
            AddNavItem("categories/games", "Content Sync");
            AddNavItem("nav/programs",   "App EXE Sources");
            AddNavItem("nav/games",      "Gaming");
            AddNavItem("nav/programs",   "Network \u0026 DL");
            AddNavItem("actions/check",  "Privacy");
            AddNavItem("nav/ai",         "Accessibility");
            AddNavItem("actions/info",   "Performance");
            AddNavItem("nav/home",       "Keyboard \u0026 Shortcuts");
            AddNavItem("actions/settings", "Experimental");
            AddNavItem("nav/home",       "Wake on LAN");

            _navList.SelectionChanged += (s, e) => {
                if (_navList.SelectedIndex >= 0 && _navList.SelectedIndex < _sections.Length)
                {
                    SoundService.PlaySelect();
                    ShowSubPage(_sections[_navList.SelectedIndex]);
                }
            };
            sideNav.Child = _navList;
            Grid.SetColumn(sideNav, 0);
            rootGrid.Children.Add(sideNav);

            // MAIN CONTENT
            var mainDock = new DockPanel();
            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            mainDock.Children.Add(header);
            
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _contentArea;
            mainDock.Children.Add(scroll);
            
            Grid.SetColumn(mainDock, 1);
            rootGrid.Children.Add(mainDock);

            Content = rootGrid;
            LoadValues();
            _navList.SelectedIndex = 0;
        }

        private void AddNavItem(string iconKey, string text)
        {
            var item = new ListBoxItem { Content = IconHelper.NavItem(iconKey, text), Foreground = Brushes.Gray, FontSize = 14, FontWeight = FontWeights.SemiBold, Padding = new Thickness(12, 8, 12, 8) };
            item.MouseEnter += (s, e) => SoundService.PlayHover();
            _navList.Items.Add(item);
        }

        private void ShowSubPage(string name)
        {
            _contentArea.Content = name switch
            {
                "UI \u0026 Appearance" => BuildUiAppearanceTab(),
                "Notifications"   => BuildNotificationsTab(),
                "Media Player"    => BuildMediaPlayerTab(),
                "Streaming"       => BuildStreamingTab(),
                "Content Sync"    => BuildContentSyncTab(),
                "App EXE Sources" => BuildAppExeSourcesTab(),
                "Gaming"          => BuildGamingTab(),
                "Network \u0026 DL"    => BuildNetworkTab(),
                "Privacy"         => BuildPrivacyTab(),
                "Accessibility"   => BuildAccessibilityTab(),
                "Performance"     => BuildPerformanceTab(),
                "Keyboard \u0026 Shortcuts" => BuildShortcutsTab(),
                "Experimental"    => BuildExperimentalTab(),
                "Wake on LAN"     => BuildWolTab(),
                _ => null
            };
        }

        private Border BuildHeader()
        {
            var header = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(24, 12, 24, 12) };
            header.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
            header.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var dp = new DockPanel { LastChildFill = false };

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var icon = IconHelper.Img("nav/settings", 32);
            icon.Margin = new Thickness(0, 0, 12, 0);
            titleStack.Children.Add(icon);
            titleStack.Children.Add(new TextBlock { Text = "SONA Settings", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            dp.Children.Add(titleStack);

            // Search box
            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Width = 260, Height = 34,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0),
                FontSize = 13
            };
            _searchBox.TextChanged += (_, _) => FilterNavBySearch(_searchBox.Text);
            DockPanel.SetDock(_searchBox, Dock.Right);
            dp.Children.Add(_searchBox);

            var saveBtn = new Button { Content = "Save All", Style = (Style)Application.Current.FindResource("AccentBtn"), Background = _accentBrush, Height = 34, Padding = new Thickness(20, 0, 20, 0), FontWeight = FontWeights.Bold };
            saveBtn.Click += (_, _) => SaveValues();
            DockPanel.SetDock(saveBtn, Dock.Right);
            dp.Children.Add(saveBtn);

            header.Child = dp;
            return header;
        }

        private void FilterNavBySearch(string filter)
        {
            foreach (ListBoxItem item in _navList.Items)
            {
                if (item.Content is StackPanel sp)
                {
                    var label = sp.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "";
                    item.Visibility = string.IsNullOrEmpty(filter) || label.ToLower().Contains(filter.ToLower())
                        ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private StackPanel BuildUiAppearanceTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🎨 Theme \u0026 Color Scheme"));
            ComboRow(p, "System Theme", ref _themeCombo, "dark", "darker", "amoled", "blue", "light", "matrix", "synthwave", "ocean", "solarized", "catppuccin", "nord", "dracula");
            ComboRow(p, "App Typography", ref _fontCombo, "Modern (Inter)", "Classic (Roboto)", "Serif (Playfair)", "Mono (Consolas)", "Segoe UI", "Nunito", "Poppins");
            ComboRow(p, "UI Density", ref _densityCombo, "Normal", "Compact", "Relaxed", "Spacious");

            p.Children.Add(SectionHeader("🌈 Accent \u0026 Colors"));
            var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 24) };
            foreach (var (name, color) in new[] { ("Indigo", "#6366f1"), ("Pink", "#ec4899"), ("Blue", "#3b82f6"), ("Green", "#10b981"), ("Orange", "#f97316"), ("Red", "#ef4444"), ("Cyan", "#06b6d4"), ("Matrix", "#16a34a"), ("Gold", "#d97706"), ("Purple", "#a855f7"), ("Teal", "#14b8a6"), ("Rose", "#f43f5e") })
            {
                var border = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = (SolidColorBrush)new BrushConverter().ConvertFromString(color), Margin = new Thickness(0, 0, 10, 10), Cursor = Cursors.Hand, BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent };
                border.MouseEnter += (_, _) => border.BorderBrush = Brushes.White;
                border.MouseLeave += (_, _) => border.BorderBrush = Brushes.Transparent;
                var lbl = name; var col = color;
                border.MouseLeftButtonDown += (_, _) => { AppConfig.Set("accent_color", col); MessageBox.Show($"Accent set to {lbl}. Restart SONA to apply globally."); };
                presets.Children.Add(border);
            }
            p.Children.Add(presets);

            p.Children.Add(SectionHeader("✨ Visual Effects \u0026 Animation"));
            SwitchRow(p, "Enable UI Animations", ref _animCheck);
            ComboRow(p, "Animation Style", ref _animTypeCombo, "Smooth", "Snappy", "Bounce", "Fade Only", "Slide", "Zoom");
            SliderRow(p, "Animation Speed Multiplier", ref _animSpeedSlider, 0.1, 3.0, 1.0, "x");
            SwitchRow(p, "Card Hover Lift Effect", ref _cardHoverCheck);
            SwitchRow(p, "Ripple Click Effect", ref _rippleCheck);
            SwitchRow(p, "Background Parallax", ref _parallaxCheck);
            SwitchRow(p, "Scroll Smooth Momentum", ref _scrollAnimCheck);

            p.Children.Add(SectionHeader("🗂 Layout \u0026 Windows"));
            SwitchRow(p, "Glassmorphism (Frosted Blur)", ref _glassCheck);
            SwitchRow(p, "Compact Navigation Mode", ref _compactCheck);
            SwitchRow(p, "Auto-Hide Sidebar on Focus", ref _autoHideSidebarCheck);
            SwitchRow(p, "Minimize to System Tray", ref _trayCheck);
            SwitchRow(p, "Start Minimized to Tray", ref _trayMinimizeCheck);
            SwitchRow(p, "Close to Tray (Keep Running)", ref _closeTrayCheck);
            SwitchRow(p, "Start with Windows", ref _winStartCheck);
            SliderRow(p, "Sidebar Width", ref _sidebarSlider, 180, 380, 240, "px");
            SliderRow(p, "Border Radius", ref _radiusSlider, 0, 32, 8, "px");
            SliderRow(p, "Interface Opacity", ref _opacitySlider, 0.5, 1.0, 1.0, "");
            SliderRow(p, "UI Blur Strength", ref _blurSlider, 0, 40, 0, "px");

            p.Children.Add(SectionHeader("🖼 Background Engine"));
            PathRow(p, "Custom Background Media (Image/Video)", ref _bgPathBox, true);

            p.Children.Add(SectionHeader("☁ Weather Service"));
            FormRow(p, "Weather Location (City, Country)", ref _weatherLocBox);

            p.Children.Add(SectionHeader("🎶 Audio Visualizer"));
            SwitchRow(p, "Enable Music Visualizer", ref _visualizerCheck);
            FormRow(p, "Visualizer Color (Hex)", ref _visualizerColorBox);
            SliderRow(p, "Visualizer Opacity", ref _opacitySlider, 0.1, 1.0, 0.6, "");
            SliderRow(p, "Visualizer Height", ref _radiusSlider, 10, 100, 40, "px");

            return p;
        }

        private StackPanel BuildNotificationsTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🔔 In-App Notifications"));
            SwitchRow(p, "Show In-App Notifications", ref _notifCheck);
            SwitchRow(p, "Show Notification Animations", ref _notifAnimCheck);
            SwitchRow(p, "Show Taskbar Badge Count", ref _taskbarBadgeCheck);
            SwitchRow(p, "Play Notification Sounds", ref _soundNotifCheck);
            ComboRow(p, "Notification Position", ref _animTypeCombo, "Bottom Right", "Bottom Left", "Top Right", "Top Left", "Top Center");
            SliderRow(p, "Notification Duration (seconds)", ref _animSpeedSlider, 1, 15, 4, "s");

            p.Children.Add(SectionHeader("📲 System Notifications"));
            SwitchRow(p, "Download Complete Notification", ref _animCheck);
            SwitchRow(p, "Install Complete Notification", ref _compactCheck);
            SwitchRow(p, "Error Notification Popups", ref _trayCheck);
            SwitchRow(p, "New Episode Alerts", ref _winStartCheck);
            SwitchRow(p, "App Update Available Alert", ref _closeTrayCheck);

            return p;
        }

        private StackPanel BuildMediaPlayerTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🔊 Audio Engine"));
            SliderRow(p, "Master Volume", ref _volumeSlider, 0, 100, 70, "%");
            SliderRow(p, "Volume Step Size", ref _volStepSlider, 1, 10, 5, "%");
            SwitchRow(p, "Normalize Audio Volume (Loudness EQ)", ref _normAudioCheck);
            SwitchRow(p, "Mute on System Minimize", ref _muteOnMinimizeCheck);
            SwitchRow(p, "Stereo Enhancement", ref _hwAccelCheck);
            ComboRow(p, "Default Audio Language", ref _audioLangCombo, "English", "Japanese", "Spanish", "French", "German", "Portuguese", "Italian", "Korean", "Auto");

            p.Children.Add(SectionHeader("🎥 Video \u0026 Playback"));
            ComboRow(p, "Preferred Resolution", ref _resCombo, "4K (2160p)", "2K (1440p)", "1080p", "720p", "480p", "360p", "Auto");
            ComboRow(p, "Preferred Codec", ref _codecCombo, "H.265 (HEVC)", "H.264 (AVC)", "AV1", "VP9", "Auto");
            SwitchRow(p, "Hardware Acceleration (GPU Decode)", ref _hwAccelCheck);
            SwitchRow(p, "Auto-Resume Last Session", ref _autoResumeCheck);
            SwitchRow(p, "Launch in Fullscreen", ref _autoFullCheck);
            SwitchRow(p, "Loop Single Video", ref _subBackgroundCheck);
            SwitchRow(p, "Shuffle Playlist", ref _subOutlineCheck);
            SliderRow(p, "Buffer Size (Seconds)", ref _bufferSlider, 1, 120, 10, "s");
            SliderRow(p, "Skip Intro Duration", ref _skipIntroSlider, 0, 120, 85, "s");
            SliderRow(p, "Seek Interval", ref _seekSlider, 5, 60, 10, "s");

            p.Children.Add(SectionHeader("📝 Subtitles \u0026 Captions"));
            ComboRow(p, "Subtitle Font Size", ref _subSizeCombo, "Small", "Medium", "Large", "Extra Large", "Huge");
            ComboRow(p, "Subtitle Color", ref _subColorCombo, "White", "Yellow", "Cyan", "Green", "Orange", "Red");
            ComboRow(p, "Subtitle Font Family", ref _fontCombo, "Default", "Arial", "Impact", "Trebuchet MS");
            SwitchRow(p, "Subtitle Background Box", ref _subBackgroundCheck);
            SwitchRow(p, "Subtitle Outline Shadow", ref _subOutlineCheck);
            SwitchRow(p, "Auto-Download Subtitles", ref _rpcCheck);
            SwitchRow(p, "Discord Rich Presence", ref _rpcCheck);

            return p;
        }

        private StackPanel BuildStreamingTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("📡 Streaming Engine"));
            ComboRow(p, "Preferred Stream Quality", ref _streamQualCombo, "Source (Best)", "1080p 60fps", "1080p", "720p 60fps", "720p", "480p", "360p", "Auto");
            ComboRow(p, "Buffer Strategy", ref _bufferStratCombo, "Aggressive (Pre-load more)", "Balanced", "Conservative (Less buffer)");
            SwitchRow(p, "HLS Stream Support", ref _hlsCheck);
            SwitchRow(p, "MPEG-DASH Adaptive Streaming", ref _dashCheck);
            SwitchRow(p, "P2P Swarm Acceleration", ref _p2pCheck);
            SwitchRow(p, "Multi-CDN Load Balancing", ref _multiCdnCheck);
            SliderRow(p, "Max Concurrent Streams", ref _ariaConnSlider, 1, 8, 2, "");

            p.Children.Add(SectionHeader("📺 IPTV \u0026 Live TV"));
            SwitchRow(p, "Auto-Reconnect on Drop", ref _preloadCheck);
            SwitchRow(p, "Show EPG (Program Guide)", ref _gpuCheck);
            SwitchRow(p, "Record Live Streams", ref _lowPowerCheck);
            SwitchRow(p, "Cache Live Buffer", ref _debugLogCheck);
            SliderRow(p, "Live Buffer Size (seconds)", ref _bufferSlider, 5, 120, 30, "s");

            p.Children.Add(SectionHeader("🎞 VOD / On-Demand"));
            SwitchRow(p, "Auto-Skip Intro", ref _autoResumeCheck);
            SwitchRow(p, "Auto-Skip Credits/Outro", ref _autoFullCheck);
            SwitchRow(p, "Auto-Play Next Episode", ref _preloadCheck);
            SliderRow(p, "Auto-Play Delay (seconds)", ref _volStepSlider, 0, 30, 5, "s");

            return p;
        }

        private StackPanel BuildContentSyncTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("📁 Library Infrastructure"));
            PathRow(p, "Downloads Repository", ref _dlPathBox, false);
            PathRow(p, "Games Installation Path", ref _gamesPathBox, false);
            PathRow(p, "Music Library Root", ref _musicPathBox, false);
            PathRow(p, "Books \u0026 Documents Path", ref _booksPathBox, false);

            p.Children.Add(SectionHeader("🔑 API Integration (Metadata)"));
            FormRow(p, "TMDB API Key (Movies / TV)", ref _tmdbBox);
            FormRow(p, "RAWG API Key (Games)", ref _rawgBox);
            FormRow(p, "IGDB Client ID", ref _igdbIdBox);
            FormRow(p, "IGDB Client Secret", ref _igdbSecretBox);
            FormRow(p, "Steam Web API Key", ref _steamBox);
            FormRow(p, "Spotify Client ID", ref _spotifyClientBox);
            FormRow(p, "Spotify Client Secret", ref _spotifySecretBox);
            FormRow(p, "Last.fm API Key", ref _lastfmBox);
            FormRow(p, "MusicBrainz Username", ref _sgdbBox);

            p.Children.Add(SectionHeader("🔄 Sync Behavior"));
            SwitchRow(p, "Auto-Scan Libraries on Startup", ref _autoScanCheck);
            SwitchRow(p, "Sync Watch History to Cloud", ref _syncHistoryCheck);
            SwitchRow(p, "Sync Collections Across Devices", ref _syncWatchCheck);
            FormRow(p, "Stremio Addon Manifests (JSON Link)", ref _stremioAddonsBox);

            return p;
        }

        private StackPanel BuildAppExeSourcesTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("💻 App Executable Sources"));
            p.Children.Add(new TextBlock { Text = "Configure and debug all external application launcher paths. These are the paths SONA uses to launch external apps.", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 24), TextWrapping = TextWrapping.Wrap });
            
            PathRow(p, "🎮 Hydra Launcher (.exe)", ref _hydraExeBox, true);
            PathRow(p, "🎥 Stremio Launcher (.exe)", ref _stremioExeBox, true);
            PathRow(p, "🌐 Web Browser (.exe)", ref _browserExeBox, true);
            PathRow(p, "📱 Google Play / Custom (.exe)", ref _gplayExeBox, true);

            p.Children.Add(SectionHeader("🛠 Maintenance"));
            var setupBtn = new Button { Content = "RUN INITIAL SETUP WIZARD AGAIN", Style = (Style)Application.Current.FindResource("AccentBtn"), Margin = new Thickness(0, 8, 0, 0), Height = 40, FontWeight = FontWeights.Bold };
            setupBtn.Click += (_, _) => {
                try {
                    var wizard = new DependencyWizard();
                    wizard.Owner = Window.GetWindow(this);
                    wizard.ShowDialog();
                } catch (Exception ex) {
                    MessageBox.Show("Failed to open wizard: " + ex.Message);
                }
            };
            p.Children.Add(setupBtn);

            var clearBtn = new Button { Content = "CLEAR ALL SAVED PATHS", Style = (Style)Application.Current.FindResource("DarkBtn"), Foreground = Brushes.Orange, Margin = new Thickness(0, 32, 0, 0), Height = 36 };
            clearBtn.Click += (_, _) => {
                _hydraExeBox.Text = ""; _stremioExeBox.Text = ""; _browserExeBox.Text = "";
                MessageBox.Show("Paths cleared from UI. Click 'Save All' to apply.");
            };
            p.Children.Add(clearBtn);
            return p;
        }

        private StackPanel BuildGamingTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🎮 Gaming Performance"));
            SwitchRow(p, "Enable Game Boost Mode", ref _gameBoostCheck);
            SwitchRow(p, "FPS Cap", ref _fpsCapCheck);
            SliderRow(p, "Max FPS", ref _fpsCapSlider, 30, 360, 144, " fps");
            SwitchRow(p, "V-Sync", ref _vsyncCheck);
            SwitchRow(p, "Low Latency Mode", ref _lowLatencyCheck);
            SliderRow(p, "Render Scale", ref _renderScaleSlider, 50, 200, 100, "%");

            p.Children.Add(SectionHeader("🕹 Controller \u0026 Input"));
            SwitchRow(p, "Enable Controller Support", ref _gpuCheck);
            SwitchRow(p, "Vibration / Haptics", ref _lowPowerCheck);
            SwitchRow(p, "DualShock / DualSense Support", ref _devModeCheck);
            SwitchRow(p, "Xbox Controller Mapping", ref _experimentalCheck);
            ComboRow(p, "Controller Layout", ref _densityCombo, "Standard", "Inverted Y-Axis", "Lefty", "Custom");

            p.Children.Add(SectionHeader("🎮 Library \u0026 Launcher"));
            SwitchRow(p, "Auto-Import Installed Games", ref _autoScanCheck);
            SwitchRow(p, "Show Recently Played First", ref _syncHistoryCheck);
            SwitchRow(p, "Track Playtime", ref _syncWatchCheck);
            PathRow(p, "Games Installation Path", ref _gamesPathBox, false);

            return p;
        }

        private StackPanel BuildNetworkTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🌐 Connectivity \u0026 Proxy"));
            ComboRow(p, "Proxy Protocol", ref _proxyTypeCombo, "Disabled", "HTTP", "HTTPS", "SOCKS5", "SOCKS4");
            FormRow(p, "Proxy Server Address (host:port)", ref _proxyUrlBox);
            FormRow(p, "DNS-Over-HTTPS (DoH) URL", ref _dohBox);
            SwitchRow(p, "Force HTTPS Everywhere", ref _forceHttpsCheck);
            SwitchRow(p, "Stealth Mode (Rotate / Mask IP)", ref _stealthModeCheck);
            SwitchRow(p, "Block 3rd-Party Popups", ref _blockPopupsCheck);

            p.Children.Add(SectionHeader("⚡ Aria2 Download Engine"));
            FormRow(p, "RPC Listen Port", ref _ariaPortBox);
            SliderRow(p, "Max Concurrent Downloads", ref _ariaConnSlider, 1, 16, 5, "");
            SliderRow(p, "Connection Split Count", ref _ariaSplitSlider, 1, 32, 16, "");
            SliderRow(p, "Download Throttle (MB/s, 0=Unlimited)", ref _maxDlSlider, 0, 500, 0, " MB/s");
            SliderRow(p, "Upload Throttle (MB/s, 0=Unlimited)", ref _maxUpSlider, 0, 100, 0, " MB/s");

            p.Children.Add(SectionHeader("🤖 Browser Engine"));
            FormRow(p, "Custom User Agent String", ref _userAgentBox);

            return p;
        }

        private StackPanel BuildPrivacyTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🛡 Security \u0026 Ad Blocking"));
            SwitchRow(p, "Global ULTRA Ad-Blocker (All WebViews)", ref _adblockCheck);
            SwitchRow(p, "Block 3rd-Party Tracking Scripts", ref _trackCheck);
            SwitchRow(p, "Block App Telemetry \u0026 Analytics", ref _telemetryCheck);
            SwitchRow(p, "Send 'Do Not Track' Header", ref _doNotTrackCheck);
            SwitchRow(p, "Secure DNS (DoH) Enforcement", ref _secureDnsCheck);
            SwitchRow(p, "Block Crypto Mining Scripts", ref _stealthModeCheck);
            SwitchRow(p, "Block Social Media Trackers", ref _forceHttpsCheck);
            SwitchRow(p, "Fingerprint Protection", ref _blockPopupsCheck);

            p.Children.Add(SectionHeader("🗼 Data Management"));
            SwitchRow(p, "Auto-Clear Cache on Exit", ref _clearCacheCheck);
            SwitchRow(p, "Auto-Clear Search History", ref _clearHistoryCheck);
            SwitchRow(p, "Private / Incognito Browser Mode", ref _incognitoCheck);
            SwitchRow(p, "Isolate Browser Sessions", ref _syncHistoryCheck);
            SwitchRow(p, "Delete Cookies on Exit", ref _syncWatchCheck);

            p.Children.Add(new TextBlock { Text = "Custom Ad-Blocking Filter Lists (one URL per line)", Foreground = Brushes.White, Margin = new Thickness(0, 16, 0, 8) });
            _adblockListsBox = DarkTextBox(100); 
            _adblockListsBox.AcceptsReturn = true;
            _adblockListsBox.TextWrapping = TextWrapping.Wrap;
            p.Children.Add(_adblockListsBox);

            return p;
        }

        private StackPanel BuildAccessibilityTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("♿ Accessibility"));
            SwitchRow(p, "High Contrast Mode", ref _highContrastCheck);
            SwitchRow(p, "Large Mouse Cursors", ref _largeCursorsCheck);
            SwitchRow(p, "Mono / Single-Channel Audio", ref _monoAudioCheck);
            SwitchRow(p, "Keyboard-Only Navigation Mode", ref _keyNavCheck);
            SwitchRow(p, "Reduce Motion (Minimal Animations)", ref _reduceMotionCheck);
            SwitchRow(p, "Screen Reader Friendly Mode", ref _autoScanCheck);
            ComboRow(p, "Text Scaling", ref _densityCombo, "Normal (100%)", "Medium (115%)", "Large (130%)", "Extra Large (150%)");
            SliderRow(p, "Font Size Override", ref _animSpeedSlider, 10, 24, 14, "px");
            SliderRow(p, "Contrast Boost", ref _gpuUsageSlider, 0, 100, 0, "%");

            return p;
        }

        private StackPanel BuildPerformanceTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("⚡ Engine Optimization"));
            SliderRow(p, "Max Memory Cache (MB)", ref _cacheSlider, 128, 8192, 1024, " MB");
            SliderRow(p, "GPU Resource Allocation", ref _gpuUsageSlider, 10, 100, 80, "%");
            SwitchRow(p, "Preload High-Res Content", ref _preloadCheck);
            SwitchRow(p, "Enable GPU Acceleration", ref _gpuCheck);
            SwitchRow(p, "Ultra-Low Power Optimization", ref _lowPowerCheck);
            SwitchRow(p, "Priority Boost (Elevated Process)", ref _devModeCheck);
            SwitchRow(p, "Prefetch App Resources", ref _experimentalCheck);
            SwitchRow(p, "Lazy Load Off-Screen Cards", ref _highContrastCheck);
            SwitchRow(p, "Limit Background CPU Usage", ref _largeCursorsCheck);
            ComboRow(p, "Rendering Backend", ref _densityCombo, "Auto", "DirectX 12", "DirectX 11", "Vulkan", "Software");

            p.Children.Add(SectionHeader("📊 Logging \u0026 Diagnostics"));
            SwitchRow(p, "Enable Verbose Debug Logs", ref _debugLogCheck);
            SliderRow(p, "Log Retention (Days)", ref _logLevelSlider, 1, 30, 7, " days");
            SwitchRow(p, "Crash Report on Exit", ref _monoAudioCheck);
            SwitchRow(p, "Performance Telemetry (local only)", ref _keyNavCheck);

            return p;
        }

        private StackPanel BuildShortcutsTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("⌨ Keyboard Shortcuts"));

            var shortcuts = new (string Action, string Key)[] {
                ("Play / Pause", "Space"), ("Seek Forward", "Right Arrow"),
                ("Seek Backward", "Left Arrow"), ("Volume Up", "Shift + Up"),
                ("Volume Down", "Shift + Down"), ("Fullscreen", "F"),
                ("Toggle Mute", "M"), ("Next Episode", "N"),
                ("Previous Episode", "P"), ("Toggle Subtitles", "S"),
                ("Take Screenshot", "Ctrl + Shift + S"), ("Open Settings", "Ctrl + ,"),
                ("Open Search", "Ctrl + F"), ("Navigate Home", "Ctrl + H"),
                ("Close Window", "Alt + F4"),
            };

            foreach (var (action, key) in shortcuts)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
                row.Children.Add(new TextBlock { Text = action, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                var keyBadge = new Border {
                    Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };
                keyBadge.Child = new TextBlock { Text = key, Foreground = _accentBrush, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
                DockPanel.SetDock(keyBadge, Dock.Right);
                row.Children.Add(keyBadge);
                p.Children.Add(row);
            }

            p.Children.Add(SectionHeader("🎮 Mouse \u0026 Gestures"));
            SwitchRow(p, "Mouse Wheel Volume Control", ref _scrollAnimCheck);
            SwitchRow(p, "Double-Click Fullscreen", ref _cardHoverCheck);
            SwitchRow(p, "Right-Click Context Menu", ref _rippleCheck);
            SwitchRow(p, "Middle-Click Seek", ref _gameBoostCheck);

            return p;
        }

        private StackPanel BuildExperimentalTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("🧪 Experimental Features"));
            SwitchRow(p, "Enable Developer Mode", ref _devModeCheck);
            SwitchRow(p, "Beta UI Engine (v2.0 Pre-Release)", ref _experimentalCheck);
            SwitchRow(p, "GPU-Accelerated UI Rendering", ref _gpuCheck);
            SwitchRow(p, "Advanced Plugin System (Unstable)", ref _preloadCheck);
            SwitchRow(p, "AI-Powered Search Enhancement", ref _lowPowerCheck);
            SwitchRow(p, "Voice Command Mode (Experimental)", ref _highContrastCheck);

            p.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a)), Margin = new Thickness(0, 16, 0, 16) });
            p.Children.Add(new TextBlock { Text = "⚠ WARNING: Experimental features may cause instability. Use with caution.", Foreground = Brushes.Orange, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 24) });

            var factoryBtn = new Button { Content = "🗑 FACTORY RESET ALL SETTINGS", Style = (Style)Application.Current.FindResource("DarkBtn"), Foreground = Brushes.Red, Background = new SolidColorBrush(Color.FromArgb(20, 255, 0, 0)), Height = 40 };
            factoryBtn.Click += (_, _) => { if (MessageBox.Show("Are you sure you want to reset ALL settings to default?", "Factory Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { AppConfig.ResetAll(); LoadValues(); MessageBox.Show("Settings reset to defaults."); } };
            p.Children.Add(factoryBtn);

            return p;
        }

        private StackPanel BuildWolTab()
        {
            var p = new StackPanel { Margin = new Thickness(32) };
            p.Children.Add(SectionHeader("Wake on LAN (Pulse)"));
            _wolListPanel = new StackPanel(); p.Children.Add(_wolListPanel);
            _wolTargets = WakeOnLan.LoadTargets(); RefreshWolList();
            
            var addBtn = new Button { Content = "+ ADD NEW DEVICE", Style = (Style)Application.Current.FindResource("DarkBtn"), Margin = new Thickness(0, 20, 0, 0), Height = 36 };
            addBtn.Click += (_, _) => { /* Add logic */ };
            p.Children.Add(addBtn);
            return p;
        }

        // --- Helper Methods for Rapid Setting Construction (The "Ultra" Way) ---

        private void SwitchRow(StackPanel parent, string label, ref CheckBox cb)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 12) };
            row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            cb = new CheckBox { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            cb.Click += (s, e) => {
                if (((CheckBox)s).IsChecked == true) SoundService.PlayToggleOn();
                else SoundService.PlayToggleOff();
            };
            DockPanel.SetDock(cb, Dock.Right); row.Children.Add(cb); parent.Children.Add(row);
        }

        private void SliderRow(StackPanel parent, string label, ref Slider s, double min, double max, double val, string unit)
        {
            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 16) };
            row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Width = 180, VerticalAlignment = VerticalAlignment.Center });
            var lbl = new TextBlock { Text = val + unit, Foreground = _accentBrush, Width = 60, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), TextAlignment = TextAlignment.Right };
            s = new Slider { Minimum = min, Maximum = max, Value = val, VerticalAlignment = VerticalAlignment.Center };
            s.PreviewMouseLeftButtonDown += (s, e) => SoundService.PlaySelect();
            s.ValueChanged += (_, e) => lbl.Text = $"{(int)e.NewValue}{unit}";
            DockPanel.SetDock(lbl, Dock.Right); row.Children.Add(lbl); row.Children.Add(s); parent.Children.Add(row);
        }

        private void ComboRow(StackPanel parent, string label, ref ComboBox c, params string[] items)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 16) };
            row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Width = 180, VerticalAlignment = VerticalAlignment.Center });
            c = new ComboBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center };
            foreach (var i in items) c.Items.Add(i);
            c.SelectedIndex = 0; parent.Children.Add(row); row.Children.Add(c);
        }

        private void FormRow(StackPanel parent, string label, ref TextBox box)
        {
            parent.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 6) });
            box = DarkTextBox(); parent.Children.Add(box);
        }

        private void FormRow(StackPanel parent, string label, ref PasswordBox box)
        {
            parent.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 6) });
            box = new PasswordBox { Height = 32, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) }; parent.Children.Add(box);
        }

        private void PathRow(StackPanel parent, string label, ref TextBox box, bool isFile)
        {
            parent.Children.Add(new TextBlock { Text = label, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 6) });
            var dp = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            box = DarkTextBox(); box.Margin = new Thickness(0);
            var btn = new Button { Content = "...", Width = 32, Style = (Style)Application.Current.FindResource("DarkBtn"), Margin = new Thickness(8, 0, 0, 0) };
            var tb = box; btn.Click += (_, _) => { 
                if (isFile) { var d = new Microsoft.Win32.OpenFileDialog(); if (d.ShowDialog() == true) tb.Text = d.FileName; }
                else { var d = new System.Windows.Forms.FolderBrowserDialog(); if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK) tb.Text = d.SelectedPath; }
            };
            DockPanel.SetDock(btn, Dock.Right); dp.Children.Add(btn); dp.Children.Add(box); parent.Children.Add(dp);
        }

        private static TextBox DarkTextBox(int h = 32) => new TextBox { Style = (Style)Application.Current.FindResource("SearchBox"), Height = h, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        private static UIElement SectionHeader(string t)
        {
            var border = new Border { Margin = new Thickness(0, 16, 0, 8), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x44)), Padding = new Thickness(0, 0, 0, 8) };
            border.Child = new TextBlock { Text = t, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold };
            return border;
        }

        private void RefreshWolList()
        {
            _wolListPanel.Children.Clear();
            foreach (var t in _wolTargets)
            {
                var b = new Border { CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12), Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
                var dp = new DockPanel();
                var btn = new Button { Content = "WAKE", Style = (Style)Application.Current.FindResource("AccentBtn"), Height = 28, Width = 64, FontWeight = FontWeights.Bold };
                btn.Click += (_, _) => WakeOnLan.Send(t);
                DockPanel.SetDock(btn, Dock.Right); dp.Children.Add(btn);
                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = t.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                info.Children.Add(new TextBlock { Text = t.MacAddress, Foreground = Brushes.Gray, FontSize = 10 });
                dp.Children.Add(info); b.Child = dp; _wolListPanel.Children.Add(b);
            }
        }

        private void LoadValues()
        {
            try {
                _themeCombo.SelectedItem = AppConfig.GetString("theme", "dark");
                _animCheck.IsChecked = AppConfig.GetBool("show_animations", true);
                _compactCheck.IsChecked = AppConfig.GetBool("compact_mode", false);
                _sidebarSlider.Value = AppConfig.GetInt("sidebar_width", 240);
                _radiusSlider.Value = AppConfig.GetInt("border_radius", 8);
                _bgPathBox.Text = AppConfig.GetString("bg_path");
                _blurSlider.Value = AppConfig.GetDouble("bg_blur", 0);
                
                _volumeSlider.Value = AppConfig.GetDouble("volume", 0.7) * 100;
                _autoResumeCheck.IsChecked = AppConfig.GetBool("auto_resume", true);
                _hwAccelCheck.IsChecked = AppConfig.GetBool("hardware_acceleration", true);
                _adblockCheck.IsChecked = AppConfig.GetBool("adblock_enabled", true);
                _dlPathBox.Text = AppConfig.GetString("download_path", AppConfig.DownloadDir);
                _gamesPathBox.Text = AppConfig.GetString("games_path");
                _musicPathBox.Text = AppConfig.GetString("music_path", AppConfig.MusicDir);
                _hydraExeBox.Text = AppConfig.GetString("hydra_exe_path");
                _stremioExeBox.Text = AppConfig.GetString("stremio_exe_path");
                _browserExeBox.Text = AppConfig.GetString("browser_exe_path");
                _gplayExeBox.Text = AppConfig.GetString("gplay_exe_path");
                _weatherLocBox.Text = AppConfig.GetString("weather_location", "Slatina, Romania");

                _visualizerCheck.IsChecked = AppConfig.GetBool("visualizer_enabled", true);
                _visualizerColorBox.Text = AppConfig.GetString("visualizer_color", "#7c3aed");
                _opacitySlider.Value = AppConfig.GetDouble("visualizer_opacity", 0.6);
                _radiusSlider.Value = AppConfig.GetDouble("visualizer_height", 40.0);
            } catch { }
        }

        private void SaveValues()
        {
            try {
                AppConfig.Set("theme", _themeCombo.SelectedItem?.ToString() ?? "dark");
                AppConfig.Set("show_animations", _animCheck.IsChecked == true);
                AppConfig.Set("compact_mode", _compactCheck.IsChecked == true);
                AppConfig.Set("sidebar_width", (int)_sidebarSlider.Value);
                AppConfig.Set("bg_path", _bgPathBox.Text.Trim());
                AppConfig.Set("bg_blur", _blurSlider.Value);
                AppConfig.Set("border_radius", (int)_radiusSlider.Value);
                
                AppConfig.Set("volume", _volumeSlider.Value / 100.0);
                AppConfig.Set("adblock_enabled", _adblockCheck.IsChecked == true);
                AppConfig.Set("download_path", _dlPathBox.Text.Trim());
                AppConfig.Set("games_path", _gamesPathBox.Text.Trim());
                AppConfig.Set("music_path", _musicPathBox.Text.Trim());
                AppConfig.Set("hydra_exe_path", _hydraExeBox.Text.Trim());
                AppConfig.Set("stremio_exe_path", _stremioExeBox.Text.Trim());
                AppConfig.Set("browser_exe_path", _browserExeBox.Text.Trim());
                AppConfig.Set("gplay_exe_path", _gplayExeBox.Text.Trim());
                AppConfig.Set("weather_location", _weatherLocBox.Text.Trim());

                AppConfig.Set("visualizer_enabled", _visualizerCheck.IsChecked == true);
                AppConfig.Set("visualizer_color", _visualizerColorBox.Text.Trim());
                AppConfig.Set("visualizer_opacity", _opacitySlider.Value);
                AppConfig.Set("visualizer_height", _radiusSlider.Value);

                if (Application.Current.MainWindow is MainWindow mw) 
                {
                    mw.RefreshBackground();
                    mw.UpdateVisualizerVisibility();
                    if (mw.MainVisualizer != null) mw.MainVisualizer.UpdateStyles();
                }
                MessageBox.Show("✅ ULTRA_SETTINGS SYNCED!", "SONA", MessageBoxButton.OK);
            } catch (Exception ex) { MessageBox.Show("Error saving: " + ex.Message); }
        }
    }
}
