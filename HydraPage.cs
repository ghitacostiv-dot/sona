using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Input;
using SONA.Controls;
using SONA.Services;

namespace SONA
{
    public class HydraPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private Grid _mainGrid;
        private Grid _setupView;
        private Grid _appHostContainer;
        private NativeWindowHost? _nativeHost;
        private TextBlock _pathText;

        public void Dispose()
        {
            _nativeHost?.Dispose();
            _nativeHost = null;
        }

        public HydraPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Background = Brushes.Transparent;
            InitializeUI();
            Loaded += (s, e) => CheckAndLaunch();
            Unloaded += (s, e) => Dispose();
        }

        private const double OverlayTopHeight = 0;

        private void InitializeUI()
        {
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(OverlayTopHeight) });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- Setup View ---
            _setupView = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x14)) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var icon = IconHelper.Img("nav/games", 48);
            icon.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(icon);

            stack.Children.Add(new TextBlock { 
                Text = "Games Launcher Setup", 
                Foreground = Brushes.White, 
                FontSize = 24, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0,0,0,10),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock { 
                Text = "Select your Hydra Launcher (or any Game Launcher) executable to integrate it into SONA.", 
                Foreground = Brushes.LightGray, 
                Margin = new Thickness(0,0,0,20),
                TextAlignment = TextAlignment.Center
            });

            _pathText = new TextBlock { 
                Text = AppConfig.GetString("hydra_exe_path", "No executable selected"), 
                Foreground = Brushes.Cyan, 
                Margin = new Thickness(0,0,0,20),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontStyle = FontStyles.Italic
            };
            stack.Children.Add(_pathText);

            var selectBtn = new Button { 
                Content = "Select Launcher Executable (.exe)", 
                Width = 250, Height = 45, 
                Background = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Medium
            };
            selectBtn.Click += (s, e) => SelectExe();
            stack.Children.Add(selectBtn);

            _setupView.Children.Add(stack);
            Grid.SetRow(_setupView, 1);
            _mainGrid.Children.Add(_setupView);

            // --- App Host Container ---
            _appHostContainer = new Grid { Background = Brushes.Black, Visibility = Visibility.Collapsed };
            
            // Add a "Change Launcher" floating button
            var changeBtn = new Button
            {
                Content = "⚙ Change Launcher",
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
                _nativeHost?.Dispose();
                _nativeHost = null;
                AppConfig.Set("hydra_exe_path", "");
                CheckAndLaunch();
            };

            _appHostContainer.Children.Add(changeBtn);
            Panel.SetZIndex(changeBtn, 999);

            Grid.SetRow(_appHostContainer, 1);
            _mainGrid.Children.Add(_appHostContainer);

            // Transparent overlay "taskbar" area at the top (lets BG video show)
            var transparentTop = new Border { Background = Brushes.Transparent };
            Grid.SetRow(transparentTop, 0);
            _mainGrid.Children.Add(transparentTop);

            Content = _mainGrid;
        }

        private void SelectExe()
        {
            var dialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                AppConfig.Set("hydra_exe_path", dialog.FileName);
                _pathText.Text = dialog.FileName;
                CheckAndLaunch();
            }
        }

        private async void CheckAndLaunch()
        {
            try
            {
                string path = AppConfig.GetString("hydra_exe_path", "");
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                {
                    // Try to auto-detect
                    var info = await SONA.Services.NativeAppIntegrationService.GetAppInfoAsync("hydra");
                    if (info.IsInstalled && !string.IsNullOrEmpty(info.ExecutablePath))
                    {
                        path = info.ExecutablePath;
                        AppConfig.Set("hydra_exe_path", path);
                        _pathText.Text = path;
                    }
                    else
                    {
                        _setupView.Visibility = Visibility.Visible;
                        _appHostContainer.Visibility = Visibility.Collapsed;
                        return;
                    }
                }

                _setupView.Visibility = Visibility.Collapsed;
                _appHostContainer.Visibility = Visibility.Visible;

                if (_nativeHost == null)
                {
                    _nativeHost = new NativeWindowHost();
                    _nativeHost.ProcessNameToKill = System.IO.Path.GetFileNameWithoutExtension(path);
                    _nativeHost.IsFloating = true; 
                    _nativeHost.KeepNativeControls = false; 
                    _nativeHost.IsSticky = true;
                    
                    _nativeHost.FloatingOffsetY = 0;
                    _nativeHost.FloatingOffsetX = 0;
                    
                    _appHostContainer.Children.Clear();
                    _appHostContainer.Children.Add(_nativeHost);

                    // No placeholder needed as the app is hosted directly
                }
                else
                {
                    _nativeHost.ProcessNameToKill = System.IO.Path.GetFileNameWithoutExtension(path);
                    _nativeHost.IsSticky = true;
                    _nativeHost.IsFloating = true;
                    _nativeHost.KeepNativeControls = false;
                    _nativeHost.FloatingOffsetY = 0;
                    _nativeHost.FloatingOffsetX = 0;
                }

                await _nativeHost.LoadAppAsync(path, killExisting: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch app: {ex.Message}", "Integration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _setupView.Visibility = Visibility.Visible;
                _appHostContainer.Visibility = Visibility.Collapsed;
            }
        }
    }
}
