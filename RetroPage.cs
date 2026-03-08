using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SONA.Services;
using SONA.Controls;

namespace SONA
{
    public class RetroPage : UserControl
    {
        private TextBlock _pathLabel = new();
        private Button _launchBtn = new();
        private Grid _mainContent = new();
        private Grid _hostContainer = new();
        private NativeWindowHost? _host;

        public RetroPage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hero
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            
            _mainContent = new Grid();
            Grid.SetRowSpan(_mainContent, 2);
            root.Children.Add(_mainContent);

            _hostContainer = new Grid { Visibility = Visibility.Collapsed, Background = Brushes.Black };
            Grid.SetRowSpan(_hostContainer, 2);
            root.Children.Add(_hostContainer);

            // --- HERO ---
            var hero = new Border { Height = 200, Padding = new Thickness(40, 32, 40, 32) };
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x8b, 0x5c, 0xf6), 0.0)); // Purple
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x3b, 0x0a, 0x45), 0.6));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0x0a, 0x0a, 0x0a), 1.0));
            hero.Background = grad;
            var heroStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            var heroTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var heroIcon = IconHelper.Img("nav/home", 48);
            heroIcon.Margin = new Thickness(0, 0, 16, 0);
            heroTitleStack.Children.Add(heroIcon);
            heroTitleStack.Children.Add(new TextBlock { Text = "Retro Gaming", Foreground = Brushes.White, FontSize = 42, FontWeight = FontWeights.Heavy, VerticalAlignment = VerticalAlignment.Center });
            heroStack.Children.Add(heroTitleStack);
            
            heroStack.Children.Add(new TextBlock { Text = "Launch RetroBat and relive classic gaming", Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            hero.Child = heroStack;
            Grid.SetRow(hero, 0);
            _mainContent.Children.Add(hero);

            // --- CONTENT ---
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var content = new StackPanel { Margin = new Thickness(40, 32, 40, 40) };

            // RetroBat Card
            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x1a)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0x8b, 0x5c, 0xf6)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(32),
                Margin = new Thickness(0, 0, 0, 24)
            };

            var cardStack = new StackPanel();

            // Title
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            titleRow.Children.Add(IconHelper.Img("nav/home", 36));
            var titleInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            titleInfo.Children.Add(new TextBlock { Text = "RetroBat", Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.Bold });
            titleInfo.Children.Add(new TextBlock { Text = "All-in-one retro gaming station for Windows", Foreground = Brushes.Gray, FontSize = 13 });
            titleRow.Children.Add(titleInfo);
            cardStack.Children.Add(titleRow);

            // Separator
            cardStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 16) });

            // Path Display
            var savedPath = AppConfig.GetString("retrobat_path", "");
            _pathLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(savedPath) ? "No executable configured" : savedPath,
                Foreground = string.IsNullOrEmpty(savedPath) ? Brushes.Gray : new SolidColorBrush(Color.FromRgb(0x8b, 0x5c, 0xf6)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            cardStack.Children.Add(_pathLabel);

            // Buttons
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

            var browseBtn = new Button
            {
                Content = "📂  Browse for EXE",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Height = 44,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 14,
                Margin = new Thickness(0, 0, 12, 0)
            };
            browseBtn.Click += BrowseForExe;
            btnRow.Children.Add(browseBtn);

            _launchBtn = new Button
            {
                Content = "🚀  Launch RetroBat",
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Background = new SolidColorBrush(Color.FromRgb(0x8b, 0x5c, 0xf6)),
                Height = 44,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 14,
                IsEnabled = !string.IsNullOrEmpty(savedPath) && File.Exists(savedPath)
            };
            _launchBtn.Click += LaunchRetrobat;
            btnRow.Children.Add(_launchBtn);

            cardStack.Children.Add(btnRow);
            card.Child = cardStack;
            content.Children.Add(card);

            // Info Section
            var infoCard = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(40, 0x8b, 0x5c, 0xf6)),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock { Text = "💡 What is RetroBat?", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            infoStack.Children.Add(new TextBlock
            {
                Text = "RetroBat is a free, all-in-one emulation frontend for Windows. It automatically detects and configures emulators for dozens of retro gaming platforms including NES, SNES, N64, PlayStation, Sega Genesis, Game Boy, and many more. Just point SONA to your RetroBat executable and launch it directly from here.",
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });
            infoCard.Child = infoStack;
            content.Children.Add(infoCard);

            // Download link
            var dlBtn = new Button
            {
                Content = "🌐  Download RetroBat (retrobat.org)",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Height = 40,
                Padding = new Thickness(20, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            dlBtn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo("https://www.retrobat.org/") { UseShellExecute = true }); } catch { }
            };
            content.Children.Add(dlBtn);

            scroll.Content = content;
            Grid.SetRow(scroll, 1);
            _mainContent.Children.Add(scroll);
            Content = root;
        }

        private void BrowseForExe(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Select RetroBat Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                AppConfig.Set("retrobat_path", ofd.FileName);
                _pathLabel.Text = ofd.FileName;
                _pathLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x8b, 0x5c, 0xf6));
                _launchBtn.IsEnabled = true;
            }
        }

        private async void LaunchRetrobat(object sender, RoutedEventArgs e)
        {
            var path = AppConfig.GetString("retrobat_path", "");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("RetroBat executable not found. Please browse for it again.", "SONA", MessageBoxButton.OK, MessageBoxImage.Warning);
                _launchBtn.IsEnabled = false;
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
                        ProcessNameToKill = Path.GetFileNameWithoutExtension(path),
                        IsFloating = true,
                        KeepNativeControls = true,
                        IsSticky = false
                    };
                    _hostContainer.Children.Add(_host);
                }

                await _host.LoadAppAsync(path, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "SONA", MessageBoxButton.OK, MessageBoxImage.Error);
                _mainContent.Visibility = Visibility.Visible;
                _hostContainer.Visibility = Visibility.Collapsed;
            }
        }
    }
}
