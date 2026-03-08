using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using SONA.Services;

namespace SONA
{
    public class TvPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private VideoView _videoView;
        private LibVLC _libVlc;
        private LibVLCSharp.Shared.MediaPlayer _player;

        // UI refs
        private Grid _playerOverlay = new();
        private StackPanel _categoryStack = new();
        private ScrollViewer _mainScroll = new();
        private TextBox _searchBox = new();
        private TextBlock _errorText = new();
        private TextBlock _nowPlayingLabel = new();
        private Slider _volSlider = new();
        private Border _controlsBar = new();
        private ToggleButton _muteBtn = new();
        private TextBlock _loadingLabel = new();

        private string _searchQuery = "";
        private List<TvChannelInfo> _allChannels = new();

        private bool _isFullscreen = false;
        private bool _isPip = false;
        private Window? _pipHostWindow;

        private static readonly SolidColorBrush LiveRed    = new(Color.FromRgb(0xef, 0x44, 0x44));
        private static readonly SolidColorBrush AccentBlue = new(Color.FromRgb(0x3b, 0x82, 0xf6));
        private static readonly SolidColorBrush DarkCard   = new(Color.FromArgb(0xCC, 0x0d, 0x0d, 0x14));

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int cx, int cy, uint f);
        private static readonly IntPtr HWND_TOPMOST = new(-1);

        public TvPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            bool vlcOk = TryInitVlc();
            if (!vlcOk)
            {
                var errStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(32) };
                errStack.Children.Add(new TextBlock { Text = "📺  VLC Media Player Required", Foreground = Brushes.White, FontSize = 26, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) });
                errStack.Children.Add(new TextBlock { Text = "The Live TV player requires VLC to be installed (64-bit recommended).", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 24) });
                var dlBtn = new Button { Content = "🌐  Download VLC", Padding = new Thickness(20, 10, 20, 10), Style = (Style)Application.Current.FindResource("AccentBtn"), HorizontalAlignment = HorizontalAlignment.Center };
                dlBtn.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.videolan.org/vlc/") { UseShellExecute = true });
                errStack.Children.Add(dlBtn);
                Content = errStack;
                return;
            }

            var root = new Grid();

            var dockPanel = new DockPanel();

            // ── Hero Header ──────────────────────────────────────────────────
            var hero = BuildHero();
            DockPanel.SetDock(hero, Dock.Top);
            dockPanel.Children.Add(hero);

            // ── Search Bar ──────────────────────────────────────────────────
            var searchBar = BuildSearchBar();
            DockPanel.SetDock(searchBar, Dock.Top);
            dockPanel.Children.Add(searchBar);

            // ── Main Scroll with Categories ─────────────────────────────────
            _mainScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _categoryStack = new StackPanel { Margin = new Thickness(24, 0, 24, 32) };

            _loadingLabel = new TextBlock
            {
                Text = "📡  Loading channels from iptv-org...",
                Foreground = Brushes.Gray,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 80, 0, 0)
            };
            _categoryStack.Children.Add(_loadingLabel);

            _mainScroll.Content = _categoryStack;
            dockPanel.Children.Add(_mainScroll);

            root.Children.Add(dockPanel);

            // ── VLC Player Overlay ───────────────────────────────────────────
            _playerOverlay = BuildPlayerOverlay();
            _playerOverlay.Visibility = Visibility.Collapsed;
            root.Children.Add(_playerOverlay);

            Content = root;
            Loaded += OnLoaded;
        }

        private Border BuildHero()
        {
            var hero = new Border { Height = 130, Padding = new Thickness(32, 0, 32, 0) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x1e, 0x3a, 0x8a), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x3b, 0x82, 0xf6), 0.4));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x06, 0x0b, 0x27), 1.0));
            hero.Background = grad;

            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var icon = IconHelper.Img("categories/tv", 36);
            icon.Margin = new Thickness(0, 0, 14, 0);
            titleRow.Children.Add(icon);
            titleRow.Children.Add(new TextBlock { Text = "LIVE TELEVISION", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(titleRow);
            content.Children.Add(new TextBlock { Text = "Global IPTV streams  ·  10,000+ channels  ·  Categorized by genre", Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)), FontSize = 13 });
            hero.Child = content;
            return hero;
        }

        private Border BuildSearchBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x1a)),
                Padding = new Thickness(24, 14, 24, 14),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x35))
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 40, Width = 380, FontSize = 14,
                Padding = new Thickness(14, 0, 14, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x2e))
            };
            _searchBox.TextChanged += async (_, _) =>
            {
                string cur = _searchBox.Text;
                await Task.Delay(300);
                if (cur == _searchBox.Text)
                {
                    _searchQuery = cur.ToLower();
                    RenderAllCategories();
                }
            };

            var searchIcon = new Border { Width = 40, Height = 40, Background = AccentBlue, CornerRadius = new CornerRadius(0, 8, 8, 0) };
            searchIcon.Child = new TextBlock { Text = "🔍", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            row.Children.Add(_searchBox);
            row.Children.Add(searchIcon);
            bar.Child = row;
            return bar;
        }

        private bool TryInitVlc()
        {
            try
            {
                Core.Initialize();
                _libVlc = new LibVLC("--network-caching=3000", "--live-caching=3000", "--avcodec-hw=any");
                _player = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
                _player.EncounteredError += (_, _) => Dispatcher.Invoke(() => _errorText.Visibility = Visibility.Visible);
                _videoView = new VideoView { MediaPlayer = _player };
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TvPage] VLC init failed: {ex.Message}");
                return false;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_allChannels.Count > 0) return;
            try
            {
                _allChannels = await IptvManager.GetChannelsAsync();
                _loadingLabel.Visibility = Visibility.Collapsed;

                if (_allChannels.Count == 0)
                {
                    _loadingLabel.Text = "⚠️  No channels found. Check your connection.";
                    _loadingLabel.Visibility = Visibility.Visible;
                    return;
                }

                RenderAllCategories();
            }
            catch (Exception ex)
            {
                _loadingLabel.Text = $"⚠️  Failed to load channels: {ex.Message}";
                _loadingLabel.Visibility = Visibility.Visible;
            }
        }

        // ── Render all categories as horizontal carousels ────────────────────
        private void RenderAllCategories()
        {
            _categoryStack.Children.Clear();

            var grouped = _allChannels
                .Where(c => string.IsNullOrEmpty(_searchQuery) || c.Name.ToLower().Contains(_searchQuery))
                .GroupBy(c => c.Category)
                .OrderBy(g => GetCategoryOrder(g.Key))
                .ToList();

            if (!grouped.Any())
            {
                _categoryStack.Children.Add(new TextBlock
                {
                    Text = "🔍  No channels matched your search.",
                    Foreground = Brushes.Gray, FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 80, 0, 0)
                });
                return;
            }

            foreach (var group in grouped)
            {
                _categoryStack.Children.Add(BuildCategoryCarousel(group.Key, group.ToList()));
            }
        }

        private int GetCategoryOrder(string cat)
        {
            var order = IptvManager.CategoryOrder;
            var idx = Array.IndexOf(order, cat);
            return idx >= 0 ? idx : order.Length;
        }

        private StackPanel BuildCategoryCarousel(string categoryName, List<TvChannelInfo> channels)
        {
            var section = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };

            // Category header
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var title = new TextBlock
            {
                Text = categoryName,
                Foreground = Brushes.White,
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var count = new TextBlock
            {
                Text = $"{channels.Count} channels",
                Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);
            header.Children.Add(count);
            section.Children.Add(header);

            // Horizontal carousel
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            // Show up to 60 channels per carousel row (smaller cards)
            foreach (var ch in channels.Take(60))
                row.Children.Add(MakeChannelCard(ch));

            scrollViewer.Content = row;
            section.Children.Add(scrollViewer);

            // Separator
            section.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Margin = new Thickness(0, 20, 0, 0)
            });

            return section;
        }

        private Border MakeChannelCard(TvChannelInfo ch)
        {
            var card = new Border
            {
                Width = 140,
                Height = 90,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 4),
                Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var grid = new Grid();

            // Faded background logo
            if (!string.IsNullOrEmpty(ch.LogoUrl))
            {
                try
                {
                    var bgImg = new Image { Stretch = Stretch.UniformToFill, Opacity = 0.08 };
                    bgImg.Source = IconHelper.CreateSafe(new Uri(ch.LogoUrl));
                    grid.Children.Add(bgImg);
                }
                catch { }
            }

            var content = new DockPanel { Margin = new Thickness(8) };

            // Logo (centered)
            if (!string.IsNullOrEmpty(ch.LogoUrl))
            {
                try
                {
                    var logo = new Image { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    logo.Source = IconHelper.CreateSafe(new Uri(ch.LogoUrl));
                    logo.Margin = new Thickness(0, 0, 0, 4);
                    DockPanel.SetDock(logo, Dock.Top);
                    content.Children.Add(logo);
                }
                catch { }
            }

            // Channel name
            var nameText = new TextBlock
            {
                Text = ch.Name,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 120
            };
            DockPanel.SetDock(nameText, Dock.Top);
            content.Children.Add(nameText);

            // Bottom row: country + LIVE badge
            var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            if (!string.IsNullOrEmpty(ch.Country))
            {
                bottomRow.Children.Add(new TextBlock
                {
                    Text = CountryFlag(ch.Country),
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                });
            }
            var liveBadge = new Border { Background = LiveRed, CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 1, 4, 1) };
            liveBadge.Child = new TextBlock { Text = "LIVE", Foreground = Brushes.White, FontSize = 8, FontWeight = FontWeights.Bold };
            bottomRow.Children.Add(liveBadge);
            DockPanel.SetDock(bottomRow, Dock.Bottom);
            content.Children.Add(bottomRow);

            grid.Children.Add(content);
            card.Child = grid;

            card.MouseEnter += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x1a, 0x1a, 0x2e));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 59, 130, 246));
                SoundService.PlayHover();
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0d, 0x0d, 0x14));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255));
            };
            card.MouseLeftButtonDown += (_, _) => PlayChannel(ch);

            return card;
        }

        private static string CountryFlag(string country)
        {
            // Return flag emoji for common countries
            return country.ToUpper() switch
            {
                "US" or "USA" or "UNITED STATES" => "🇺🇸",
                "GB" or "UK" or "UNITED KINGDOM"  => "🇬🇧",
                "DE" or "GERMANY"                  => "🇩🇪",
                "FR" or "FRANCE"                   => "🇫🇷",
                "IT" or "ITALY"                    => "🇮🇹",
                "ES" or "SPAIN"                    => "🇪🇸",
                "RO" or "ROMANIA"                  => "🇷🇴",
                "RU" or "RUSSIA"                   => "🇷🇺",
                "JP" or "JAPAN"                    => "🇯🇵",
                "KR" or "SOUTH KOREA"              => "🇰🇷",
                "CN" or "CHINA"                    => "🇨🇳",
                "IN" or "INDIA"                    => "🇮🇳",
                "BR" or "BRAZIL"                   => "🇧🇷",
                "AU" or "AUSTRALIA"                => "🇦🇺",
                "CA" or "CANADA"                   => "🇨🇦",
                "TR" or "TURKEY"                   => "🇹🇷",
                "AR" or "ARGENTINA"                => "🇦🇷",
                "MX" or "MEXICO"                   => "🇲🇽",
                "PL" or "POLAND"                   => "🇵🇱",
                "NL" or "NETHERLANDS"              => "🇳🇱",
                _                                  => "📺"
            };
        }

        private Grid BuildPlayerOverlay()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(245, 0, 0, 0)) };

            _videoView = new VideoView { MediaPlayer = _player };
            grid.Children.Add(_videoView);

            _controlsBar = new Border
            {
                Background = DarkCard,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(24)
            };

            var ctrlGrid = new Grid();
            ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ctrlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _nowPlayingLabel = new TextBlock { Text = "Loading...", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_nowPlayingLabel, 0);
            ctrlGrid.Children.Add(_nowPlayingLabel);

            var centerStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var stopBtn = new Button { Content = "■ STOP", Style = (Style)Application.Current.FindResource("DarkBtn"), Margin = new Thickness(8, 0, 8, 0) };
            stopBtn.Click += (_, _) => StopAndClose();
            var pipBtn = new Button { Content = "🗔 PiP", Style = (Style)Application.Current.FindResource("DarkBtn"), Margin = new Thickness(8, 0, 8, 0) };
            pipBtn.Click += (_, _) => TogglePiP();
            centerStack.Children.Add(stopBtn);
            centerStack.Children.Add(pipBtn);
            Grid.SetColumn(centerStack, 1);
            ctrlGrid.Children.Add(centerStack);

            var volStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _muteBtn = new ToggleButton { Content = "🔊", Foreground = Brushes.White, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 20, Margin = new Thickness(0, 0, 12, 0) };
            _volSlider = new Slider { Width = 120, Minimum = 0, Maximum = 100, Value = AppConfig.GetInt("tv_default_vol", 60), VerticalAlignment = VerticalAlignment.Center };
            _volSlider.ValueChanged += (_, e) => { if (_player != null) _player.Volume = (int)e.NewValue; };
            volStack.Children.Add(_muteBtn);
            volStack.Children.Add(_volSlider);
            var fsBtn = new Button { Content = "⛶", Style = (Style)Application.Current.FindResource("DarkBtn"), Margin = new Thickness(16, 0, 0, 0), FontSize = 18 };
            fsBtn.Click += (_, _) => ToggleFullscreen();
            volStack.Children.Add(fsBtn);
            Grid.SetColumn(volStack, 2);
            ctrlGrid.Children.Add(volStack);

            _controlsBar.Child = ctrlGrid;
            grid.Children.Add(_controlsBar);

            var backBtn = new Button { Content = "← Back to Channels", Style = (Style)Application.Current.FindResource("DarkBtn"), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(24) };
            backBtn.Click += (_, _) => StopAndClose();
            grid.Children.Add(backBtn);

            _errorText = new TextBlock
            {
                Text = "[ STREAM OFFLINE OR INCOMPATIBLE ]",
                Foreground = LiveRed, FontWeight = FontWeights.Bold, FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            grid.Children.Add(_errorText);

            return grid;
        }

        private void PlayChannel(TvChannelInfo ch)
        {
            _nowPlayingLabel.Text = ch.Name;
            _errorText.Visibility = Visibility.Collapsed;
            _playerOverlay.Visibility = Visibility.Visible;

            using var media = new Media(_libVlc, new Uri(ch.StreamUrl));
            _player.Play(media);
            _player.Volume = (int)_volSlider.Value;
        }

        private void StopAndClose()
        {
            if (_isPip) TogglePiP();
            if (_isFullscreen) ToggleFullscreen();
            _player.Stop();
            _playerOverlay.Visibility = Visibility.Collapsed;
        }

        private void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                _mainWindow.WindowStyle = WindowStyle.None;
                _mainWindow.WindowState = WindowState.Maximized;
                _mainWindow.ResizeMode = ResizeMode.NoResize;
                _controlsBar.Visibility = Visibility.Collapsed;
                _playerOverlay.Margin = new Thickness(-_mainWindow.GetSidebarWidth(), -_mainWindow.GetHeaderHeight(), 0, 0);
            }
            else
            {
                _mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.ResizeMode = ResizeMode.CanResize;
                _controlsBar.Visibility = Visibility.Visible;
                _playerOverlay.Margin = new Thickness(0);
            }
        }

        private void TogglePiP()
        {
            _isPip = !_isPip;
            if (_isPip)
            {
                _pipHostWindow = new Window
                {
                    Title = "SONA PiP",
                    Width = 400, Height = 225,
                    WindowStyle = WindowStyle.None,
                    Topmost = true, ShowInTaskbar = false,
                    Background = Brushes.Black, ResizeMode = ResizeMode.CanResizeWithGrip
                };
                _playerOverlay.Children.Remove(_videoView);
                _pipHostWindow.Content = _videoView;
                _pipHostWindow.Left = SystemParameters.PrimaryScreenWidth - 420;
                _pipHostWindow.Top = SystemParameters.PrimaryScreenHeight - 245;
                _pipHostWindow.Show();
                _playerOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (_pipHostWindow != null) _pipHostWindow.Content = null;
                _pipHostWindow?.Close();
                _pipHostWindow = null;
                _playerOverlay.Children.Insert(0, _videoView);
                _playerOverlay.Visibility = Visibility.Visible;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_isPip) _pipHostWindow?.Close();
                _player.Stop();
                _player.Dispose();
                _libVlc.Dispose();
                _videoView.Dispose();
            }
            catch { }
        }
    }
}
