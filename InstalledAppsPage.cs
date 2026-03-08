using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using Microsoft.Web.WebView2.Wpf;
using SONA.Services;

namespace SONA.Pages
{
    public class InstalledAppInfo
    {
        public string Name { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string IconPath { get; set; } = "";
    }

    public class InstalledAppsPage : UserControl
    {
        private readonly WrapPanel _appsPanel = new();
        private List<InstalledAppInfo> _installedApps = new();
        private readonly string _saveFilePath;
        private readonly YoutubeClient _yt = new();
        private WebView2? _preVideo;
        private Border? _preVideoContainer;
        private bool _isWebViewInitialized;

        // Scan progress overlay elements
        private Grid? _mainGrid;
        private Border? _scanOverlay;
        private TextBlock? _scanStatusText;
        private TextBlock? _scanAppsFoundText;
        private CancellationTokenSource? _scanCts;

        public InstalledAppsPage()
        {
            _saveFilePath = Path.Combine(AppConfig.DataDir, "installed_apps.json");
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            _mainGrid = new Grid { Margin = new Thickness(32) };
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });

            // ── Header ────────────────────────────────────────────────────────
            var headerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 32)
            };

            var icon = IconHelper.Img("nav/programs", 48);
            icon.Margin = new Thickness(0, 0, 24, 0);
            headerStack.Children.Add(icon);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Installed Apps",
                Foreground = Brushes.White,
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "All apps found on your PC — click any card to launch",
                Foreground = Brushes.Gray,
                FontSize = 14
            });
            headerStack.Children.Add(titleStack);

            // Add Custom App Button
            var addBtn = new Button
            {
                Content = "+ Add App",
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 12, 0),
                Height = 40
            };
            addBtn.Click += BtnAddApp_Click;
            headerStack.Children.Add(addBtn);

            // Re-scan Button
            var rescanBtn = new Button
            {
                Content = "↻ Full Scan",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Padding = new Thickness(16, 8, 16, 8),
                Height = 40
            };
            rescanBtn.Click += BtnRescan_Click;
            headerStack.Children.Add(rescanBtn);

            _mainGrid.Children.Add(headerStack);
            Grid.SetRow(headerStack, 0);

            // ── Apps Grid ─────────────────────────────────────────────────────
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 24, 0)
            };
            _appsPanel.Orientation = Orientation.Horizontal;
            scroll.Content = _appsPanel;
            Grid.SetColumn(scroll, 0);
            contentGrid.Children.Add(scroll);

            // ── Video Sidebar ─────────────────────────────────────────────────
            _preVideo = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                IsHitTestVisible = false
            };
            _ = InitializePreviewWebViewAsync();

            _preVideoContainer = new Border
            {
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Background = Brushes.Black,
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 160, 80, 255)),
                BorderThickness = new Thickness(1.5),
                Height = 210,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 20)
            };
            _preVideoContainer.Child = _preVideo;

            var videoSidebar = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            videoSidebar.Children.Add(_preVideoContainer);
            videoSidebar.Children.Add(new TextBlock
            {
                Text = "▶ Apps Showcase",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(4, 0, 0, 12)
            });

            Grid.SetColumn(videoSidebar, 1);
            contentGrid.Children.Add(videoSidebar);

            Grid.SetRow(contentGrid, 1);
            _mainGrid.Children.Add(contentGrid);

            // ── Scan Overlay (hidden by default) ─────────────────────────────
            _scanOverlay = BuildScanOverlay();
            Grid.SetRowSpan(_scanOverlay, 2);
            _mainGrid.Children.Add(_scanOverlay);

            Content = _mainGrid;

            Loaded += (_, _) =>
            {
                LoadApps();
                _ = LoadAppsPreviewAsync();
            };
            Unloaded += (_, _) =>
            {
                _scanCts?.Cancel();
                try { _preVideo?.Dispose(); } catch { }
            };
        }

        // ── Scan Overlay ──────────────────────────────────────────────────────
        private Border BuildScanOverlay()
        {
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 10, 10, 20)),
                CornerRadius = new CornerRadius(16),
                Visibility = Visibility.Collapsed,
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 520
            };

            stack.Children.Add(new TextBlock
            {
                Text = "🔍  Scanning your PC…",
                Foreground = Brushes.White,
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            });

            _scanStatusText = new TextBlock
            {
                Text = "Starting scan…",
                Foreground = new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xb8)),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(_scanStatusText);

            _scanAppsFoundText = new TextBlock
            {
                Text = "0 apps found",
                Foreground = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24)
            };
            stack.Children.Add(_scanAppsFoundText);

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Padding = new Thickness(24, 10, 24, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            cancelBtn.Click += (_, _) => _scanCts?.Cancel();
            stack.Children.Add(cancelBtn);

            overlay.Child = stack;
            return overlay;
        }

        // ── WebView preview ───────────────────────────────────────────────────
        private async Task InitializePreviewWebViewAsync()
        {
            if (_preVideo == null || _isWebViewInitialized) return;
            try
            {
                await WebViewService.InitializeWebViewAsync(_preVideo);
                _isWebViewInitialized = true;
            }
            catch { }
        }

        private async Task LoadAppsPreviewAsync()
        {
            try
            {
                await foreach (var video in _yt.Search.GetVideosAsync("best pc software 2024 apps"))
                {
                    var manifest = await _yt.Videos.Streams.GetManifestAsync(video.Id);
                    var streamInfo = manifest.GetMuxedStreams()
                        .Where(s => s.Container.Name.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                        .Where(s => s.VideoQuality.MaxHeight <= 720)
                        .OrderByDescending(s => s.VideoQuality.MaxHeight)
                        .FirstOrDefault();

                    if (streamInfo != null && _preVideo != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            string html = $@"<!DOCTYPE html><html><head><style>
                                body,html{{margin:0;padding:0;width:100%;height:100%;overflow:hidden;background:black;}}
                                video{{width:100%;height:100%;object-fit:cover;}}
                            </style></head><body>
                            <video id='p' autoplay loop muted playsinline>
                                <source src='{streamInfo.Url}' type='video/mp4'>
                            </video>
                            <script>document.getElementById('p').play();</script>
                            </body></html>";
                            _preVideo.NavigateToString(html);
                        });
                        break;
                    }
                }
            }
            catch { }
        }

        // ── App loading ───────────────────────────────────────────────────────
        private void LoadApps()
        {
            _installedApps.Clear();

            if (File.Exists(_saveFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_saveFilePath);
                    var saved = JsonConvert.DeserializeObject<List<InstalledAppInfo>>(json);
                    if (saved != null) _installedApps.AddRange(saved);
                }
                catch { }
            }

            // First run — kick off the full async scan automatically
            if (_installedApps.Count == 0)
            {
                _ = RunFullScanAsync();
            }
            else
            {
                RefreshUI();
            }
        }

        private void SaveApps()
        {
            try
            {
                Directory.CreateDirectory(AppConfig.DataDir);
                File.WriteAllText(_saveFilePath, JsonConvert.SerializeObject(_installedApps, Formatting.Indented));
            }
            catch { }
        }

        // ── Full async scan ───────────────────────────────────────────────────
        private async Task RunFullScanAsync()
        {
            // Cancel any in-progress scan
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            // Show overlay
            if (_scanOverlay != null) _scanOverlay.Visibility = Visibility.Visible;

            int foundCount = 0;

            var progress = new Progress<string>(msg =>
            {
                if (_scanStatusText != null) _scanStatusText.Text = msg;
                if (_scanAppsFoundText != null) _scanAppsFoundText.Text = $"{foundCount} apps found";
            });

            try
            {
                var discovered = await AppManagerService.ScanAllInstalledAppsAsync(progress, ct);

                if (!ct.IsCancellationRequested)
                {
                    // Merge results — avoid duplicates
                    foreach (var (name, exe) in discovered)
                    {
                        if (!_installedApps.Any(a => a.ExecutablePath.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                        {
                            _installedApps.Add(new InstalledAppInfo
                            {
                                Name = name,
                                ExecutablePath = exe
                            });
                            foundCount++;

                            // Update counter live
                            if (_scanAppsFoundText != null)
                                Dispatcher.InvokeAsync(() => _scanAppsFoundText.Text = $"{foundCount} apps found");
                        }
                    }

                    SaveApps();
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                // Hide overlay
                if (_scanOverlay != null) _scanOverlay.Visibility = Visibility.Collapsed;
                RefreshUI();
            }
        }

        // ── UI building ───────────────────────────────────────────────────────
        private void RefreshUI()
        {
            _appsPanel.Children.Clear();

            if (_installedApps.Count == 0)
            {
                _appsPanel.Children.Add(new TextBlock
                {
                    Text = "No apps found. Try ↻ Full Scan or + Add App.",
                    Foreground = Brushes.Gray,
                    FontSize = 16,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            // Known SONA apps that haven't been mapped yet → show "not mapped" card
            foreach (var kvp in AppManagerService.AllApps)
            {
                string id = kvp.Key;
                var info = kvp.Value;
                string? savedPath = AppConfig.GetString($"{id}_exe_path");

                bool alreadyMapped = !string.IsNullOrEmpty(savedPath) && File.Exists(savedPath);
                bool inList = _installedApps.Any(a =>
                    a.Name.Equals(info.Name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(savedPath) && a.ExecutablePath.Equals(savedPath, StringComparison.OrdinalIgnoreCase)));

                if (!alreadyMapped && !inList)
                {
                    _appsPanel.Children.Add(CreateUnmappedCard(id, info));
                }
            }

            foreach (var app in _installedApps)
            {
                _appsPanel.Children.Add(CreateAppCard(app));
            }
        }

        // ── "Not mapped" card ─────────────────────────────────────────────────
        private Border CreateUnmappedCard(string appId, AppInfo info)
        {
            var card = new Border
            {
                Width = 140, Height = 160,
                Margin = new Thickness(0, 0, 16, 16),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(24, 16, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 50, 160)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ClipToBounds = true,
                ToolTip = $"{info.Name} — not found on this PC. Click to map EXE."
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            stack.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0xfa, 0xcc, 0x15)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = info.Name,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var mapBtn = new Button
            {
                Content = "Map EXE",
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mapBtn.Click += (_, e) =>
            {
                e.Handled = true;
                BrowseAndMapExe(appId, info.Name);
            };
            stack.Children.Add(mapBtn);

            card.Child = stack;
            return card;
        }

        private void BrowseAndMapExe(string appId, string appName)
        {
            var dialog = new OpenFileDialog
            {
                Title = $"Locate {appName} Executable",
                Filter = "Executable Files (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string chosen = dialog.FileName;
                AppConfig.Set($"{appId}_exe_path", chosen);

                // Add to the list and refresh
                if (!_installedApps.Any(a => a.ExecutablePath.Equals(chosen, StringComparison.OrdinalIgnoreCase)))
                {
                    _installedApps.Add(new InstalledAppInfo
                    {
                        Name = appName,
                        ExecutablePath = chosen
                    });
                    SaveApps();
                }

                SoundService.PlaySelect();
                RefreshUI();
            }
        }

        // ── Regular app card ──────────────────────────────────────────────────
        private Border CreateAppCard(InstalledAppInfo app)
        {
            var card = new Border
            {
                Width = 140, Height = 160,
                Margin = new Thickness(0, 0, 16, 16),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var grid = new Grid();

            // Hover overlay
            var hoverOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Opacity = 0,
                IsHitTestVisible = false
            };
            grid.Children.Add(hoverOverlay);

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(12)
            };

            // Icon
            ImageSource? iconSource = null;
            if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
            {
                try { iconSource = new BitmapImage(new Uri(app.IconPath)); } catch { }
            }

            if (iconSource == null && File.Exists(app.ExecutablePath))
            {
                try
                {
                    var ico = System.Drawing.Icon.ExtractAssociatedIcon(app.ExecutablePath);
                    if (ico != null)
                    {
                        var bmp = ico.ToBitmap();
                        var stream = new System.IO.MemoryStream();
                        bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Position = 0;
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = stream;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        iconSource = bi;
                    }
                }
                catch { }
            }

            if (iconSource != null)
            {
                var img = new Image
                {
                    Source = iconSource,
                    Width = 64, Height = 64,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                stack.Children.Add(img);
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "📦",
                    FontSize = 40,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = app.Name,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 120
            });

            grid.Children.Add(stack);

            // Context menu
            var ctxMenu = new ContextMenu();

            var reMapMenu = new MenuItem { Header = "📁  Re-map EXE…" };
            reMapMenu.Click += (_, _) =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = $"Locate {app.Name}",
                    Filter = "Executable Files (*.exe)|*.exe",
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() == true)
                {
                    app.ExecutablePath = dialog.FileName;
                    SaveApps();
                    RefreshUI();
                }
            };
            ctxMenu.Items.Add(reMapMenu);

            var removeMenu = new MenuItem { Header = "🗑  Remove" };
            removeMenu.Click += (_, _) =>
            {
                _installedApps.Remove(app);
                SaveApps();
                RefreshUI();
            };
            ctxMenu.Items.Add(removeMenu);
            card.ContextMenu = ctxMenu;

            card.Child = grid;

            card.MouseEnter += (_, _) =>
            {
                SoundService.PlayHover();
                hoverOverlay.Opacity = 1;
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
            };
            card.MouseLeave += (_, _) =>
            {
                hoverOverlay.Opacity = 0;
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            };
            card.MouseLeftButtonDown += (_, _) => LaunchApp(app);

            return card;
        }

        private void LaunchApp(InstalledAppInfo app)
        {
            if (File.Exists(app.ExecutablePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = app.ExecutablePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(app.ExecutablePath)
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch {app.Name}:\n{ex.Message}", "SONA");
                }
            }
            else
            {
                if (MessageBox.Show($"Executable not found at:\n{app.ExecutablePath}\n\nRemove this shortcut?",
                    "App Not Found", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _installedApps.Remove(app);
                    SaveApps();
                    RefreshUI();
                }
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────
        private void BtnAddApp_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Application Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                var exePath = dialog.FileName;
                var defaultName = CapitalizeFirstLetter(Path.GetFileNameWithoutExtension(exePath));

                if (_installedApps.Any(a => a.ExecutablePath.Equals(exePath, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("This app is already in the list.", "SONA");
                    return;
                }

                _installedApps.Add(new InstalledAppInfo
                {
                    Name = defaultName,
                    ExecutablePath = exePath
                });

                SaveApps();
                RefreshUI();
            }
        }

        private void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            _ = RunFullScanAsync();
        }

        private string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return char.ToUpper(str[0]) + str.Substring(1);
        }
    }
}
