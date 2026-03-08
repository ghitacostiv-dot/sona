using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SONA.Services;
using System.Diagnostics;
using System.Windows.Input;

namespace SONA.Pages
{
    public class LibraryPage : UserControl
    {
        private WrapPanel _appsWrap;
        private List<CustomApp> _customApps = new();
        private readonly string _libraryFile = Path.Combine(AppConfig.DataDir, "library.json");

        public LibraryPage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            LoadLibrary();

            var root = new Grid { Margin = new Thickness(40) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Grid

            // --- HEADER ---
            var header = new Grid { Margin = new Thickness(0, 0, 0, 32) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = "📚 Library", Foreground = Brushes.White, FontSize = 36, FontWeight = FontWeights.Bold });
            titleStack.Children.Add(new TextBlock { Text = "Manage and launch your custom applications", Foreground = Brushes.Gray, FontSize = 14 });
            header.Children.Add(titleStack);

            var addBtn = new Button { 
                Content = "+ Add Application", 
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Padding = new Thickness(20, 10, 20, 10)
            };
            addBtn.Click += AddApp_Click;
            Grid.SetColumn(addBtn, 1);
            header.Children.Add(addBtn);

            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // --- APPS GRID ---
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _appsWrap = new WrapPanel();
            scroll.Content = _appsWrap;

            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            RefreshGrid();
            Content = root;
        }

        private void RefreshGrid()
        {
            _appsWrap.Children.Clear();
            foreach (var app in _customApps)
            {
                _appsWrap.Children.Add(CreateAppCard(app));
            }
        }

        private Border CreateAppCard(CustomApp app)
        {
            var card = new Border
            {
                Width = 200, Height = 250,
                Margin = new Thickness(0, 0, 20, 20),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 45)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            // Icon
            var iconImg = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 0, 16) };
            try {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(app.Path);
                if (icon != null) iconImg.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            } catch { }
            stack.Children.Add(iconImg);

            // Title
            stack.Children.Add(new TextBlock { 
                Text = app.Name, 
                Foreground = Brushes.White, 
                FontSize = 16, 
                FontWeight = FontWeights.Bold, 
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(12, 0, 12, 0)
            });

            card.Child = stack;

            card.MouseEnter += (_, _) => { card.BorderBrush = Brushes.DeepSkyBlue; card.Background = new SolidColorBrush(Color.FromRgb(40, 40, 60)); };
            card.MouseLeave += (_, _) => { card.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)); card.Background = new SolidColorBrush(Color.FromRgb(30, 30, 45)); };
            card.MouseLeftButtonDown += (_, _) => LaunchApp(app);

            // Right click to remove
            var cm = new ContextMenu();
            var remove = new MenuItem { Header = "Remove from Library" };
            remove.Click += (_, _) => { _customApps.Remove(app); SaveLibrary(); RefreshGrid(); };
            cm.Items.Add(remove);
            card.ContextMenu = cm;

            return card;
        }

        private void AddApp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
            {
                var app = new CustomApp { 
                    Name = Path.GetFileNameWithoutExtension(openFileDialog.FileName), 
                    Path = openFileDialog.FileName 
                };
                _customApps.Add(app);
                SaveLibrary();
                RefreshGrid();
            }
        }

        private void LaunchApp(CustomApp app)
        {
            try { Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show($"Failed to launch app: {ex.Message}"); }
        }

        private void LoadLibrary()
        {
            if (File.Exists(_libraryFile))
            {
                try {
                    var json = File.ReadAllText(_libraryFile);
                    _customApps = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CustomApp>>(json) ?? new List<CustomApp>();
                } catch { }
            }
        }

        private void SaveLibrary()
        {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(_libraryFile)!);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_customApps);
                File.WriteAllText(_libraryFile, json);
            } catch { }
        }
    }

    public class CustomApp
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
