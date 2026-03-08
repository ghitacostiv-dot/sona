using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using SONA.Services;

namespace SONA
{
    public class LinksPage : UserControl
    {
        private readonly MainWindow _mainWindow;
        private readonly WrapPanel _linksPanel = new() { Margin = new Thickness(10) };
        private readonly TextBox _searchBox = new();
        private string _currentCategory = "All";
        private readonly StackPanel _categoryStack = new();
        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x7c, 0x3a, 0xed)); // Violet

        public LinksPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // ── Header ────────────────────────────────────────────────────
            var header = new Border { Padding = new Thickness(24, 20, 24, 20), Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)) };
            var headerStack = new DockPanel();

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = IconHelper.Img("nav/browser", 40);
            icon.Margin = new Thickness(0, 0, 16, 0);
            titleStack.Children.Add(icon);
            titleStack.Children.Add(new TextBlock { Text = "LINKS & SOURCES", Foreground = Brushes.White, FontSize = 28, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            headerStack.Children.Add(titleStack);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(rightStack, Dock.Right);

            _searchBox.Style = (Style)Application.Current.FindResource("SearchBox");
            _searchBox.Width = 300;
            _searchBox.Height = 36;
            _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
            _searchBox.Margin = new Thickness(0, 0, 12, 0);
            _searchBox.TextChanged += (_, _) => RefreshLinks();
            rightStack.Children.Add(_searchBox);

            var addBtn = new Button { Content = "+ Add Link", Style = (Style)Application.Current.FindResource("AccentBtn"), Height = 36, Background = _accentBrush };
            addBtn.Click += (_, _) => ShowAddLinkDialog();
            rightStack.Children.Add(addBtn);

            headerStack.Children.Add(rightStack);
            header.Child = headerStack;
            rootGrid.Children.Add(header);

            // ── Main Content ─────────────────────────────────────────────
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // sidebar
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Sidebar
            var sidebar = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Margin = new Thickness(0, 10, 0, 10) };
            sidebar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var sideScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _categoryStack.Margin = new Thickness(12, 10, 12, 10);
            sideScroll.Content = _categoryStack;
            sidebar.Child = sideScroll;
            Grid.SetColumn(sidebar, 0);
            contentGrid.Children.Add(sidebar);

            // Links View
            var mainScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(14) };
            mainScroll.Content = _linksPanel;
            Grid.SetColumn(mainScroll, 1);
            contentGrid.Children.Add(mainScroll);

            Grid.SetRow(contentGrid, 1);
            rootGrid.Children.Add(contentGrid);

            Content = rootGrid;

            RefreshCategories();
            RefreshLinks();
        }

        private void RefreshCategories()
        {
            _categoryStack.Children.Clear();
            var categories = LinksService.GetCategories();
            foreach (var cat in categories)
            {
                var rb = new RadioButton
                {
                    Content = cat,
                    Style = (Style)Application.Current.FindResource("PageTab"),
                    GroupName = "LinkCats",
                    IsChecked = (cat == _currentCategory),
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                rb.Checked += (_, _) => { _currentCategory = cat; RefreshLinks(); };
                _categoryStack.Children.Add(rb);
            }
        }

        private void RefreshLinks()
        {
            _linksPanel.Children.Clear();
            var links = LinksService.GetAll();
            var query = _searchBox.Text.ToLower();

            var filtered = links.Where(l => 
                (_currentCategory == "All" || l.Category == _currentCategory) &&
                (string.IsNullOrEmpty(query) || l.Title.ToLower().Contains(query) || l.Url.ToLower().Contains(query))
            ).ToList();

            foreach (var link in filtered)
            {
                _linksPanel.Children.Add(CreateLinkCard(link));
            }
        }

        private FrameworkElement CreateLinkCard(LinkItem link)
        {
            var card = new Border
            {
                Width = 220, Height = 90,
                Margin = new Thickness(8),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(40, 30, 30, 50)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var grid = new Grid { Margin = new Thickness(12) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var title = new TextBlock
            {
                Text = link.Title,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            grid.Children.Add(title);

            var urlText = new TextBlock
            {
                Text = link.Url,
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(urlText, 1);
            grid.Children.Add(urlText);

            var catLabel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, _accentBrush.Color.R, _accentBrush.Color.G, _accentBrush.Color.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = link.Category, Foreground = _accentBrush, FontSize = 9, FontWeight = FontWeights.Bold }
            };
            grid.Children.Add(catLabel);

            card.Child = grid;

            card.MouseEnter += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(80, 50, 50, 80));
                card.BorderBrush = _accentBrush;
            };
            card.MouseLeave += (_, _) => {
                card.Background = new SolidColorBrush(Color.FromArgb(40, 30, 30, 50));
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            };
            card.MouseLeftButtonDown += async (_, _) => {
                var streamableCategories = new[] { "movies", "anime", "tv", "Live TV", "radio" };
                if (streamableCategories.Contains(link.Category, StringComparer.OrdinalIgnoreCase))
                {
                    var originalText = title.Text;
                    var originalForeground = title.Foreground;
                    title.Text = "Starting Scraper...";
                    title.Foreground = Brushes.Orange;
                    try
                    {
                        var result = await ScraperService.ScrapeAsync(link.Url);
                        if (result != null && result.Success && !string.IsNullOrEmpty(result.StreamUrl))
                        {
                            _mainWindow.PlayVideo(result.StreamUrl, link.Title, result.Headers);
                        }
                        else
                        {
                            var err = result?.Error ?? "No direct stream found.";
                            MessageBox.Show($"Scraping failed: {err}\nFalling back to browser.", "SONA Master Scraper", MessageBoxButton.OK, MessageBoxImage.Warning);
                            Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
                        }
                    }
                    catch
                    {
                        Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
                    }
                    finally
                    {
                        title.Text = originalText;
                        title.Foreground = originalForeground;
                    }
                }
                else
                {
                    Process.Start(new ProcessStartInfo(link.Url) { UseShellExecute = true });
                }
            };
            
            // Context menu for delete
            var cm = new ContextMenu();
            var delItem = new MenuItem { Header = "Remove Link" };
            delItem.Click += (_, _) => { LinksService.RemoveLink(link); RefreshLinks(); };
            cm.Items.Add(delItem);
            card.ContextMenu = cm;

            return card;
        }

        private void ShowAddLinkDialog()
        {
            var popup = new Window
            {
                Title = "Add New Link",
                Width = 400, Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                WindowStyle = WindowStyle.None, AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1a)),
                BorderBrush = _accentBrush, BorderThickness = new Thickness(1)
            };

            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "Add Resource Link", Foreground = Brushes.White, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 20) });

            panel.Children.Add(new TextBlock { Text = "Title:", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
            var titleBox = new TextBox { Style = (Style)Application.Current.FindResource("SearchBox"), Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(titleBox);

            panel.Children.Add(new TextBlock { Text = "URL:", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
            var urlBox = new TextBox { Style = (Style)Application.Current.FindResource("SearchBox"), Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(urlBox);

            panel.Children.Add(new TextBlock { Text = "Category:", Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });
            var catBox = new ComboBox { Margin = new Thickness(0, 0, 0, 20), IsEditable = true };
            foreach (var c in LinksService.GetCategories().Where(c => c != "All")) catBox.Items.Add(c);
            catBox.SelectedIndex = 0;
            panel.Children.Add(catBox);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("DarkBtn"), Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            cancelBtn.Click += (_, _) => popup.Close();
            var saveBtn = new Button { Content = "Add", Style = (Style)Application.Current.FindResource("AccentBtn"), Width = 100, Background = _accentBrush };
            saveBtn.Click += (_, _) => {
                if (!string.IsNullOrWhiteSpace(titleBox.Text) && !string.IsNullOrWhiteSpace(urlBox.Text)) {
                    LinksService.AddLink(titleBox.Text, urlBox.Text, catBox.Text);
                    RefreshCategories();
                    RefreshLinks();
                    popup.Close();
                }
            };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(saveBtn);
            panel.Children.Add(btnRow);

            popup.Content = panel;
            popup.ShowDialog();
        }
    }
}
