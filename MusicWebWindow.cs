using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using SONA.Services;

namespace SONA
{
    public class MusicWebWindow : Window
    {
        private WebView2 _webView;

        public MusicWebWindow(string url, string title)
        {
            Title = title;
            Width = 1200;
            Height = 800;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 18)),
                CornerRadius = new CornerRadius(15),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Title Bar
            var titleBar = new Border { Background = new SolidColorBrush(Color.FromRgb(25, 25, 25)) };
            titleBar.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
            
            var titleText = new TextBlock 
            { 
                Text = title, 
                Foreground = Brushes.White, 
                VerticalAlignment = VerticalAlignment.Center, 
                Margin = new Thickness(20, 0, 0, 0),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            
            var closeBtn = new Button 
            { 
                Content = "✕", 
                Width = 45, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent, 
                Foreground = Brushes.Gray,
                BorderThickness = new Thickness(0),
                FontSize = 18,
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => Close();
            closeBtn.MouseEnter += (s, e) => closeBtn.Foreground = Brushes.White;
            closeBtn.MouseLeave += (s, e) => closeBtn.Foreground = Brushes.Gray;
            
            var titleGrid = new Grid();
            titleGrid.Children.Add(titleText);
            titleGrid.Children.Add(closeBtn);
            titleBar.Child = titleGrid;
            
            Grid.SetRow(titleBar, 0);
            grid.Children.Add(titleBar);

            // WebView
            _webView = new WebView2 { 
                DefaultBackgroundColor = System.Drawing.Color.Transparent,
                Margin = new Thickness(0)
            };
            _webView.NavigationCompleted += OnNavigationCompleted;
            Grid.SetRow(_webView, 1);
            grid.Children.Add(_webView);

            border.Child = grid;
            Content = border;

            InitializeAsync(url);
        }

        private async void InitializeAsync(string url)
        {
            try {
                await WebViewService.InitializeWebViewAsync(_webView);
                _webView.Source = new Uri(url);
            } catch { }
        }

        private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess) return;

            // Overdrive styles as requested
            string css = @"
                ::-webkit-scrollbar { width: 0px; background: transparent; }
                #header, #player-bar-background { background: rgba(10,10,10,0.8) !important; backdrop-filter: blur(10px); }
                ytmusic-player-bar { border-top: 1px solid rgba(255,255,255,0.1) !important; }
                .ytmusic-nav-bar { background: transparent !important; }
                /* Hide 'Open in App' prompts */
                #clarification-renderer, .upsell-dialog-renderer { display: none !important; }
            ";

            await WebViewService.InjectCssAsync(_webView, css);
        }
    }
}
