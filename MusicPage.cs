using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SONA.Services;

namespace SONA
{
    public class MusicPage : UserControl
    {
        public MusicPage(MainWindow mainWindow)
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hero
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Integrated Apps
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cards

            // --- HERO HEADER ---
            var hero = new Border { Height = 180, Padding = new Thickness(40, 32, 40, 32) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x1d, 0xb9, 0x54), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x1a, 0x0a), 0.7));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            hero.Background = grad;
            var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            heroStack.Children.Add(new TextBlock { Text = "🎵 Music", Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeights.Heavy });
            heroStack.Children.Add(new TextBlock { Text = "Open your favorite music platform in your browser", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            hero.Child = heroStack;
            Grid.SetRow(hero, 0);
            root.Children.Add(hero);

            // --- INTEGRATED APPS ---
            var appsSection = new StackPanel { Margin = new Thickness(40, 20, 40, 0) };
            appsSection.Children.Add(new TextBlock { Text = "Integrated Apps", Foreground = Brushes.Gray, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
            
            var appsWrap = new WrapPanel();
            var spotifyApp = CreateAppLaunchCard("Spotify", "Open the desktop app", "spotify", "🟢", "#1db954");
            appsWrap.Children.Add(spotifyApp);
            
            var harmonyApp = CreateInternalAppCard("Harmony Music", "Stream music and videos seamlessly", "harmony", "🎵", "#b533ff");
            appsWrap.Children.Add(harmonyApp);
            
            appsSection.Children.Add(appsWrap);
            Grid.SetRow(appsSection, 1);
            root.Children.Add(appsSection);

            // --- SERVICE CARDS ---
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var wrap = new WrapPanel { Margin = new Thickness(40, 32, 40, 40) };

            var services = new (string Name, string Desc, string Url, string Emoji, string Color)[]
            {
                ("YouTube Music", "Stream millions of songs and playlists", "https://music.youtube.com", "🎶", "#ff0000"),
                ("Spotify", "Discover and stream music, podcasts & more", "https://open.spotify.com", "🟢", "#1db954"),
                ("Apple Music", "Listen to 100 million songs ad-free", "https://music.apple.com", "🍎", "#fc3c44"),
                ("SoundCloud", "Discover emerging artists and independent tracks", "https://soundcloud.com", "☁️", "#ff5500"),
                ("Tidal", "HiFi & Master quality audio streaming", "https://listen.tidal.com", "🌊", "#000000"),
                ("Deezer", "Music streaming with lossless audio", "https://www.deezer.com", "🎧", "#a238ff"),
                ("Amazon Music", "Stream HD music with Alexa integration", "https://music.amazon.com", "📦", "#25d1da"),
                ("Bandcamp", "Support artists directly — buy music", "https://bandcamp.com", "🎸", "#629aa9")
            };

            foreach (var svc in services)
            {
                var card = CreateServiceCard(svc.Name, svc.Desc, svc.Url, svc.Emoji, svc.Color);
                wrap.Children.Add(card);
            }

            scroll.Content = wrap;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);
            Content = root;
        }

        private Border CreateAppLaunchCard(string name, string desc, string appId, string emoji, string colorStr)
        {
            var card = CreateServiceCard(name, desc, "", emoji, colorStr);
            var textBlock = (TextBlock)((StackPanel)((StackPanel)card.Child).Children[2]).Children[0];
            textBlock.Text = "Launch Native App →";
            
            card.MouseLeftButtonDown += (_, _) => {
                var exe = AppManagerService.FindExecutable(appId);
                if (!string.IsNullOrEmpty(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                else MessageBox.Show($"{name} app not found. Please install it first.");
            };
            return card;
        }

        private Border CreateInternalAppCard(string name, string desc, string navKey, string emoji, string colorStr)
        {
            var card = CreateServiceCard(name, desc, "", emoji, colorStr);
            var textBlock = (TextBlock)((StackPanel)((StackPanel)card.Child).Children[2]).Children[0];
            textBlock.Text = "Open in SONA →";
            
            card.MouseLeftButtonDown += (_, _) => {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.Navigate(navKey);
                }
            };
            return card;
        }

        private Border CreateServiceCard(string name, string desc, string url, string emoji, string colorStr)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr)!;
            var accentBrush = new SolidColorBrush(color);

            var card = new Border
            {
                Width = 300, Height = 140,
                Margin = new Thickness(0, 0, 20, 20),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1a)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20),
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            titleRow.Children.Add(new TextBlock { Text = emoji, FontSize = 28, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            titleRow.Children.Add(new TextBlock { Text = name, Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(titleRow);

            stack.Children.Add(new TextBlock { Text = desc, Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), FontSize = 12, TextWrapping = TextWrapping.Wrap });

            var openRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            openRow.Children.Add(new TextBlock { Text = "Open in Browser →", Foreground = accentBrush, FontSize = 12, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(openRow);

            card.Child = stack;

            card.MouseEnter += (_, _) =>
            {
                SoundService.PlayHover();
                card.BorderBrush = accentBrush;
                card.Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
                var anim = new DoubleAnimation(1.03, TimeSpan.FromMilliseconds(100)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };
            card.MouseLeave += (_, _) =>
            {
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                card.Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1a));
                var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(100)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };
            card.MouseLeftButtonDown += (_, _) =>
            {
                if (!string.IsNullOrEmpty(url) && Application.Current.MainWindow is MainWindow mw)
                {
                    mw.NavigateToBrowser(url);
                }
            };

            return card;
        }
    }
}
