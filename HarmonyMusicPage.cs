using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using SONA.Services;

namespace SONA.Pages
{
    public class HarmonyMusicPage : UserControl, IDisposable
    {
        private WebView2? _webView;
        private readonly Grid _loadingView;
        private readonly Grid _contentView;
        private readonly MainWindow _mw;
        private bool _isInitialized = false;

        public HarmonyMusicPage(MainWindow mw)
        {
            _mw = mw;
            Background = Brushes.Transparent;

            var rootGrid = new Grid();

            _loadingView = new Grid { Background = Brushes.Transparent };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock 
            { 
                Text = "Loading Harmony Music...", 
                Foreground = Brushes.White,
                FontSize = 24, 
                HorizontalAlignment = HorizontalAlignment.Center 
            });
            stack.Children.Add(new TextBlock 
            { 
                Text = "Starting local server...", 
                Foreground = Brushes.Gray,
                FontSize = 14, 
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center 
            });
            _loadingView.Children.Add(stack);

            _contentView = new Grid { Visibility = Visibility.Collapsed };

            rootGrid.Children.Add(_loadingView);
            rootGrid.Children.Add(_contentView);

            Content = rootGrid;

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                _webView?.Focus();
                return;
            }

            // Ensure HarmonyService is running
            if (!HarmonyService.IsRunning)
            {
                await HarmonyService.StartAsync();
            }

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                Margin = new Thickness(0)
            };
            
            _contentView.Children.Add(_webView);

            await WebViewService.InitializeWebViewAsync(_webView);
            
            _webView.CoreWebView2.ProcessFailed += (s, args) =>
            {
                _webView.Reload();
            };

            _webView.Source = new Uri("http://localhost:3000");
            _webView.NavigationCompleted += OnNavigationCompleted;
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                _loadingView.Visibility = Visibility.Collapsed;
                _contentView.Visibility = Visibility.Visible;
                _webView?.Focus();
                _isInitialized = true;
            }
        }

        public void Dispose()
        {
            try { _webView?.Dispose(); } catch { }
        }
    }
}
