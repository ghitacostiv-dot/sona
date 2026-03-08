using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;
using SONA.Controls;

namespace SONA.Pages
{
    public class BrowserHostPage : UserControl, IDisposable
    {
        private readonly NativeWindowHost _host;
        private readonly Grid _setupView;
        private readonly Border _hostContainer;
        private readonly MainWindow _mw;
        private string? _initialUrl;

        public BrowserHostPage(MainWindow mw, string? url = null)
        {
            _mw = mw;
            _initialUrl = url;
            Background = Brushes.Transparent;

            var root = new Grid();
            
            // SETUP VIEW
            _setupView = new Grid { Visibility = Visibility.Collapsed };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var icon = new TextBlock { Text = "🌐", FontSize = 72, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,20) };
            var title = new TextBlock { Text = "Browser Launcher", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            var desc = new TextBlock { Text = "Select your preferred browser executable to embed it in SONA.", Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0,10,0,30), HorizontalAlignment = HorizontalAlignment.Center };
            
            var btnSelect = new Button { Content = "Select Browser EXE", Style = (Style)Application.Current.FindResource("AccentBtn"), Width = 220, Height = 45 };
            btnSelect.Click += SelectExe;

            stack.Children.Add(icon);
            stack.Children.Add(title);
            stack.Children.Add(desc);
            stack.Children.Add(btnSelect);
            _setupView.Children.Add(stack);

            // HOST VIEW
            _hostContainer = new Border { Visibility = Visibility.Collapsed };
            var hostGrid = new Grid();
            _host = new NativeWindowHost { ProcessNameToKill = "" }; 
            hostGrid.Children.Add(_host);

            // Add a "Change Browser" floating button
            var changeBtn = new Button
            {
                Content = "⚙ Change Browser",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 10, 0),
                Padding = new Thickness(10, 5, 10, 5),
                Background = new SolidColorBrush(Color.FromArgb(150, 40, 40, 40)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                Cursor = Cursors.Hand,
                Opacity = 0.6
            };
            changeBtn.MouseEnter += (s, e) => changeBtn.Opacity = 1.0;
            changeBtn.MouseLeave += (s, e) => changeBtn.Opacity = 0.6;
            changeBtn.Click += (s, e) => {
                _host?.Dispose();
                AppConfig.Set("browser_exe_path", "");
                CheckAndLaunch();
            };
            hostGrid.Children.Add(changeBtn);
            Panel.SetZIndex(changeBtn, 999);

            _hostContainer.Child = hostGrid;

            root.Children.Add(_setupView);
            root.Children.Add(_hostContainer);
            Content = root;

            CheckAndLaunch();
        }

        private void SelectExe(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Executable Files (*.exe)|*.exe", Title = "Select Browser Executable" };
            if (dlg.ShowDialog() == true)
            {
                AppConfig.Set("browser_exe_path", dlg.FileName);
                CheckAndLaunch();
            }
        }

        private async void CheckAndLaunch()
        {
            var path = AppConfig.GetString("browser_exe_path");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _setupView.Visibility = Visibility.Visible;
                _hostContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                _setupView.Visibility = Visibility.Collapsed;
                _hostContainer.Visibility = Visibility.Visible;
                
                try
                {
                    // Pass initial URL if available
                    // Pass initial URL if available and force GPU
                    string gpuFlags = "--enable-gpu --ignore-gpu-blocklist --enable-gpu-rasterization";
                    string args = !string.IsNullOrEmpty(_initialUrl) ? $"\"{_initialUrl}\" {gpuFlags}" : gpuFlags;
                    
                    // Set ProcessNameToKill based on file name (e.g., chrome, msedge, firefox)
                    _host.ProcessNameToKill = Path.GetFileNameWithoutExtension(path);
                    _host.IsFloating = true; 
                    _host.KeepNativeControls = true; 
                    _host.IsSticky = false;

                    // No placeholder needed as the app is hosted directly
                    
                    await _host.LoadAppAsync(path, args);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch browser: {ex.Message}", "Browser Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _setupView.Visibility = Visibility.Visible;
                    _hostContainer.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void Navigate(string url)
        {
            _initialUrl = url;
            CheckAndLaunch(); // Re-launch or pass args if possible? Most browsers handle single instance well
        }

        public void Dispose()
        {
            _host.Dispose();
        }
    }
}
