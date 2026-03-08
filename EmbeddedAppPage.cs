using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;
using SONA.Controls;
using SONA.Services;

namespace SONA.Pages
{
    public class EmbeddedAppPage : UserControl, IDisposable
    {
        private readonly NativeWindowHost _host;
        private readonly Grid _setupView;
        private readonly Border _hostContainer;
        private readonly MainWindow _mw;
        private string _appId;

        public EmbeddedAppPage(string appId, MainWindow mw)
        {
            _appId = appId;
            _mw = mw;
            Background = Brushes.Transparent;
            Unloaded += (s, e) => Dispose();

            var root = new Grid();
            
            // SETUP VIEW
            _setupView = new Grid { Visibility = Visibility.Collapsed };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var appInfo = AppManagerService.GetApp(appId);
            string appName = appInfo?.Name ?? appId;

            var icon = IconHelper.Img("nav/programs", 72);
            icon.Margin = new Thickness(0,0,0,20);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            
            var title = new TextBlock { Text = $"{appName} Launcher", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            var desc = new TextBlock { Text = $"Select the {appName} executable to embed it in SONA.", Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0,10,0,30), HorizontalAlignment = HorizontalAlignment.Center };
            
            var btnSelect = new Button { Content = "Select Executable", Style = (Style)Application.Current.FindResource("AccentBtn"), Width = 220, Height = 45 };
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

            // Navigation bar for embedded mode
            var navBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 15, 15, 20)),
                Height = 40,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Visible
            };
            
            var navStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10,0,10,0) };
            
            var changeBtn = new Button
            {
                Content = "⚙ Re-map",
                Style = (Style)Application.Current.FindResource("DarkBtn"),
                Height = 28,
                Margin = new Thickness(5,0,5,0),
                FontSize = 11
            };
            changeBtn.Click += (s, e) => {
                _host?.Dispose();
                AppConfig.Set($"{_appId}_exe_path", "");
                CheckAndLaunch();
            };
            
            navStack.Children.Add(changeBtn);
            navBar.Child = navStack;
            hostGrid.Children.Add(navBar);
            Panel.SetZIndex(navBar, 1000);

            _hostContainer.Child = hostGrid;

            root.Children.Add(_setupView);
            root.Children.Add(_hostContainer);
            Content = root;

            CheckAndLaunch();
        }

        private void SelectExe(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Executable Files (*.exe)|*.exe", Title = $"Select {_appId} Executable" };
            if (dlg.ShowDialog() == true)
            {
                AppConfig.Set($"{_appId}_exe_path", dlg.FileName);
                CheckAndLaunch();
            }
        }

        private async void CheckAndLaunch()
        {
            var path = AppConfig.GetString($"{_appId}_exe_path");
            if (string.IsNullOrEmpty(path))
            {
                path = AppManagerService.FindExecutable(_appId);
            }

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
                    _host.ProcessNameToKill = Path.GetFileNameWithoutExtension(path);
                    _host.IsFloating = true; 
                    _host.KeepNativeControls = true; 
                    _host.IsSticky = true;
                    
                    await _host.LoadAppAsync(path, "");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to launch application: {ex.Message}", "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _setupView.Visibility = Visibility.Visible;
                    _hostContainer.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void Dispose()
        {
            _host.Dispose();
        }
    }
}
