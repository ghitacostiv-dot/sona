using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SONA.Services;

namespace SONA.Pages
{
    public class IntegrationsPage : UserControl
    {
        private WrapPanel _grid = new();

        public IntegrationsPage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainPanel = new StackPanel { Margin = new Thickness(40) };

            var title = new TextBlock
            {
                Text = "Integrations",
                Foreground = Brushes.White,
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            mainPanel.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Quickly launch built-in integrations or open their web counterparts.",
                Foreground = Brushes.Gray,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 32)
            };
            mainPanel.Children.Add(subtitle);

            _grid = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
            mainPanel.Children.Add(_grid);

            scroll.Content = mainPanel;
            Content = scroll;

            BuildGrid();
        }

        private void BuildGrid()
        {
            _grid.Children.Clear();

            AddIntegrationCard("Steam", "steam", "https://store.steampowered.com/", "integrations/steam");
            AddIntegrationCard("Epic Games", "epic", "https://store.epicgames.com/", "integrations/epic");
            AddIntegrationCard("Battle.net", "battlenet", "https://eu.shop.battle.net/", "integrations/battlenet");
            AddIntegrationCard("Spotify", "spotify", "https://open.spotify.com/", "integrations/spotify");
            AddIntegrationCard("Discord", "discord", "https://discord.com/app", "integrations/discord");
            AddIntegrationCard("WhatsApp", "whatsapp", "https://web.whatsapp.com/", "integrations/whatsapp");
            AddIntegrationCard("Riot Games", "riot", "https://auth.riotgames.com/", "integrations/riot");
        }

        private async void AddIntegrationCard(string name, string appId, string webUrl, string iconPath)
        {
            var card = new Border
            {
                Width = 260,
                Height = 140,
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(0, 0, 24, 24),
                Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1c)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var dp = new DockPanel();

            // Status label - Placeholder while loading
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) };
            var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Brushes.Gray, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            var status = new TextBlock
            {
                Text = "Checking...",
                Foreground = Brushes.Gray,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(dot);
            statusRow.Children.Add(status);
            DockPanel.SetDock(statusRow, Dock.Top);
            dp.Children.Add(statusRow);

            // Async Status Update
            _ = System.Threading.Tasks.Task.Run(() => AppManagerService.IsInstalled(appId)).ContinueWith(t => {
                Application.Current.Dispatcher.Invoke(() => {
                    bool installed = t.Result;
                    dot.Background = installed ? Brushes.LimeGreen : Brushes.Orange;
                    status.Text = installed ? "Installed" : "Action Required";
                    status.Foreground = installed ? Brushes.LimeGreen : Brushes.Orange;
                });
            });

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 0) };
            try 
            {
                 var img = IconHelper.Img(iconPath, 32);
                 img.Margin = new Thickness(0, 0, 12, 0);
                 titleRow.Children.Add(img);
            } 
            catch { }

            var title = new TextBlock
            {
                Text = name,
                Foreground = Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(title);
            dp.Children.Add(titleRow);

            // Web Launch Button
            var webBtn = new Button
            {
                Content = "Launch Web Version ↗",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Height = 32,
                FontSize = 11,
                Foreground = Brushes.LightGray,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 12, 12)
            };
            webBtn.Click += (_, e) => 
            {
                e.Handled = true; // Prevent bubble
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    mw.NavigateToBrowserWithUrl(webUrl);
                }
            };
            
            // Layout hack for absolute positioning essentially
            var grid = new Grid();
            grid.Children.Add(dp);
            grid.Children.Add(webBtn);
            
            card.Child = grid;

            card.MouseLeftButtonDown += (_, _) => LaunchApp(appId, name);
            card.MouseEnter += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(0x23, 0x23, 0x2a));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
                SoundService.PlayHover();
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1c));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            };

            _grid.Children.Add(card);
        }

        private void LaunchApp(string appId, string name)
        {
            string? exe = AppManagerService.FindExecutable(appId);
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                SoundService.PlaySelect();
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exe)
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error launching {name}: {ex.Message}");
                }
            }
            else
            {
                SoundService.PlaySelect(); // Changed to a valid sound if PlayError is missing
                MessageBox.Show($"{name} executable not found. Will be auto-installed on start or bind paths manually in Settings.", "App Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
