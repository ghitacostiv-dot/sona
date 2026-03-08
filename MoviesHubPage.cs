using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SONA.Controls;
using SONA.Services;

namespace SONA.Pages
{
    public class MoviesHubPage : UserControl
    {
        private readonly MainWindow _mainWindow;

        public MoviesHubPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Background = Brushes.Transparent;
            InitializeUI();
        }

        private void InitializeUI()
        {
            var grid = new Grid();
            
            var stack = new StackPanel 
            { 
                VerticalAlignment = VerticalAlignment.Center, 
                HorizontalAlignment = HorizontalAlignment.Center 
            };

            var title = new TextBlock
            {
                Text = "Choose Your Experience",
                Foreground = Brushes.White,
                FontSize = 32,
                FontWeight = FontWeights.ExtraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 40)
            };
            title.Effect = new DropShadowEffect { BlurRadius = 15, Opacity = 0.5, ShadowDepth = 0 };
            stack.Children.Add(title);

            var cardsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            cardsPanel.Children.Add(CreateAppCard(
                "Nexus", 
                "All-in-one streaming with built-in addons. Fast and light.", 
                "nav/movies", 
                "#e50914",
                () => _mainWindow.Navigate("nexus")
            ));

            cardsPanel.Children.Add(new Border { Width = 40 }); // Spacer

            cardsPanel.Children.Add(CreateAppCard(
                "Stremio", 
                "The classic movie launcher. Requires local installation.", 
                "nav/movies", 
                "#3b82f6",
                () => _mainWindow.Navigate("stremio")
            ));

            stack.Children.Add(cardsPanel);

            // Back to web sources
            var backBtn = new Button
            {
                Content = "View Web Sources",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Margin = new Thickness(0, 50, 0, 0),
                Padding = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            backBtn.Click += (s, e) => _mainWindow.PageHost.Content = new SourcesPage("movies", _mainWindow);
            stack.Children.Add(backBtn);

            grid.Children.Add(stack);
            Content = grid;
        }

        private Border CreateAppCard(string name, string desc, string icon, string accent, Action onClick)
        {
            var accentColor = (Color)ColorConverter.ConvertFromString(accent);
            var card = new Border
            {
                Width = 300,
                Height = 350,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(30),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var iconImg = IconHelper.Img(icon, 64);
            iconImg.Margin = new Thickness(0, 0, 0, 20);
            content.Children.Add(iconImg);

            content.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = Brushes.White,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            content.Children.Add(new TextBlock
            {
                Text = desc,
                Foreground = Brushes.LightGray,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var launchBtn = new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 30, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            launchBtn.Child = new TextBlock { Text = "LAUNCH", Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            content.Children.Add(launchBtn);

            card.Child = content;

            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(accentColor);
                card.Effect = new DropShadowEffect { Color = accentColor, BlurRadius = 20, Opacity = 0.3, ShadowDepth = 0 };
            };
            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                card.Effect = null;
            };
            card.MouseLeftButtonDown += (s, e) => onClick();

            return card;
        }
    }
}
