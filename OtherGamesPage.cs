using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using SONA.Services;

namespace SONA.Pages
{
    public class OtherGamesPage : UserControl
    {
        private readonly MainWindow _mainWindow;
        private WrapPanel _gameGrid = new();

        public OtherGamesPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainPanel = new StackPanel { Margin = new Thickness(40) };

            var title = new TextBlock
            {
                Text = "Other Games",
                Foreground = Brushes.White,
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Quickly launch individual games or emulators.",
                Foreground = Brushes.Gray,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 32)
            };
            mainPanel.Children.Add(subtitle);

            _gameGrid = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            mainPanel.Children.Add(_gameGrid);

            scroll.Content = mainPanel;
            Content = scroll;

            BuildGrid();
        }

        private void BuildGrid()
        {
            _gameGrid.Children.Clear();

            AddGameCard("osu!", "osu", "nav/games");
            AddGameCard("RetroBat", "retrobat", "nav/home");
            AddGameCard("Moonlight", "moonlight", "categories/games");
            AddGameCard("Steam", "steam", "integrations/steam");
            AddGameCard("Epic Games", "epic", "integrations/epic");
            AddGameCard("Battle.net", "battlenet", "integrations/battlenet");
            AddGameCard("Riot Games", "riot", "integrations/riot");
        }

        private void AddGameCard(string name, string appId, string iconPath)
        {
            var card = new Border
            {
                Width = 200,
                Height = 220,
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(0, 0, 24, 24),
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1c)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            try 
            {
                 var img = IconHelper.Img(iconPath, 80);
                 img.Margin = new Thickness(0, 0, 0, 20);
                 img.HorizontalAlignment = HorizontalAlignment.Center;
                 stack.Children.Add(img);
            } 
            catch { }

            var titleText = new TextBlock
            {
                Text = name,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(titleText);

            card.Child = stack;

            card.MouseLeftButtonDown += (s, e) => _mainWindow.NavigateToEmbeddedApp(appId);
            
            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x2a));
                card.BorderBrush = (Brush)Application.Current.FindResource("AccentBrush");
                var anim = new System.Windows.Media.Animation.DoubleAnimation(1.05, TimeSpan.FromMilliseconds(150));
                card.RenderTransform = new ScaleTransform(1, 1);
                card.RenderTransformOrigin = new Point(0.5, 0.5);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                SoundService.PlayHover();
            };
            
            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1c));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                var anim = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150));
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            _gameGrid.Children.Add(card);
        }
    }
}
