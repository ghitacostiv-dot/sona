using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using SONA.Services;

namespace SONA.Pages
{
    public class VideoPlayerPage : Page
    {
        private readonly string _url;
        private readonly string _title;
        private readonly MainWindow _mainWindow;
        private VideoView _videoView;
        private LibVLC _libVlc;
        private LibVLCSharp.Shared.MediaPlayer _player;
        private readonly DispatcherTimer _hideTimer;
        private bool _isUserSeeking = false;
        private bool _controlsVisible = true;
        private bool _isPlayingInternal = true;

        private Slider _progressSlider = null!;
        private TextBlock _timeLabel = null!;
        private TextBlock _torrentStatsLabel = null!;
        private string? _torrentInfoHash;
        private DispatcherTimer _statsPollTimer = null!;
        private Border _controlsOverlay = null!;
        private Image _playPauseIcon = null!;
        
        // Track the length to avoid cross-thread issues
        private long _mediaLength = 0;

        public VideoPlayerPage(string url, string title, MainWindow mainWindow, ScrapeHeaders? headers = null)
        {
            _url = url;
            _title = title;
            _mainWindow = mainWindow;

            Background = Brushes.Black;

            // Initialize LibVLC
            Core.Initialize();
            _libVlc = new LibVLC(
                "--ffmpeg-hw",
                "--avcodec-hw=any",
                "--directx-hw-decoding=1",
                "--network-caching=10000",
                "--http-continuous",
                "--http-reconnect"
            );
            _player = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
            
            _videoView = new VideoView { MediaPlayer = _player };

            var grid = new Grid();
            grid.Children.Add(_videoView);

            _controlsOverlay = CreateControlsOverlay();
            grid.Children.Add(_controlsOverlay);

            Content = grid;
            Focusable = true;

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _hideTimer.Tick += (_, _) => HideControls();

            _player.LengthChanged += Player_LengthChanged;
            _player.TimeChanged += Player_TimeChanged;
            _player.EndReached += (_, _) => Application.Current.Dispatcher.Invoke(() => { if (_mainWindow.IsFullscreen) _mainWindow.ToggleFullscreen(false); _mainWindow.Navigate("movies"); });

            // Detect if this is a torrent stream to enable stats polling
            if (_url.Contains("/api/stream/"))
            {
                var parts = _url.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var streamIdx = Array.IndexOf(parts, "stream");
                if (streamIdx >= 0 && streamIdx + 1 < parts.Length)
                {
                    _torrentInfoHash = parts[streamIdx + 1].ToLowerInvariant();
                    _statsPollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    _statsPollTimer.Tick += async (s, e) => await UpdateTorrentStats();
                    _statsPollTimer.Start();
                }
            }

            MouseMove += (_, _) => ShowControls();
            PreviewKeyDown += (s, ev) =>
            {
                if (ev.Key == Key.F11) { _mainWindow.ToggleFullscreen(!_mainWindow.IsFullscreen); ev.Handled = true; }
            };

            Loaded += (s, e) => {
                Focus();
                try {
                    Console.WriteLine($"[VIDEOPLAYER] Attempting to play: {url}");
                    Console.WriteLine($"[VIDEOPLAYER] Headers: {headers?.Referer}, {headers?.UserAgent}, {headers?.Origin}");
                    
                    var playUrl = _url;
                    if (_url.Contains("localhost") || _url.Contains("127.0.0.1")) {
                        playUrl += _url.Contains("?") ? "&vlc=1" : "?vlc=1";
                    }
                    var uri = playUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(playUrl) : new Uri(System.IO.Path.GetFullPath(playUrl));
                    var media = new Media(_libVlc, uri);
                    
                    // Add streaming options for better performance
                    media.AddOption(":network-caching=10000");
                    media.AddOption(":http-continuous");
                    media.AddOption(":http-reconnect");
                    
                    if (headers != null) {
                        if (!string.IsNullOrEmpty(headers.Referer)) {
                            media.AddOption($":http-referrer={headers.Referer}");
                            Console.WriteLine($"[VIDEOPLAYER] Added referer: {headers.Referer}");
                        }
                        if (!string.IsNullOrEmpty(headers.UserAgent)) {
                            media.AddOption($":http-user-agent={headers.UserAgent}");
                            Console.WriteLine($"[VIDEOPLAYER] Added user-agent: {headers.UserAgent}");
                        }
                        if (!string.IsNullOrEmpty(headers.Origin)) {
                            media.AddOption($":http-origin={headers.Origin}");
                            Console.WriteLine($"[VIDEOPLAYER] Added origin: {headers.Origin}");
                        }
                    }
                    
                    _player.Media = media;
                    _player.Play();
                    ShowControls();
                    
                    Console.WriteLine($"[VIDEOPLAYER] Successfully started playback");
                } catch (Exception ex) {
                    Console.WriteLine($"[VIDEOPLAYER] Failed to open media: {ex.Message}");
                    MessageBox.Show($"Failed to open media: {ex.Message}\n\nURL: {url}\n\nThis might be due to:\n- Invalid stream URL\n- Network connectivity issues\n- Source blocking access\n\nTry a different source or check your internet connection.");
                }
            };

            Unloaded += (s, e) => {
                _hideTimer.Stop();
                _statsPollTimer?.Stop();
                _player.Stop();
                _player.Dispose();
                _libVlc.Dispose();
                _videoView.Dispose();
            };
        }

        private Border CreateControlsOverlay()
        {
            var overlay = new Border
            {
                Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(180, 0, 0, 0), 90),
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TOP BAR
            var topBar = new DockPanel { Margin = new Thickness(24) };
            var backBtn = new Button { Style = (Style)Application.Current.FindResource("DarkBtn"), Content = "← BACK", Width = 80, Height = 32 };
            backBtn.Click += (_, _) => { if (_mainWindow.IsFullscreen) _mainWindow.ToggleFullscreen(false); _mainWindow.Navigate("movies"); };
            DockPanel.SetDock(backBtn, Dock.Left);
            topBar.Children.Add(backBtn);

            var titleLabel = new TextBlock { Text = _title, Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0) };
            topBar.Children.Add(titleLabel);
            Grid.SetRow(topBar, 0);
            mainGrid.Children.Add(topBar);

            // BOTTOM BAR
            var bottomStack = new StackPanel { Margin = new Thickness(24, 0, 24, 24) };
            
            _progressSlider = new Slider { Style = (Style)Application.Current.FindResource("SpotifySlider"), Minimum = 0, Maximum = 100, Margin = new Thickness(0, 0, 0, 16) };
            _progressSlider.PreviewMouseLeftButtonDown += (_, _) => _isUserSeeking = true;
            _progressSlider.PreviewMouseLeftButtonUp += (_, _) => { SeekToSlider(); _isUserSeeking = false; };
            bottomStack.Children.Add(_progressSlider);

            var controlRow = new DockPanel();
            
            var leftControls = new StackPanel { Orientation = Orientation.Horizontal };
            var playPauseBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Width = 40, Height = 40, Cursor = Cursors.Hand };
            _playPauseIcon = IconHelper.Img("player/pause", 24);
            playPauseBtn.Content = _playPauseIcon;
            playPauseBtn.Click += (_, _) => TogglePlayPause();
            leftControls.Children.Add(playPauseBtn);

            _timeLabel = new TextBlock { Text = "0:00 / 0:00", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            leftControls.Children.Add(_timeLabel);

            _torrentStatsLabel = new TextBlock { Visibility = Visibility.Collapsed, Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 128)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0), FontSize = 12, FontWeight = FontWeights.Medium };
            leftControls.Children.Add(_torrentStatsLabel);

            DockPanel.SetDock(leftControls, Dock.Left);
            controlRow.Children.Add(leftControls);

            var rightControls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var volIcon = IconHelper.Img("player/volume", 18);
            volIcon.Opacity = 1.0;
            volIcon.Visibility = Visibility.Visible;
            rightControls.Children.Add(volIcon);
            
            var initialVol = AppConfig.GetDouble("volume", 0.7);
            var volSlider = new Slider { Width = 100, Value = initialVol * 100, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            volSlider.Opacity = 1.0;
            volSlider.Visibility = Visibility.Visible;
            volSlider.IsHitTestVisible = true;
            _player.Volume = (int)(volSlider.Value);

            AppConfig.VolumeChanged += (vol) => {
                _player.Volume = (int)(vol * 100);
                if (Math.Abs(volSlider.Value - (vol * 100)) > 0.1)
                    volSlider.Value = vol * 100;
            };

            volSlider.ValueChanged += (s, e) => { 
                _player.Volume = (int)e.NewValue; 
                if (Math.Abs(AppConfig.GetDouble("volume", 0.7) - (e.NewValue / 100.0)) > 0.01)
                    AppConfig.Set("volume", e.NewValue / 100.0);
            };
            rightControls.Children.Add(volSlider);

            var fullscreenBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Width = 36, Height = 36, Cursor = Cursors.Hand, ToolTip = "Full screen (F11)", Content = new TextBlock { Text = "⛶", Foreground = Brushes.White, FontSize = 18, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center } };
            fullscreenBtn.Click += (_, _) => _mainWindow.ToggleFullscreen(!_mainWindow.IsFullscreen);
            rightControls.Children.Add(fullscreenBtn);

            DockPanel.SetDock(rightControls, Dock.Right);
            controlRow.Children.Add(rightControls);

            bottomStack.Children.Add(controlRow);
            Grid.SetRow(bottomStack, 2);
            mainGrid.Children.Add(bottomStack);

            overlay.Child = mainGrid;
            return overlay;
        }

        private void Player_LengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
        {
            _mediaLength = e.Length;
            Application.Current.Dispatcher.InvokeAsync(() => {
                _progressSlider.Maximum = e.Length;
            });
        }

        private void Player_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            if (!_isUserSeeking && _mediaLength > 0)
            {
                Application.Current.Dispatcher.InvokeAsync(() => {
                    _progressSlider.Value = e.Time;
                    _timeLabel.Text = $"{FormatTime(TimeSpan.FromMilliseconds(e.Time))} / {FormatTime(TimeSpan.FromMilliseconds(_mediaLength))}";
                });
            }
        }

        private void TogglePlayPause()
        {
            if (_isPlayingInternal)
            {
                _player.Pause();
                _playPauseIcon.Source = IconHelper.Get("player/play");
            }
            else
            {
                _player.Play();
                _playPauseIcon.Source = IconHelper.Get("player/pause");
            }
            _isPlayingInternal = !_isPlayingInternal;
        }

        private void SeekToSlider()
        {
            _player.Time = (long)_progressSlider.Value;
        }

        private void ShowControls()
        {
            _controlsOverlay.Opacity = 1;
            _controlsOverlay.IsHitTestVisible = true;
            _controlsVisible = true;
            _hideTimer.Stop();
            _hideTimer.Start();
            Cursor = Cursors.Arrow;
        }

        private void HideControls()
        {
            if (_isUserSeeking) return;
            // Keep volume controls always visible - only fade other controls
            if (_controlsOverlay.Child is Grid mainGrid && mainGrid.Children.Count > 2)
            {
                var bottomStack = mainGrid.Children[2] as StackPanel;
                if (bottomStack?.Children.Count > 1)
                {
                    var controlRow = bottomStack.Children[1] as DockPanel;
                    if (controlRow?.Children.Count > 1)
                    {
                        var rightControls = controlRow.Children[1] as StackPanel;
                        if (rightControls?.Children.Count > 1)
                        {
                            var volIcon = rightControls.Children[0] as Image;
                            var volSlider = rightControls.Children[1] as Slider;
                            
                            if (volIcon != null) volIcon.Opacity = 1.0;
                            if (volSlider != null) volSlider.Opacity = 1.0;
                        }
                    }
                }
            }
            
            _controlsOverlay.Opacity = 0.3; // Keep slight opacity instead of completely hiding
            _controlsOverlay.IsHitTestVisible = false;
            _controlsVisible = false;
            Cursor = Cursors.None;
        }

        private async Task UpdateTorrentStats()
        {
            if (string.IsNullOrEmpty(_torrentInfoHash)) return;
            var stats = await NexusTorrentService.GetStatsAsync(_torrentInfoHash);
            if (stats != null)
            {
                _torrentStatsLabel.Visibility = Visibility.Visible;
                string speedText = stats.DownloadSpeed > 1024 * 1024 
                    ? $"{(stats.DownloadSpeed / 1024 / 1024):0.0} MB/s" 
                    : $"{(stats.DownloadSpeed / 1024):0.0} KB/s";
                
                _torrentStatsLabel.Text = $"PEERS: {stats.NumPeers}  |  SPEED: {speedText}  |  {(stats.Progress * 100):0.0}%";
            }
        }

        private string FormatTime(TimeSpan ts) => $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }
}
