using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Input;
using SONA.Controls;
using SONA.Services;
using System.Windows.Controls.Primitives;

namespace SONA
{
    public class StremioPage : UserControl, IDisposable
    {
        private readonly MainWindow _mainWindow;
        private Grid _mainGrid = null!;
        private Grid _setupView = null!;
        private Grid _appHostContainer = null!;
        private NativeWindowHost? _nativeHost;
        private TextBlock _pathText = null!;
        private Border _frameBorder = null!;
        private const double FramePadding = 8;
        private const double ExtraDown = 16;
        private const double OverlayTopHeight = 0;
        private double _reservedTop = 0;

        public void Dispose()
        {
            _nativeHost?.Dispose();
            _nativeHost = null;
        }

        public StremioPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            Background = Brushes.Transparent;
            InitializeUI();
            Loaded += (s, e) => CheckAndLaunch();
            Unloaded += (s, e) => Dispose();
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(OverlayTopHeight) });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- Setup View ---
            _setupView = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x0a, 0x0a, 0x14)) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            
            var icon = IconHelper.Img("nav/movies", 48);
            icon.Margin = new Thickness(0, 0, 0, 16);
            stack.Children.Add(icon);

            stack.Children.Add(new TextBlock { 
                Text = "Movies Launcher Setup", 
                Foreground = Brushes.White, 
                FontSize = 24, 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0,0,0,10),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock { 
                Text = "Select your Stremio Launcher (or any Movie Launcher) executable to integrate it into SONA.", 
                Foreground = Brushes.LightGray, 
                Margin = new Thickness(0,0,0,20),
                TextAlignment = TextAlignment.Center
            });

            _pathText = new TextBlock { 
                Text = AppConfig.GetString("stremio_exe_path", "No executable selected"), 
                Foreground = Brushes.Cyan, 
                Margin = new Thickness(0,0,0,20),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontStyle = FontStyles.Italic
            };
            stack.Children.Add(_pathText);

            var selectBtn = new Button { 
                Content = "Select Launcher Executable (.exe)", 
                Width = 250, Height = 45, 
                Background = new SolidColorBrush(Color.FromRgb(0x4a, 0x14, 0x8c)),
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
            _appHostContainer = new Grid { Background = Brushes.Transparent, Visibility = Visibility.Collapsed };

            _frameBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 120, 120, 140)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _appHostContainer.Children.Add(_frameBorder);
            Grid.SetRow(_appHostContainer, 1);
            _mainGrid.Children.Add(_appHostContainer);

            // Transparent overlay "taskbar" that lets background show through
            var transparentTop = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 0)
            };
            Grid.SetRow(transparentTop, 0);
            _mainGrid.Children.Add(transparentTop);

            Content = _mainGrid;
        }

        private void SelectExe()
        {
            var dialog = new OpenFileDialog { Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*" };
            if (dialog.ShowDialog() == true)
            {
                AppConfig.Set("stremio_exe_path", dialog.FileName);
                _pathText.Text = dialog.FileName;
                CheckAndLaunch();
            }
        }

        private async void CheckAndLaunch()
        {
            try
            {
                string path = AppConfig.GetString("stremio_exe_path", "");
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                {
                    // Try to auto-detect
                    var info = await SONA.Services.NativeAppIntegrationService.GetAppInfoAsync("stremio");
                    if (info.IsInstalled && !string.IsNullOrEmpty(info.ExecutablePath))
                    {
                        path = info.ExecutablePath;
                        AppConfig.Set("stremio_exe_path", path);
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
                    _frameBorder.Visibility = Visibility.Collapsed;
                    _appHostContainer.Children.Add(_frameBorder);
                    _appHostContainer.Children.Add(_nativeHost);
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

        private bool _moveMode = false;
        private Point _dragStart;
        private double _startOffsetX;
        private double _startOffsetY;
        
        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (_moveMode && _nativeHost != null)
            {
                _dragStart = e.GetPosition(_appHostContainer);
                _startOffsetX = _nativeHost.FloatingOffsetX;
                _startOffsetY = _nativeHost.FloatingOffsetY;
                Mouse.Capture(_appHostContainer);
                _frameBorder.Visibility = Visibility.Visible;
            }
        }

        protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseUp(e);
            if (Mouse.Captured == _appHostContainer)
            {
                Mouse.Capture(null);
                if (!_moveMode) _frameBorder.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (_moveMode && Mouse.Captured == _appHostContainer && _nativeHost != null)
            {
                var p = e.GetPosition(_appHostContainer);
                var dx = p.X - _dragStart.X;
                var dy = p.Y - _dragStart.Y;
                _nativeHost.FloatingOffsetX = _startOffsetX + dx;
                _nativeHost.FloatingOffsetY = Math.Max(0, _startOffsetY + dy);
                _nativeHost.ForceResize();
                UpdateFrameBorder();
            }
        }

        private void UpdateHostLayout()
        {
            if (_nativeHost == null) return;
            var w = Math.Max(1, _appHostContainer.ActualWidth);
            var h = Math.Max(1, _appHostContainer.ActualHeight);
            _nativeHost.FloatingWidth = 0;  // Let host choose 80% like Hydra style
            _nativeHost.FloatingHeight = 0; // Let host choose 80% like Hydra style
            _nativeHost.ForceResize();
            UpdateFrameBorder();
        }

        private void UpdateFrameBorder()
        {
            if (_nativeHost == null) return;
            _frameBorder.Visibility = Visibility.Collapsed;
        }
    }
}
