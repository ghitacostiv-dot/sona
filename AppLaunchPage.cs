using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using Microsoft.Win32;
using SONA.Services;
using SONA.Controls;

namespace SONA.Pages
{
    /// <summary>
    /// Per-app launch page. 
    /// Tier 1: App found → show Launch button.
    /// Tier 2: Not found → try silent Winget install.
    /// Tier 3: Install fails → show download link + "Browse for EXE" button.
    /// </summary>
    public class AppLaunchPage : UserControl
    {
        private readonly string _appId;
        private TextBlock _statusText   = new();
        private TextBlock _exePathText  = new();
        private Button   _launchBtn     = new();
        private Button   _browseBtn     = new();
        private Button   _downloadBtn   = new();
        private Border   _fallbackPanel = new();
        private StackPanel _mainContent = new();
        private Grid     _hostContainer = new();
        private NativeWindowHost? _host;

        // Official download URLs for each app
        private static readonly System.Collections.Generic.Dictionary<string, string> DownloadUrls = new()
        {
            { "brave",      "https://brave.com/download/" },
            { "yacreader",  "https://www.yacreader.com/" },
            { "poddr",      "https://sn8z.github.io/Poddr/" },
            { "retrobat",   "https://www.retrobat.org/" },
            { "osu",        "https://osu.ppy.sh/home/download" },
            { "tailscale",  "https://tailscale.com/download/windows" },
            { "moonlight",  "https://moonlight-stream.org/" },
            { "steam",      "https://store.steampowered.com/about/" },
            { "epic",       "https://store.epicgames.com/en-US/download" },
            { "spotify",    "https://www.spotify.com/download/windows/" },
            { "discord",    "https://discord.com/download" },
            { "whatsapp",   "https://www.whatsapp.com/download/" },
            { "battlenet",  "https://www.blizzard.com/en-us/apps/battle.net/desktop" },
            { "nuclear",    "https://github.com/nukeop/nuclear/releases" },
            { "miru",       "https://github.com/ThaUnknown/miru/releases" },
            { "librum",     "https://librumreader.com/" },
            { "mangayomi",  "https://github.com/kodjodevf/mangayomi/releases" },
            { "hydra",      "https://github.com/hydralauncher/hydra/releases" },
        };

        public AppLaunchPage(string appId)
        {
            _appId = appId;
            var appInfo  = AppManagerService.GetApp(appId);
            string appName = appInfo?.Name ?? appId;

            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var root = new Grid();
            
            // ── Main Content (Setup/Status) ──────────────────────────────────
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _mainContent = new StackPanel
            {
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 560,
                Margin   = new Thickness(32)
            };
            scroll.Content = _mainContent;
            root.Children.Add(scroll);

            // ── Host Container (Hidden until launch) ─────────────────────────
            _hostContainer = new Grid { Visibility = Visibility.Collapsed, Background = Brushes.Black };
            root.Children.Add(_hostContainer);

            // ── Icon ──────────────────────────────────────────────────────────
            var iconImg = IconHelper.Img("nav/programs", 64);
            iconImg.Margin = new Thickness(0, 0, 0, 20);
            _mainContent.Children.Add(iconImg);

            // ── Title ─────────────────────────────────────────────────────────
            _mainContent.Children.Add(new TextBlock
            {
                Text                = appName,
                Foreground          = Brushes.White,
                FontSize            = 32,
                FontWeight          = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 6)
            });

            // ── Status line ───────────────────────────────────────────────────
            _statusText = new TextBlock
            {
                Text                = "Scanning your PC for the app…",
                Foreground          = new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xb0)),
                FontSize            = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(0, 0, 0, 24)
            };
            _mainContent.Children.Add(_statusText);

            // ── EXE path hint ─────────────────────────────────────────────────
            _exePathText = new TextBlock
            {
                Foreground          = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
                FontSize            = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping        = TextWrapping.Wrap,
                Margin              = new Thickness(0, 0, 0, 20),
                Visibility          = Visibility.Collapsed,
            };
            _mainContent.Children.Add(_exePathText);

            // ── Launch button ─────────────────────────────────────────────────
            _launchBtn = new Button
            {
                Content    = "▶  LAUNCH",
                Style      = (Style)Application.Current.FindResource("AccentBtn"),
                Width      = 220, Height = 48,
                FontSize   = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            _launchBtn.Click += LaunchBtn_Click;
            _mainContent.Children.Add(_launchBtn);

            // ── Fallback panel (shown only when auto-install fails) ───────────
            _fallbackPanel = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x24)),
                CornerRadius    = new CornerRadius(12),
                Padding         = new Thickness(24),
                Margin          = new Thickness(0, 8, 0, 0),
                BorderThickness = new Thickness(1),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0x3b, 0x3b, 0x55)),
                Visibility      = Visibility.Collapsed
            };

            var fallbackStack = new StackPanel();

            fallbackStack.Children.Add(new TextBlock
            {
                Text         = "⚠️  Auto-install unavailable",
                Foreground   = new SolidColorBrush(Color.FromRgb(0xfa, 0xcc, 0x15)),
                FontSize     = 16,
                FontWeight   = FontWeights.SemiBold,
                Margin       = new Thickness(0, 0, 0, 8)
            });

            fallbackStack.Children.Add(new TextBlock
            {
                Text         = $"Please download and install {appName} manually, then click \"Browse for EXE\" to map it to SONA.",
                Foreground   = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize     = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 16)
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            // Download link
            _downloadBtn = new Button
            {
                Content             = "🌐  Download Page",
                Style               = (Style)Application.Current.FindResource("DarkBtn"),
                Padding             = new Thickness(16, 10, 16, 10),
                Margin              = new Thickness(0, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility          = Visibility.Collapsed
            };
            _downloadBtn.Click += DownloadBtn_Click;
            btnRow.Children.Add(_downloadBtn);

            // Browse for exe
            _browseBtn = new Button
            {
                Content = "📁  Browse for EXE",
                Style   = (Style)Application.Current.FindResource("DarkBtn"),
                Padding = new Thickness(16, 10, 16, 10)
            };
            _browseBtn.Click += BrowseBtn_Click;
            btnRow.Children.Add(_browseBtn);

            fallbackStack.Children.Add(btnRow);
            _fallbackPanel.Child = fallbackStack;
            _mainContent.Children.Add(_fallbackPanel);

            Content = root;

            Loaded += async (_, _) => await CheckAndPrepareApp();
        }

        // ────────────────────────────────────────────────────────────────────
        private async Task CheckAndPrepareApp()
        {
            // Brief async yield so the UI renders the "Scanning…" text first
            await Task.Delay(100);

            // Tier 1: Already found (AppConfig override OR scan found it)
            string? exe = AppManagerService.FindExecutable(_appId);
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                ShowReady(exe);
                return;
            }

            // Tier 2: Try silent Winget/GitHub install
            var appInfo = AppManagerService.GetApp(_appId);
            if (appInfo != null)
            {
                SetStatus($"⬇️  {appInfo.Name} not found — trying silent install via Winget…", Colors.Orange);
                bool success = await AppManagerService.InstallAppAsync(_appId, msg =>
                    Dispatcher.InvokeAsync(() => SetStatus(msg, Colors.Orange)));

                if (success)
                {
                    exe = AppManagerService.FindExecutable(_appId);
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe)) { ShowReady(exe); return; }
                }
            }

            // Tier 3: Show manual fallback
            ShowFallback();
        }

        // ────────────────────────────────────────────────────────────────────
        private void ShowReady(string exePath)
        {
            SetStatus($"✅  Found at: …{exePath}", Colors.LightGreen);
            _exePathText.Text        = exePath;
            _exePathText.Visibility  = Visibility.Visible;
            _launchBtn.Visibility    = Visibility.Visible;
            _fallbackPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowFallback()
        {
            SetStatus("❌  App not installed automatically.", Colors.Salmon);
            _launchBtn.Visibility     = Visibility.Collapsed;
            _fallbackPanel.Visibility = Visibility.Visible;

            if (DownloadUrls.TryGetValue(_appId, out _))
                _downloadBtn.Visibility = Visibility.Visible;
        }

        private void SetStatus(string text, Color colour)
        {
            _statusText.Text       = text;
            _statusText.Foreground = new SolidColorBrush(colour);
        }

        // ────────────────────────────────────────────────────────────────────
        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            string? exe = AppManagerService.FindExecutable(_appId);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                ShowFallback();
                return;
            }

            try
            {
                _mainContent.Visibility = Visibility.Collapsed;
                _hostContainer.Visibility = Visibility.Visible;

                if (_host == null)
                {
                    _host = new NativeWindowHost
                    {
                        ProcessNameToKill = Path.GetFileNameWithoutExtension(exe),
                        IsFloating = true,
                        KeepNativeControls = true,
                        IsSticky = false
                    };
                    _hostContainer.Children.Add(_host);
                }

                await _host.LoadAppAsync(exe, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _mainContent.Visibility = Visibility.Visible;
                _hostContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void DownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadUrls.TryGetValue(_appId, out string? url))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title            = "Select the app executable",
                Filter           = "Executable Files (*.exe)|*.exe",
                CheckFileExists  = true
            };

            if (dialog.ShowDialog() == true)
            {
                string chosen = dialog.FileName;
                AppConfig.Set($"{_appId}_exe_path", chosen);
                ShowReady(chosen);
                SoundService.PlaySelect();
            }
        }
    }
}
