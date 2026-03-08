using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SONA.Services;

namespace SONA.Pages
{
    public class NexusPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private WebView2 _webView;
        private Grid _mainGrid;
        private Border _loadingView;
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private int _retryCount = 0;
        private const int MaxRetries = 3;
        private bool _tweaksInjected = false;

        public NexusPage(MainWindow mainWindow, string subPath = "")
        {
            _mainWindow = mainWindow;
            Background = Brushes.Transparent;

            _mainGrid = new Grid();

            // Loading view
            _loadingView = CreateLoadingView("🎬  Nexus", "Starting streaming server...");
            _mainGrid.Children.Add(_loadingView);

            _webView = new WebView2
            {
                Visibility = Visibility.Hidden,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 13, 13, 20)
            };
            _mainGrid.Children.Add(_webView);
            Content = _mainGrid;

            Loaded += OnLoaded;
        }

        private Border CreateLoadingView(string title, string subtitle)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromRgb(0x0d, 0x0d, 0x14)) };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) });
            stack.Children.Add(new TextBlock { Text = subtitle, Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center });
            border.Child = stack;
            return border;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) { _webView.Focus(); return; }
            await InitializeAppAsync();
        }

        private async Task InitializeAppAsync()
        {
            UpdateLoadingText("Starting Nexus engine...");
            
            // Start services in parallel to save time (prefer built-in C# torrent streaming)
            var nexusTask = NexusService.StartAsync();
            var scraperTask = ScraperService.StartAsync();
            TorrentHttpServer.StartAsync();
            
            await Task.WhenAll(nexusTask, scraperTask);

            UpdateLoadingText("Initializing browser...");
            try
            {
                await WebViewService.InitializeWebViewAsync(_webView);

                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                _webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

                _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
                _webView.CoreWebView2.ContainsFullScreenElementChanged += async (s, ev) =>
                {
                    _mainWindow.ToggleFullscreen(_webView.CoreWebView2.ContainsFullScreenElement);
                    if (_webView.CoreWebView2.ContainsFullScreenElement)
                    {
                        await _webView.ExecuteScriptAsync(@"
                            document.documentElement.style.overflow='hidden';
                            document.body.style.overflow='hidden';
                            document.body.classList.add('sona-fullscreen');
                            document.body.style.margin='0';
                            document.body.style.padding='0';
                            document.body.style.border='none';
                            document.body.style.width='100vw';
                            document.body.style.height='100vh';
                            document.body.style.position='fixed';
                            document.body.style.top='0';
                            document.body.style.left='0';
                            document.body.style.zIndex='999999';
                            // Hide all scrollbars completely
                            const style = document.createElement('style');
                            style.textContent = '*::-webkit-scrollbar { display: none !important; } * { scrollbar-width: none !important; -ms-overflow-style: none !important; }';
                            document.head.appendChild(style);
                        ");
                    }
                    else
                    {
                        await _webView.ExecuteScriptAsync(@"
                            document.documentElement.style.overflow='';
                            document.body.style.overflow='';
                            document.body.classList.remove('sona-fullscreen');
                            document.body.style.margin='';
                            document.body.style.padding='';
                            document.body.style.border='';
                            document.body.style.width='';
                            document.body.style.height='';
                            document.body.style.position='';
                            document.body.style.top='';
                            document.body.style.left='';
                            document.body.style.zIndex='';
                        ");
                    }
                };

                _webView.CoreWebView2.WebMessageReceived += async (s, ev) =>
                {
                    try
                    {
                        var json = ev.WebMessageAsJson;
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        
                        if (root.TryGetProperty("action", out var actionProp))
                        {
                            string action = actionProp.GetString() ?? "";
                            
                            if (action == "SCRAPE_AND_PLAY")
                            {
                                var url = root.GetProperty("url").GetString();
                                var title = root.GetProperty("title").GetString();
                                
                                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                                {
                                    // Check if scraper service is running
                                    if (!ScraperService.IsRunning)
                                    {
                                        ShowError("Streaming service is not available. Please restart the app.");
                                        return;
                                    }
                                    
                                    Dispatcher.Invoke(() =>
                                    {
                                        UpdateLoadingText($"Finding best stream for: {title}...");
                                        _webView.Visibility = Visibility.Hidden;
                                        _loadingView.Visibility = Visibility.Visible;
                                    });
                                    
                                    try
                                    {
                                        var result = await ScraperService.ScrapeAsync(url, 15000);
                                        
                                        Dispatcher.Invoke(() =>
                                        {
                                            _loadingView.Visibility = Visibility.Collapsed;
                                            _webView.Visibility = Visibility.Visible;
                                            
                                            if (result != null && result.Success && !string.IsNullOrEmpty(result.StreamUrl))
                                            {
                                                // Log successful stream extraction
                                                Console.WriteLine($"[STREAMING] Successfully extracted stream: {result.StreamUrl}");
                                                Console.WriteLine($"[STREAMING] Provider: {result.Provider}");
                                                Console.WriteLine($"[STREAMING] Type: {result.StreamType}");
                                                
                                                // Play the extracted stream
                                                _mainWindow.PlayVideo(result.StreamUrl, title, result.Headers);
                                            }
                                            else
                                            {
                                                string errorMsg = result?.Error ?? "No playable stream found.";
                                                if (errorMsg.Contains("timeout")) 
                                                    errorMsg = "The source is taking too long to respond. Try another source or check your internet connection.";
                                                else if (errorMsg.Contains("No stream URLs intercepted"))
                                                    errorMsg = "This source doesn't appear to have playable video content. Try a different source.";
                                                else if (errorMsg.Contains("playwright"))
                                                    errorMsg = "Browser automation failed. The source may be blocking automated access.";
                                                    
                                                Console.WriteLine($"[STREAMING] Failed to extract stream: {errorMsg}");
                                                ShowError($"Streaming failed: {errorMsg}");
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            _loadingView.Visibility = Visibility.Collapsed;
                                            _webView.Visibility = Visibility.Visible;
                                            
                                            string errorMsg = ex.Message;
                                            if (errorMsg.Contains("timeout"))
                                                errorMsg = "Stream extraction timed out. The source may be slow or unavailable.";
                                            else if (errorMsg.Contains("connection"))
                                                errorMsg = "Network connection failed. Check your internet connection.";
                                                
                                            Console.WriteLine($"[STREAMING] Exception during scraping: {ex}");
                                            ShowError($"Streaming error: {errorMsg}");
                                        });
                                    }
                                }
                            }
                            else if (action == "PLAY_TORRENT")
                            {
                                var infoHash = root.GetProperty("infoHash").GetString();
                                var fileIndex = root.TryGetProperty("fileIndex", out var fi) ? fi.GetInt32() : 0;
                                var title = root.GetProperty("title").GetString();
                                
                                if (!string.IsNullOrEmpty(infoHash))
                                {
                                    // Prefer the Node.js server (NexusService) for torrents as it has advanced transcoding
                                    // and superior sequential picking logic.
                                    var baseUrl = NexusService.IsRunning ? NexusService.BaseUrl : (TorrentHttpServer.IsRunning ? TorrentHttpServer.BaseUrl : "http://localhost:3003");
                                    string streamUrl = $"{baseUrl}/api/stream/{infoHash.ToLowerInvariant()}/{fileIndex}";
                                    _mainWindow.PlayVideo(streamUrl, title, null);
                                }
                            }
                            else if (action == "PLAY_DIRECT")
                            {
                                var url = root.GetProperty("url").GetString();
                                var title = root.GetProperty("title").GetString();
                                ScrapeHeaders? headers = null;
                                if (root.TryGetProperty("headers", out var hdr))
                                {
                                    headers = new ScrapeHeaders
                                    {
                                        Referer = hdr.TryGetProperty("Referer", out var r) ? r.GetString() : (hdr.TryGetProperty("referer", out var rr) ? rr.GetString() : null),
                                        UserAgent = hdr.TryGetProperty("User-Agent", out var ua) ? ua.GetString() : (hdr.TryGetProperty("user-agent", out var ual) ? ual.GetString() : null),
                                        Origin = hdr.TryGetProperty("Origin", out var o) ? o.GetString() : null
                                    };
                                }
                                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                                {
                                    _mainWindow.PlayVideo(url, title, headers);
                                }
                            }
                            else if (action == "OPEN_QBITTORRENT")
                        {
                            var magnet = root.GetProperty("magnet").GetString();
                            if (!string.IsNullOrEmpty(magnet))
                            {
                                try
                                {
                                    Process.Start(new ProcessStartInfo(magnet) { UseShellExecute = true });
                                }
                                catch (Exception ex)
                                {
                                    ShowError($"Failed to open qBittorrent: {ex.Message}");
                                }
                            }
                        }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"WebMessage processing error: {ex.Message}");
                    }
                };

                _webView.CoreWebView2.NavigationCompleted += async (s, ev) =>
                {
                    if (ev.IsSuccess)
                    {
                        _loadingView.Visibility = Visibility.Collapsed;
                        _webView.Visibility = Visibility.Visible;
                        _webView.Focus();
                        _isInitialized = true;
                        _retryCount = 0;
                        if (!_tweaksInjected)
                        {
                            await InjectTweaksAsync();
                            _tweaksInjected = true;
                        }
                    }
                    else if (_retryCount < MaxRetries)
                    {
                        _retryCount++;
                        UpdateLoadingText($"Retrying connection... ({_retryCount}/{MaxRetries})");
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                        timer.Tick += (_, _) =>
                        {
                            timer.Stop();
                            _webView.CoreWebView2.Navigate(NexusService.BaseUrl);
                        };
                        timer.Start();
                    }
                    else
                    {
                        ShowError("Nexus server failed to respond. Make sure Node.js is installed and 'npm run dev' works in ~/Downloads/nexus");
                    }
                };

                _webView.CoreWebView2.Navigate(NexusService.BaseUrl);
            }
            catch (Exception ex)
            {
                ShowError($"WebView2 init failed: {ex.Message}");
            }
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs args)
        {
            if (args.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                _isInitialized = false;
                _ = RecreateAsync();
            }
            else
            {
                _webView.Reload();
            }
        }

        private async Task RecreateAsync()
        {
            _mainGrid.Children.Remove(_webView);
            try { _webView.Dispose(); } catch { }
            _webView = new WebView2 { Visibility = Visibility.Hidden, DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 13, 13, 20) };
            _mainGrid.Children.Add(_webView);
            _loadingView.Visibility = Visibility.Visible;
            _retryCount = 0;
            await InitializeAppAsync();
        }

        private void UpdateLoadingText(string subtitle)
        {
            Dispatcher.Invoke(() =>
            {
                if (_loadingView.Child is StackPanel sp && sp.Children.Count > 1)
                    ((TextBlock)sp.Children[1]).Text = subtitle;
                _loadingView.Visibility = Visibility.Visible;
            });
        }

        private void ShowError(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                if (_loadingView.Child is StackPanel sp)
                {
                    sp.Children.Clear();
                    sp.Children.Add(new TextBlock { Text = "⚠️  Nexus Error", Foreground = Brushes.OrangeRed, FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) });
                    sp.Children.Add(new TextBlock { Text = msg, Foreground = Brushes.Gray, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 500 });
                    var retryBtn = new Button { Content = "Retry", Style = (Style)Application.Current.FindResource("AccentBtn"), Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(20, 10, 20, 10), HorizontalAlignment = HorizontalAlignment.Center };
                    retryBtn.Click += async (_, _) => { _retryCount = 0; await InitializeAppAsync(); };
                    sp.Children.Add(retryBtn);
                }
                _loadingView.Visibility = Visibility.Visible;
            });
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            NexusService.Stop();
            try { ScraperService.Stop(); } catch { }
            try { TorrentHttpServer.Stop(); } catch { }
            try { _webView?.Dispose(); } catch { }
        }

        private async Task InjectTweaksAsync()
        {
            try
            {
                var css = @"
html, body { background-color: transparent !important; }
body.sona-fullscreen { overflow: hidden !important; }
::-webkit-scrollbar { width: 0 !important; height: 0 !important; }

/* Aggressive Volume Control Styling */
[class*=""volume""], [data-volume], [aria-label*=""volume""], 
video::-webkit-media-controls-volume-slider, 
video::-webkit-media-controls-mute-button,
video::-webkit-media-controls-volume-control-container {
  opacity: 1 !important; 
  visibility: visible !important; 
  display: block !important;
  transition: none !important;
  position: static !important;
}

/* Force all video controls to be visible */
video::-webkit-media-controls-panel { 
  display: flex !important; 
  opacity: 1 !important;
  background: rgba(0,0,0,0.8) !important;
}

video::-webkit-media-controls-play-pause-button { opacity: 1 !important; }
video::-webkit-media-controls-timeline { opacity: 1 !important; }
video::-webkit-media-controls-current-time-display { opacity: 1 !important; }
video::-webkit-media-controls-time-remaining-display { opacity: 1 !important; }
video::-webkit-media-controls-fullscreen-button { opacity: 1 !important; }

/* Enhanced Video Player Styles */
video { 
  background: #000 !important;
  border-radius: 8px !important;
}

video::-webkit-media-controls-enclosure { 
  background-color: rgba(0,0,0,0.7) !important; 
  border-radius: 0 0 8px 8px !important;
}

.sona-ep-card { display: inline-flex; flex-direction: column; width: 140px; margin: 8px; padding: 8px; border-radius: 10px; background: rgba(255,255,255,0.05); border: 1px solid rgba(255,255,255,0.08); cursor: pointer; transition: all 0.3s ease; }
.sona-ep-card:hover { transform: translateY(-4px); background: rgba(255,255,255,0.1); box-shadow: 0 8px 25px rgba(0,0,0,0.3); }
.sona-ep-card img { width: 100%; height: 80px; object-fit: cover; border-radius: 8px; margin-bottom: 6px; }
.sona-ep-card .t { color: #e5e7eb; font-size: 12px; font-weight: 600; text-align: center; }

.sona-overlay-btn { position: fixed; top: 12px; right: 12px; z-index: 999999; display: flex; gap: 8px; }
.sona-overlay-btn button { background: rgba(0,0,0,0.7) !important; color: white !important; border: 1px solid rgba(255,255,255,0.3) !important; border-radius: 8px !important; padding: 8px 12px !important; cursor: pointer !important; font-size: 13px !important; transition: all 0.2s ease !important; opacity: 1 !important; visibility: visible !important; display: block !important; }
.sona-overlay-btn button:hover { background: rgba(0,0,0,0.9) !important; border-color: rgba(255,255,255,0.5) !important; transform: scale(1.05) !important; }
.sona-overlay-panel { position: fixed; top: 44px; right: 12px; z-index: 999999; background: rgba(0,0,0,0.85) !important; backdrop-filter: blur(10px) !important; border: 1px solid rgba(255,255,255,0.3) !important; border-radius: 12px !important; padding: 12px !important; display: none !important; max-height: 50vh !important; overflow: auto !important; min-width: 200px !important; box-shadow: 0 10px 30px rgba(0,0,0,0.5) !important; }
.sona-overlay-panel.show { display: block !important; animation: fadeIn 0.2s ease !important; }
.sona-overlay-panel .row { color: #e5e7eb !important; font-size: 14px !important; padding: 8px 12px !important; border-radius: 8px !important; cursor: pointer !important; transition: all 0.2s ease !important; margin: 2px 0 !important; opacity: 1 !important; visibility: visible !important; }
.sona-overlay-panel .row:hover { background: rgba(255,255,255,0.15) !important; transform: translateX(-2px) !important; }
.sona-overlay-panel .row.active { background: rgba(59, 130, 246, 0.3) !important; border-left: 3px solid #3b82f6 !important; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }

.sona-media-logo { position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 999998; opacity: 0; animation: logoPulse 2s ease-in-out infinite; }
@keyframes logoPulse { 0%, 100% { transform: translate(-50%, -50%) scale(1); opacity: 0.3; } 50% { transform: translate(-50%, -50%) scale(1.1); opacity: 0.8; } }
.sona-loading-banner { position: fixed; top: 0; left: 0; width: 100%; height: 100%; z-index: 999997; background-size: cover; background-position: center; opacity: 0.3; filter: blur(3px); }

/* Custom Video Overlay Controls */
.sona-video-overlay { position: absolute; top: 10px; right: 10px; z-index: 999999; }
.sona-custom-controls { display: flex; gap: 8px; }
.sona-custom-controls button { background: rgba(0,0,0,0.8) !important; color: white !important; border: 1px solid rgba(255,255,255,0.3) !important; border-radius: 6px !important; width: 36px !important; height: 36px !important; cursor: pointer !important; font-size: 14px !important; transition: all 0.2s ease !important; opacity: 1 !important; visibility: visible !important; display: block !important; }
.sona-custom-controls button:hover { background: rgba(0,0,0,0.9) !important; transform: scale(1.1) !important; }

/* Force all media elements to show controls */
video, audio { 
  controls: true !important; 
}

/* Additional volume control targeting */
.vjs-volume-control, .vjs-volume-panel, .vjs-volume-bar, 
.vjs-mute-control, .vjs-volume-menu-button {
  opacity: 1 !important;
  visibility: visible !important;
  display: block !important;
}

/* Generic volume slider targeting */
input[type=""range""][class*=""volume""], 
input[type=""range""][aria-label*=""volume""] {
  opacity: 1 !important;
  visibility: visible !important;
  display: block !important;
}

/* Media Details Styles */
.sona-media-details { position: fixed; top: 0; left: 0; width: 100%; height: 100%; z-index: 999999; display: flex; align-items: center; justify-content: center; }
.sona-details-backdrop { position: absolute; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.8); backdrop-filter: blur(10px); }
.sona-details-content { position: relative; background: rgba(13, 13, 20, 0.95); border: 1px solid rgba(255,255,255,0.1); border-radius: 16px; max-width: 900px; max-height: 80vh; width: 90%; overflow: hidden; box-shadow: 0 25px 50px rgba(0,0,0,0.5); }
.sona-details-header { padding: 20px 24px; border-bottom: 1px solid rgba(255,255,255,0.1); display: flex; justify-content: space-between; align-items: center; }
.sona-details-header h2 { color: white; font-size: 24px; font-weight: bold; margin: 0; }
.sona-details-close { background: none; border: none; color: white; font-size: 28px; cursor: pointer; padding: 0; width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; border-radius: 6px; transition: background 0.2s ease; }
.sona-details-close:hover { background: rgba(255,255,255,0.1); }
.sona-details-body { display: flex; padding: 24px; gap: 24px; max-height: 60vh; overflow-y: auto; }
.sona-details-main { flex: 1; }
.sona-details-info { display: flex; gap: 12px; margin-bottom: 16px; }
.sona-details-year, .sona-details-rating { background: rgba(59, 130, 246, 0.2); color: #3b82f6; padding: 4px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
.sona-details-description { color: #e5e7eb; line-height: 1.6; margin-bottom: 20px; }
.sona-details-actions { display: flex; gap: 12px; }
.sona-btn-primary, .sona-btn-secondary, .sona-btn-trailer { padding: 10px 16px; border: none; border-radius: 8px; font-weight: 600; cursor: pointer; transition: all 0.2s ease; }
.sona-btn-primary { background: #3b82f6; color: white; }
.sona-btn-primary:hover { background: #2563eb; transform: translateY(-1px); }
.sona-btn-secondary { background: rgba(255,255,255,0.1); color: white; border: 1px solid rgba(255,255,255,0.2); }
.sona-btn-secondary:hover { background: rgba(255,255,255,0.2); }
.sona-btn-trailer { background: rgba(239, 68, 68, 0.2); color: #ef4444; border: 1px solid rgba(239, 68, 68, 0.3); }
.sona-btn-trailer:hover { background: rgba(239, 68, 68, 0.3); }
.sona-details-sidebar { width: 280px; }
.sona-cast-section, .sona-suggestions { margin-bottom: 24px; }
.sona-cast-section h4, .sona-suggestions h4 { color: white; font-size: 16px; font-weight: 600; margin-bottom: 12px; }
.sona-cast-item { display: flex; align-items: center; gap: 12px; padding: 8px; border-radius: 8px; cursor: pointer; transition: background 0.2s ease; }
.sona-cast-item:hover { background: rgba(255,255,255,0.05); }
.sona-cast-avatar { width: 40px; height: 40px; background: rgba(255,255,255,0.1); border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 18px; }
.sona-cast-name { color: white; font-weight: 600; font-size: 14px; }
.sona-cast-role { color: #9ca3af; font-size: 12px; }
.sona-suggestion-item { display: flex; align-items: center; gap: 12px; padding: 8px; border-radius: 8px; cursor: pointer; transition: background 0.2s ease; }
.sona-suggestion-item:hover { background: rgba(255,255,255,0.05); }
.sona-suggestion-poster { width: 40px; height: 60px; background: rgba(255,255,255,0.1); border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 16px; }
.sona-suggestion-title { color: white; font-weight: 600; font-size: 14px; }
.sona-suggestion-meta { color: #9ca3af; font-size: 12px; }
.sona-quick-preview { position: absolute; top: -40px; left: 50%; transform: translateX(-50%); background: rgba(0,0,0,0.9); color: white; padding: 8px 12px; border-radius: 6px; font-size: 12px; white-space: nowrap; z-index: 1000; pointer-events: none; }
.sona-quick-preview::after { content: ''; position: absolute; top: 100%; left: 50%; transform: translateX(-50%); border: 6px solid transparent; border-top-color: rgba(0,0,0,0.9); }
.sona-actor-modal { position: fixed; top: 0; left: 0; width: 100%; height: 100%; z-index: 999999; display: flex; align-items: center; justify-content: center; }
.sona-modal-backdrop { position: absolute; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.8); backdrop-filter: blur(10px); }
.sona-modal-content { position: relative; background: rgba(13, 13, 20, 0.95); border: 1px solid rgba(255,255,255,0.1); border-radius: 12px; max-width: 500px; width: 90%; max-height: 60vh; overflow: hidden; }
.sona-modal-header { padding: 16px 20px; border-bottom: 1px solid rgba(255,255,255,0.1); display: flex; justify-content: space-between; align-items: center; }
.sona-modal-header h3 { color: white; font-size: 18px; font-weight: bold; margin: 0; }
.sona-modal-close { background: none; border: none; color: white; font-size: 24px; cursor: pointer; padding: 0; width: 28px; height: 28px; display: flex; align-items: center; justify-content: center; border-radius: 4px; transition: background 0.2s ease; }
.sona-modal-close:hover { background: rgba(255,255,255,0.1); }
.sona-modal-body { padding: 20px; max-height: 50vh; overflow-y: auto; }
.sona-filmography-list { display: flex; flex-direction: column; gap: 8px; }
.sona-film-item { padding: 12px; background: rgba(255,255,255,0.05); border-radius: 6px; color: white; cursor: pointer; transition: background 0.2s ease; }
.sona-film-item:hover { background: rgba(255,255,255,0.1); }

/* Sorting and Filter Styles */
.sona-sort-control { margin-left: 12px; display: inline-block; }
.sona-sort-select { background: rgba(255,255,255,0.1); color: white; border: 1px solid rgba(255,255,255,0.2); border-radius: 6px; padding: 6px 12px; font-size: 13px; cursor: pointer; outline: none; }
.sona-sort-select:hover { background: rgba(255,255,255,0.15); border-color: rgba(255,255,255,0.3); }
.sona-sort-select:focus { border-color: #3b82f6; box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.2); }
.sona-sort-select option { background: #1a1a2e; color: white; }
.sona-filter-improved .active, .sona-filter-improved .selected { background: rgba(59, 130, 246, 0.2) !important; border-color: #3b82f6 !important; color: white !important; }
";
                var js = @"
(function() {
  const injectStyle = (id, css) => {
    if (document.getElementById(id)) return;
    const s = document.createElement('style'); s.id = id; s.textContent = css;
    document.documentElement.appendChild(s);
  };
  const keepVolumeVisible = () => {
    const candidates = Array.from(document.querySelectorAll('[class*=""volume""]'));
    candidates.forEach(el => {
      el.style.opacity = '1';
      el.style.visibility = 'visible';
      el.style.transition = 'none';
    });
  };
  const setupFullscreenHooks = () => {
    const handler = () => {
      if (document.fullscreenElement) {
        document.documentElement.style.overflow = 'hidden';
        document.body.style.overflow = 'hidden';
        document.body.classList.add('sona-fullscreen');
      } else {
        document.documentElement.style.overflow = '';
        document.body.style.overflow = '';
        document.body.classList.remove('sona-fullscreen');
      }
    };
    document.addEventListener('fullscreenchange', handler, { passive: true });
    handler();
  };
  const ensureSearchSuggestions = () => {
    const input = document.querySelector('input[type=""search""], input[name*=""search"" i], input[placeholder*=""search"" i]');
    if (!input) return;
    const dlId = 'sona-suggestions';
    let dl = document.getElementById(dlId);
    if (!dl) { dl = document.createElement('datalist'); dl.id = dlId; document.body.appendChild(dl); }
    input.setAttribute('list', dlId);
    const load = () => {
      const items = JSON.parse(localStorage.getItem('sonaSearchHistory') || '[]');
      dl.innerHTML = '';
      items.slice(-15).reverse().forEach(t => {
        const o = document.createElement('option'); o.value = t; dl.appendChild(o);
      });
    };
    const save = (q) => {
      if (!q || q.length < 2) return;
      const items = JSON.parse(localStorage.getItem('sonaSearchHistory') || '[]');
      if (!items.includes(q)) items.push(q);
      localStorage.setItem('sonaSearchHistory', JSON.stringify(items));
      load();
    };
    input.addEventListener('change', () => save(input.value));
    input.addEventListener('keydown', e => { if (e.key === 'Enter') save(input.value); });
    load();
  };
  const buildTracksUI = () => {
    let v = document.querySelector('video');
    if (!v) return;
    let wrap = document.querySelector('.sona-overlay-btn');
    if (!wrap) {
      wrap = document.createElement('div'); wrap.className = 'sona-overlay-btn';
      const btnSub = document.createElement('button'); btnSub.textContent = '📺 Subtitles';
      const btnAud = document.createElement('button'); btnAud.textContent = '🔊 Audio';
      wrap.appendChild(btnSub); wrap.appendChild(btnAud);
      document.body.appendChild(wrap);
      const panel = document.createElement('div'); panel.className = 'sona-overlay-panel'; document.body.appendChild(panel);
      const showPanel = (title, rows) => {
        panel.innerHTML = ''; panel.classList.add('show');
        rows.forEach(r => {
          const row = document.createElement('div'); row.className = 'row'; row.textContent = r.label;
          if (r.active) row.classList.add('active');
          row.addEventListener('click', r.onClick);
          panel.appendChild(row);
        });
        const closer = (ev) => { if (!panel.contains(ev.target) && !wrap.contains(ev.target)) { panel.classList.remove('show'); document.removeEventListener('click', closer); } };
        setTimeout(() => document.addEventListener('click', closer));
      };
      btnSub.addEventListener('click', () => {
        v = document.querySelector('video'); if (!v) return;
        const tracks = Array.from(v.textTracks || []);
        const rows = tracks.map((t, i) => ({
          label: (t.label || t.language || 'Track ' + (i+1)) + (t.kind === 'subtitles' ? ' (Sub)' : ' (CC)'),
          onClick: () => {
            tracks.forEach(x => x.mode = 'disabled');
            t.mode = 'showing';
            showPanel('Subtitles', rows.map(r => ({...r, active: r.label === ((t.label || t.language || 'Track ' + (i+1)) + (t.kind === 'subtitles' ? ' (Sub)' : ' (CC)'))})));
          }
        }));
        rows.unshift({ label: 'Off', onClick: () => { (v.textTracks ? Array.from(v.textTracks) : []).forEach(x => x.mode = 'disabled'); showPanel('Subtitles', rows.map(r => ({...r, active: r.label === 'Off'}))); }});
        showPanel('Subtitles', rows);
      });
      btnAud.addEventListener('click', () => {
        v = document.querySelector('video'); if (!v) return;
        const at = v.audioTracks;
        if (at && at.length) {
          const rows = Array.from(at).map((t, i) => ({
            label: (t.label || t.language || 'Audio ' + (i+1)),
            onClick: () => {
              Array.from(at).forEach((x, idx) => x.enabled = idx === i);
              showPanel('Audio', rows.map(r => ({...r, active: r.label === ((t.label || t.language || 'Audio ' + (i+1)))})));
            }
          }));
          showPanel('Audio', rows);
        } else {
          const rows = [{ label: 'Default audio (no alternate tracks detected)', onClick: () => {} }];
          showPanel('Audio', rows);
        }
      });
    }
  };
  const styleEpisodesAsCards = () => {
    const containers = Array.from(document.querySelectorAll('[class*=""episode""] , [id*=""episode""] , ul li[data-episode]'));
    if (!containers.length) return;
    containers.forEach(el => {
      if (el.classList.contains('sona-processed')) return;
      const items = el.querySelectorAll('li, a, div');
      let changed = false;
      items.forEach(it => {
        const txt = (it.textContent || '').trim();
        if (txt.match(/ep(\\.|isode)?\\s*\\d+/i) || it.getAttribute('data-episode') || it.querySelector('img')) {
          it.classList.add('sona-ep-card');
          if (!it.querySelector('img')) {
            const img = it.querySelector('img, picture img');
            if (img) img.style.display = '';
          }
          const title = it.querySelector('.t') || document.createElement('div');
          title.className = 't'; title.textContent = txt.length > 40 ? txt.slice(0, 38) + '…' : txt;
          if (!it.querySelector('.t')) it.appendChild(title);
          changed = true;
        }
      });
      if (changed) el.classList.add('sona-processed');
    });
  };
  const setupMediaLoading = () => {
    const video = document.querySelector('video');
    if (!video) return;
    
    video.addEventListener('loadstart', () => {
      const banner = document.createElement('div');
      banner.className = 'sona-loading-banner';
      banner.id = 'sona-loading-banner';
      
      const logo = document.createElement('div');
      logo.className = 'sona-media-logo';
      logo.id = 'sona-media-logo';
      
      const titleEl = document.querySelector('h1, .title, [class*=""title""]');
      const title = titleEl ? titleEl.textContent.trim() : 'Loading...';
      
      logo.innerHTML = '<div style=""color: white; font-size: 24px; font-weight: bold; text-shadow: 2px 2px 4px rgba(0,0,0,0.8);"">' + title + '</div>';
      
      document.body.appendChild(banner);
      document.body.appendChild(logo);
    });
    
    video.addEventListener('canplay', () => {
      const banner = document.getElementById('sona-loading-banner');
      const logo = document.getElementById('sona-media-logo');
      if (banner) banner.remove();
      if (logo) logo.remove();
    });
  };
  const enhanceSearch = () => {
    const searchInputs = document.querySelectorAll('input[type=""search""], input[placeholder*=""search"" i]');
    searchInputs.forEach(input => {
      input.addEventListener('input', (e) => {
        const value = e.target.value.toLowerCase();
        if (value.length > 2) {
          const items = document.querySelectorAll('[class*=""item""], [class*=""card""], [class*=""movie""], [class*=""series""]');
          items.forEach(item => {
            const text = (item.textContent || '').toLowerCase();
            const isExactMatch = text.includes(value) || text === value;
            item.style.display = isExactMatch ? '' : 'none';
          });
        }
      });
    });
  };
  const enhanceSortingAndFilters = () => {
    const addSortControls = () => {
      const containers = document.querySelectorAll('[class*=""filter""], [class*=""sort""], [class*=""toolbar""]');
      containers.forEach(container => {
        if (container.classList.contains('sona-sort-enhanced')) return;
        container.classList.add('sona-sort-enhanced');
        
        const sortControl = document.createElement('div');
        sortControl.className = 'sona-sort-control';
        sortControl.innerHTML = '<select class=""sona-sort-select""><option value=""default"">Sort by Default</option><option value=""year-desc"">Year (Newest First)</option><option value=""year-asc"">Year (Oldest First)</option><option value=""rating-desc"">Rating (High to Low)</option><option value=""rating-asc"">Rating (Low to High)</option><option value=""title-asc"">Title (A-Z)</option><option value=""title-desc"">Title (Z-A)</option></select>';
        
        container.appendChild(sortControl);
        
        const select = sortControl.querySelector('.sona-sort-select');
        select.addEventListener('change', (e) => {
          applySorting(e.target.value);
        });
      });
    };
    
    const applySorting = (sortType) => {
      const items = Array.from(document.querySelectorAll('[class*=""item""], [class*=""card""], [class*=""movie""], [class*=""series""]'));
      const container = items[0]?.parentElement;
      if (!container) return;
      
      let sortedItems = [...items];
      
      switch(sortType) {
        case 'year-desc':
          sortedItems.sort((a, b) => extractYear(b) - extractYear(a));
          break;
        case 'year-asc':
          sortedItems.sort((a, b) => extractYear(a) - extractYear(b));
          break;
        case 'rating-desc':
          sortedItems.sort((a, b) => extractRating(b) - extractRating(a));
          break;
        case 'rating-asc':
          sortedItems.sort((a, b) => extractRating(a) - extractRating(b));
          break;
        case 'title-asc':
          sortedItems.sort((a, b) => extractTitle(a).localeCompare(extractTitle(b)));
          break;
        case 'title-desc':
          sortedItems.sort((a, b) => extractTitle(b).localeCompare(extractTitle(a)));
          break;
      }
      
      sortedItems.forEach(item => container.appendChild(item));
    };
    
    const extractYear = (item) => {
      const yearText = item.querySelector('.year, [class*=""year""], span, div')?.textContent || '';
      const yearMatch = yearText.match(/(20|19)\\d{2}/);
      return yearMatch ? parseInt(yearMatch[0]) : 0;
    };
    
    const extractRating = (item) => {
      const ratingText = item.querySelector('.rating, .score, [class*=""rating""], [class*=""score""]')?.textContent || '';
      const ratingMatch = ratingText.match(/([\\d.]+)/);
      return ratingMatch ? parseFloat(ratingMatch[0]) : 0;
    };
    
    const extractTitle = (item) => {
      return item.querySelector('h1, h2, h3, .title, [class*=""title""]')?.textContent || '';
    };
    
    const improveFilters = () => {
      const filterGroups = document.querySelectorAll('[class*=""filter""], [class*=""category""]');
      filterGroups.forEach(group => {
        if (group.classList.contains('sona-filter-improved')) return;
        group.classList.add('sona-filter-improved');
        
        const filterItems = group.querySelectorAll('button, .item, [class*=""option""]');
        filterItems.forEach(item => {
          item.addEventListener('click', (e) => {
            filterItems.forEach(f => f.classList.remove('active', 'selected'));
            item.classList.add('active', 'selected');
          });
        });
      });
    };
    
    addSortControls();
    improveFilters();
  };
  const enhanceMediaDetails = () => {
    const mediaItems = document.querySelectorAll('[class*=""movie""], [class*=""series""], [class*=""item""], [class*=""card""]');
    mediaItems.forEach(item => {
      if (item.classList.contains('sona-details-enhanced')) return;
      item.classList.add('sona-details-enhanced');
      
      item.addEventListener('click', (e) => {
        if (e.target.closest('button') || e.target.closest('a')) return;
        showMediaDetails(item);
      });
      
      item.addEventListener('mouseenter', () => {
        showQuickPreview(item);
      });
    });
  };
  
  const showMediaDetails = (item) => {
    const existing = document.querySelector('.sona-media-details');
    if (existing) existing.remove();
    
    const title = item.querySelector('h1, h2, h3, .title, [class*=""title""]')?.textContent || 'Unknown Title';
    const description = item.querySelector('.description, .summary, [class*=""desc""]')?.textContent || 'No description available.';
    const rating = item.querySelector('.rating, .score, [class*=""rating""]')?.textContent || '';
    const year = item.querySelector('.year, [class*=""year""]')?.textContent || '';
    
    const panel = document.createElement('div');
    panel.className = 'sona-media-details';
    panel.innerHTML = '<div class=""sona-details-backdrop""></div><div class=""sona-details-content""><div class=""sona-details-header""><h2>' + title + '</h2><button class=""sona-details-close"">×</button></div><div class=""sona-details-body""><div class=""sona-details-main""><div class=""sona-details-info"">' + (year ? '<span class=""sona-details-year"">' + year + '</span>' : '') + (rating ? '<span class=""sona-details-rating"">⭐ ' + rating + '</span>' : '') + '</div><p class=""sona-details-description"">' + description + '</p><div class=""sona-details-actions""><button class=""sona-btn-primary"">▶ Play Now</button><button class=""sona-btn-secondary"">+ Add to List</button><button class=""sona-btn-trailer"">🎬 Trailer</button></div></div><div class=""sona-details-sidebar""><div class=""sona-cast-section""><h4>Cast</h4><div class=""sona-cast-list"">Loading cast...</div></div><div class=""sona-suggestions""><h4>You might also like</h4><div class=""sona-suggestions-list"">Loading suggestions...</div></div></div></div></div>';
    
    document.body.appendChild(panel);
    
    panel.querySelector('.sona-details-close').addEventListener('click', () => panel.remove());
    panel.querySelector('.sona-details-backdrop').addEventListener('click', () => panel.remove());
    
    loadCastAndSuggestions(title, panel);
    
    panel.querySelector('.sona-btn-primary').addEventListener('click', () => {
      const playBtn = item.querySelector('button[onclick*=""play""], button[onclick*=""Play""]');
      if (playBtn) playBtn.click();
      panel.remove();
    });
    
    panel.querySelector('.sona-btn-trailer').addEventListener('click', () => {
      window.open('https://www.youtube.com/results?search_query=' + title + ' trailer', '_blank');
    });
  };
  
  const loadCastAndSuggestions = (title, panel) => {
    const mockCast = [
      { name: 'Actor One', role: 'Main Character' },
      { name: 'Actor Two', role: 'Supporting Role' },
      { name: 'Actor Three', role: 'Guest Star' }
    ];
    
    const castList = panel.querySelector('.sona-cast-list');
    castList.innerHTML = mockCast.map(actor => '<div class=""sona-cast-item"" data-actor=""' + actor.name + '""><div class=""sona-cast-avatar"">👤</div><div class=""sona-cast-info""><div class=""sona-cast-name"">' + actor.name + '</div><div class=""sona-cast-role"">' + actor.role + '</div></div></div>').join('');
    
    castList.querySelectorAll('.sona-cast-item').forEach(item => {
      item.addEventListener('click', () => {
        const actorName = item.dataset.actor;
        showActorFilmography(actorName);
      });
    });
    
    const mockSuggestions = [
      { title: 'Similar Movie 1', year: '2023', rating: '8.5' },
      { title: 'Similar Movie 2', year: '2022', rating: '7.9' },
      { title: 'Similar Movie 3', year: '2024', rating: '8.1' }
    ];
    
    const suggestionsList = panel.querySelector('.sona-suggestions-list');
    suggestionsList.innerHTML = mockSuggestions.map(item => '<div class=""sona-suggestion-item""><div class=""sona-suggestion-poster"">🎬</div><div class=""sona-suggestion-info""><div class=""sona-suggestion-title"">' + item.title + '</div><div class=""sona-suggestion-meta"">' + item.year + ' • ⭐ ' + item.rating + '</div></div></div>').join('');
  };
  
  const showActorFilmography = (actorName) => {
    const modal = document.createElement('div');
    modal.className = 'sona-actor-modal';
    modal.innerHTML = '<div class=""sona-modal-backdrop""></div><div class=""sona-modal-content""><div class=""sona-modal-header""><h3>' + actorName + ' - Filmography</h3><button class=""sona-modal-close"">×</button></div><div class=""sona-modal-body""><div class=""sona-filmography-list""><div class=""sona-film-item"">Movie 1 (2023)</div><div class=""sona-film-item"">Movie 2 (2022)</div><div class=""sona-film-item"">TV Series (2021-2023)</div></div></div></div>';
    
    document.body.appendChild(modal);
    modal.querySelector('.sona-modal-close').addEventListener('click', () => modal.remove());
    modal.querySelector('.sona-modal-backdrop').addEventListener('click', () => modal.remove());
  };
  
  const showQuickPreview = (item) => {
    const title = item.querySelector('h1, h2, h3, .title, [class*=""title""]')?.textContent || '';
    const preview = document.createElement('div');
    preview.className = 'sona-quick-preview';
    preview.innerHTML = '<div class=""sona-preview-content""><strong>' + title + '</strong><div>Click for details</div></div>';
    
    item.style.position = 'relative';
    item.appendChild(preview);
    
    setTimeout(() => {
      preview.addEventListener('mouseleave', () => preview.remove());
      item.addEventListener('mouseleave', () => preview.remove(), { once: true });
    }, 100);
  };
  const setupBannerAnimations = () => {
    const banners = document.querySelectorAll('[class*=""banner""], [class*=""hero""], [class*=""poster""], img[src*=""poster""], img[src*=""banner""]');
    banners.forEach((banner, index) => {
      if (banner.classList.contains('sona-banner-processed')) return;
      banner.classList.add('sona-banner-processed');
      
      const animationDelay = index * 0.2;
      banner.style.animation = 'bannerFloat 6s ease-in-out ' + animationDelay + 's infinite';
      banner.style.transformOrigin = 'center';
      banner.style.transition = 'transform 0.3s ease';
      
      banner.addEventListener('mouseenter', () => {
        banner.style.transform = 'scale(1.02) translateY(-2px)';
      });
      banner.addEventListener('mouseleave', () => {
        banner.style.transform = '';
      });
    });
    
    if (!document.querySelector('#sona-banner-keyframes')) {
      const style = document.createElement('style');
      style.id = 'sona-banner-keyframes';
      style.textContent = '@keyframes bannerFloat { 0%, 100% { transform: translateY(0px); } 25% { transform: translateY(-3px); } 75% { transform: translateY(2px); } } .sona-banner-processed { will-change: transform; }';
      document.head.appendChild(style);
    }
  };
  const boot = () => {
    injectStyle('sona-tweaks', '" + css.Replace("\"", "\\\"") + @"');
    
    // Force immediate execution of core features
    keepVolumeVisible();
    setupFullscreenHooks();
    ensureSearchSuggestions();
    buildTracksUI();
    styleEpisodesAsCards();
    setupMediaLoading();
    enhanceSearch();
    setupBannerAnimations();
    enhanceMediaDetails();
    enhanceSortingAndFilters();
    
    // Enhanced video player targeting
    const enhanceVideoPlayer = () => {
      const videos = document.querySelectorAll('video');
      videos.forEach(video => {
        // Force volume controls to be visible
        const volumeElements = document.querySelectorAll('[class*=""volume""], [data-volume], [aria-label*=""volume""]');
        volumeElements.forEach(el => {
          el.style.opacity = '1 !important';
          el.style.visibility = 'visible !important';
          el.style.display = 'block !important';
          el.style.transition = 'none !important';
        });
        
        // Ensure video controls are always visible
        if (video.controls !== true) {
          video.setAttribute('controls', '');
        }
        
        // Add custom controls overlay if needed
        if (!document.querySelector('.sona-video-overlay')) {
          const overlay = document.createElement('div');
          overlay.className = 'sona-video-overlay';
          overlay.innerHTML = `
            <div class=""sona-custom-controls"">
              <button class=""sona-audio-btn"" title=""Audio Tracks"">🔊</button>
              <button class=""sona-subtitle-btn"" title=""Subtitles"">📺</button>
              <button class=""sona-info-btn"" title=""Media Info"">ℹ️</button>
            </div>
          `;
          video.parentElement.appendChild(overlay);
        }
      });
    };
    
    // Enhanced track UI with better targeting
    const enhanceTrackUI = () => {
      const videos = document.querySelectorAll('video');
      videos.forEach(video => {
        // Remove existing UI to prevent duplicates
        const existing = document.querySelector('.sona-overlay-btn');
        if (existing) existing.remove();
        
        // Create enhanced track UI
        const wrap = document.createElement('div');
        wrap.className = 'sona-overlay-btn';
        wrap.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 999999; display: flex; gap: 8px;';
        
        const btnSub = document.createElement('button');
        btnSub.textContent = '📺 Subtitles';
        btnSub.style.cssText = 'background: rgba(0,0,0,0.8); color: white; border: 1px solid rgba(255,255,255,0.3); padding: 8px 12px; border-radius: 8px; cursor: pointer; font-size: 13px;';
        
        const btnAud = document.createElement('button');
        btnAud.textContent = '🔊 Audio';
        btnAud.style.cssText = 'background: rgba(0,0,0,0.8); color: white; border: 1px solid rgba(255,255,255,0.3); padding: 8px 12px; border-radius: 8px; cursor: pointer; font-size: 13px;';
        
        wrap.appendChild(btnSub);
        wrap.appendChild(btnAud);
        document.body.appendChild(wrap);
        
        // Create panel
        const panel = document.createElement('div');
        panel.className = 'sona-overlay-panel';
        panel.style.cssText = 'position: fixed; top: 70px; right: 20px; background: rgba(0,0,0,0.9); backdrop-filter: blur(10px); border: 1px solid rgba(255,255,255,0.3); border-radius: 12px; padding: 12px; display: none; max-height: 50vh; overflow: auto; min-width: 200px; z-index: 999998;';
        document.body.appendChild(panel);
        
        const showPanel = (title, rows) => {
          panel.innerHTML = ''; 
          panel.style.display = 'block';
          rows.forEach(r => {
            const row = document.createElement('div'); 
            row.style.cssText = 'color: #e5e7eb; font-size: 14px; padding: 8px 12px; border-radius: 8px; cursor: pointer; transition: all 0.2s ease; margin: 2px 0;';
            row.textContent = r.label;
            if (r.active) {
              row.style.background = 'rgba(59, 130, 246, 0.3)';
              row.style.borderLeft = '3px solid #3b82f6';
            }
            row.addEventListener('click', r.onClick);
            panel.appendChild(row);
          });
          
          const closer = (ev) => { 
            if (!panel.contains(ev.target) && !wrap.contains(ev.target)) { 
              panel.style.display = 'none'; 
              document.removeEventListener('click', closer); 
            } 
          };
          setTimeout(() => document.addEventListener('click', closer), 100);
        };
        
        // Subtitle button handler
        btnSub.addEventListener('click', () => {
          const tracks = Array.from(video.textTracks || []);
          const rows = tracks.map((t, i) => ({
            label: (t.label || t.language || 'Track ' + (i+1)) + (t.kind === 'subtitles' ? ' (Sub)' : ' (CC)'),
            onClick: () => {
              tracks.forEach(x => x.mode = 'disabled');
              t.mode = 'showing';
              showPanel('Subtitles', rows.map(r => ({...r, active: r.label === ((t.label || t.language || 'Track ' + (i+1)) + (t.kind === 'subtitles' ? ' (Sub)' : ' (CC)'))})));
            }
          }));
          rows.unshift({ 
            label: 'Off', 
            onClick: () => { 
              (video.textTracks ? Array.from(video.textTracks) : []).forEach(x => x.mode = 'disabled'); 
              showPanel('Subtitles', rows.map(r => ({...r, active: r.label === 'Off'}))); 
            }
          });
          showPanel('Subtitles', rows);
        });
        
        // Audio button handler
        btnAud.addEventListener('click', () => {
          const at = video.audioTracks;
          if (at && at.length) {
            const rows = Array.from(at).map((t, i) => ({
              label: (t.label || t.language || 'Audio ' + (i+1)),
              onClick: () => {
                Array.from(at).forEach((x, idx) => x.enabled = idx === i);
                showPanel('Audio', rows.map(r => ({...r, active: r.label === ((t.label || t.language || 'Audio ' + (i+1)))})));
              }
            }));
            showPanel('Audio', rows);
          } else {
            const rows = [{ label: 'Default audio (no alternate tracks detected)', onClick: () => {} }];
            showPanel('Audio', rows);
          }
        });
      });
    };
    
    // Force execution of enhanced features
    enhanceVideoPlayer();
    enhanceTrackUI();
    
    // Re-run functions periodically to catch new content
    const mo = new MutationObserver(() => {
      keepVolumeVisible();
      enhanceVideoPlayer();
      enhanceTrackUI();
      styleEpisodesAsCards();
      setupMediaLoading();
      setupBannerAnimations();
      enhanceMediaDetails();
      enhanceSortingAndFilters();
    });
    mo.observe(document.documentElement, { childList: true, subtree: true });
    
    // Aggressive retry mechanism for player features
    const retryFeatures = () => {
      keepVolumeVisible();
      enhanceVideoPlayer();
      enhanceTrackUI();
      styleEpisodesAsCards();
      setupMediaLoading();
      enhanceSearch();
      setupBannerAnimations();
      enhanceMediaDetails();
      enhanceSortingAndFilters();
    };
    
    setTimeout(retryFeatures, 500);
    setTimeout(retryFeatures, 1500);
    setTimeout(retryFeatures, 3000);
    setTimeout(retryFeatures, 5000);
    
    // Also run on video events
    document.addEventListener('play', retryFeatures);
    document.addEventListener('loadedmetadata', retryFeatures);
  };
  if (document.readyState === 'complete' || document.readyState === 'interactive') boot();
  else document.addEventListener('DOMContentLoaded', boot);
})();";
                await _webView.ExecuteScriptAsync(js);
            }
            catch { }
        }
    }
}
