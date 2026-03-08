using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SONA.Services;

namespace SONA
{
    public class InstallPage : UserControl
    {
        private TextBox _outputBox = new();

        // Static persistent controls (never rebuilt)
        private TextBox _searchBox = new();
        private ComboBox _catCombo = new();
        private StackPanel _appListPanel = new();   // only this gets refreshed
        private readonly ContentControl _tabContent = new(); // for Uninstall/Logs tabs
        private Grid _appsGrid = new();             // holds toolbar + list
        private ScrollViewer _appsScroll = new();

        private RadioButton _tabApps = new();
        private RadioButton _tabUninstall = new();
        private RadioButton _tabLogs = new();

        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x0e, 0xa5, 0xe9));
        // AI-chatbox card bg: deep blue-gray, semi-transparent
        private static readonly Color CardBg  = Color.FromArgb(0xCC, 0x16, 0x18, 0x2e); // dark navy
        private static readonly Color CardBorder = Color.FromArgb(0x55, 0x6e, 0x6e, 0xaa);

        private readonly Dictionary<AppEntry, CheckBox> _checkboxes = new();
        private string _selectedCategory = "";
        private List<InstalledProgram> _allInstalledPrograms = new();

        public InstallPage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) });

            // ── LEFT PANEL ──────────────────────────────────────────────────
            var leftDock = new DockPanel();

            // Tab row
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 24, 0, 0) };
            _tabApps = CreateTabButton("Apps", "actions/search", true);
            _tabUninstall = CreateTabButton("Uninstaller", "actions/uninstall", false);
            _tabLogs = CreateTabButton("Logs", "actions/logs", false);
            tabRow.Children.Add(_tabApps);
            tabRow.Children.Add(_tabUninstall);
            tabRow.Children.Add(_tabLogs);

            DockPanel.SetDock(tabRow, Dock.Top);
            leftDock.Children.Add(tabRow);

            // ── STATIC APPS GRID (toolbar + list) ───────────────────────────
            _appsGrid = new Grid();
            _appsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // toolbar
            _appsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list

            // Persistent toolbar
            var toolbar = BuildAppsToolbar();
            Grid.SetRow(toolbar, 0);
            _appsGrid.Children.Add(toolbar);

            // Persistent scrollable list
            _appListPanel = new StackPanel { Margin = new Thickness(24, 0, 24, 24) };
            _appsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _appsScroll.Content = _appListPanel;
            Grid.SetRow(_appsScroll, 1);
            _appsGrid.Children.Add(_appsScroll);

            // Tab-content placeholder (Uninstall / Logs / etc.)
            _tabContent.Visibility = Visibility.Collapsed;

            // Outer host that shows either _appsGrid or _tabContent
            var contentHost = new Grid();
            contentHost.Children.Add(_appsGrid);
            contentHost.Children.Add(_tabContent);

            leftDock.Children.Add(contentHost);
            Grid.SetColumn(leftDock, 0);
            rootGrid.Children.Add(leftDock);

            // ── RIGHT PANEL (log sidebar) ────────────────────────────────────
            var rightBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            var sideDock = new DockPanel();
            var sideHeader = new TextBlock { Text = "INSTALL_STATUS", Foreground = _accentBrush, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 12, 12, 6) };
            DockPanel.SetDock(sideHeader, Dock.Top);
            sideDock.Children.Add(sideHeader);
            _outputBox = new TextBox
            {
                Background = Brushes.Transparent, Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Consolas"), FontSize = 10, IsReadOnly = true,
                BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 12, 12), VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
            };
            sideDock.Children.Add(_outputBox);
            rightBorder.Child = sideDock;
            Grid.SetColumn(rightBorder, 1);
            rootGrid.Children.Add(rightBorder);

            Content = rootGrid;

            // Wire events after UI is ready to avoid null refs during initialization
            _tabApps.Checked += (_, _) => { if (IsLoaded) ShowAppsView(); };
            _tabUninstall.Checked += (_, _) => { if (IsLoaded) ShowUninstallView(); };
            _tabLogs.Checked += (_, _) => { if (IsLoaded) ShowLogsView(); };

            // Initial load
            if (AppRegistry.Apps.Count > 0) _selectedCategory = AppRegistry.Apps.Keys.First();
            _catCombo.SelectedItem = _selectedCategory;
            RefreshAppList();
        }

        // ── Build persistent toolbar (called once) ───────────────────────────
        private Border BuildAppsToolbar()
        {
            var toolbar = new Border
            {
                Padding = new Thickness(24, 18, 24, 18),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            toolbar.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
            toolbar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var dp = new DockPanel { LastChildFill = false };

            // Title
            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 24, 0) };
            titleSp.Children.Add(IconHelper.Img("nav/install", 32));
            titleSp.Children.Add(new TextBlock { Text = "Software Armory", Foreground = Brushes.White, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            dp.Children.Add(titleSp);

            // Category dropdown
            _catCombo = new ComboBox
            {
                Width = 200,
                Height = 36,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            foreach (var cat in AppRegistry.Apps.Keys) _catCombo.Items.Add(cat);
            if (AppRegistry.Apps.Count > 0) _catCombo.SelectedIndex = 0;
            _catCombo.SelectionChanged += (s, e) =>
            {
                if (_catCombo.SelectedItem is string cat) { _selectedCategory = cat; RefreshAppList(); }
            };
            dp.Children.Add(_catCombo);

            // Search box
            _searchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Width = 260, Height = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _searchBox.TextChanged += (s, e) => RefreshAppList();
            dp.Children.Add(_searchBox);

            // Install button
            var installBtn = new Button
            {
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Background = _accentBrush,
                Height = 36,
                Padding = new Thickness(16, 0, 16, 0)
            };
            var installSp = new StackPanel { Orientation = Orientation.Horizontal };
            installSp.Children.Add(IconHelper.Img("actions/download", 16));
            installSp.Children.Add(new TextBlock { Text = "Install Selected", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            installBtn.Content = installSp;
            DockPanel.SetDock(installBtn, Dock.Right);
            installBtn.Click += (_, _) => _ = InstallSelectedAsync();
            dp.Children.Add(installBtn);

            toolbar.Child = dp;
            return toolbar;
        }

        // ── Refresh only the list panel ───────────────────────────────────────
        private void RefreshAppList()
        {
            _appListPanel.Children.Clear();
            _checkboxes.Clear();

            if (!AppRegistry.Apps.TryGetValue(_selectedCategory, out var apps)) return;
            var filter = _searchBox?.Text ?? "";
            var filtered = apps.Where(a => string.IsNullOrEmpty(filter) || a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var app in filtered)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(CardBg),
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(16, 14, 16, 14),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(CardBorder)
                };

                var dp = new DockPanel();
                var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
                _checkboxes[app] = cb;
                DockPanel.SetDock(cb, Dock.Left);
                dp.Children.Add(cb);

                // App Icon Badge
                var iconBadge = new Border
                {
                    Width = 44, Height = 44,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(60, 0x6e, 0x72, 0xaa)),
                    Margin = new Thickness(0, 0, 14, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = new TextBlock
                {
                    Text = string.IsNullOrEmpty(app.Icon) ? "📦" : app.Icon,
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(iconBadge, Dock.Left);
                dp.Children.Add(iconBadge);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = app.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14 });
                info.Children.Add(new TextBlock { Text = app.Description, Foreground = new SolidColorBrush(Color.FromRgb(0xb0, 0xb4, 0xcc)), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                info.Children.Add(new TextBlock { Text = $"{app.Installer.ToUpper()} · {app.PackageId}", Foreground = new SolidColorBrush(Color.FromRgb(0x6e, 0x72, 0x9a)), FontSize = 10, Margin = new Thickness(0, 6, 0, 0) });
                dp.Children.Add(info);
                card.Child = dp;

                // Hover effect
                card.MouseEnter += (s, e) => card.BorderBrush = _accentBrush;
                card.MouseLeave += (s, e) => card.BorderBrush = new SolidColorBrush(CardBorder);

                _appListPanel.Children.Add(card);
            }

            // Empty state
            if (filtered.Count == 0)
                _appListPanel.Children.Add(new TextBlock { Text = "No apps found.", Foreground = Brushes.Gray, Margin = new Thickness(0, 24, 0, 0), FontSize = 14 });
        }

        // ── Tab helpers ─────────────────────────────────────────────────────
        private void ShowAppsView()
        {
            _appsGrid.Visibility = Visibility.Visible;
            _tabContent.Visibility = Visibility.Collapsed;
        }

        private async void ShowUninstallView()
        {
            _appsGrid.Visibility = Visibility.Collapsed;
            _tabContent.Visibility = Visibility.Visible;

            var stack = new StackPanel { Margin = new Thickness(24) };
            stack.Children.Add(new TextBlock { Text = "Uninstaller", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 24) });
            var loading = new TextBlock { Text = "Enumerating installed programs...", Foreground = Brushes.Gray, FontSize = 14 };
            stack.Children.Add(loading);
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            sv.Content = stack;
            _tabContent.Content = sv;

            _allInstalledPrograms = await Task.Run(() => SystemUtils.GetInstalledPrograms());
            stack.Children.Remove(loading);

            var search = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1a, 0x1a, 0x3e)),
                Foreground = Brushes.White, CaretBrush = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x6e, 0x6e, 0xcc)),
                Height = 36, Margin = new Thickness(0, 0, 0, 16), Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var listContainer = new StackPanel();
            search.TextChanged += (s, e) => FilterUninstallItems(listContainer, search.Text);
            stack.Children.Add(search);
            stack.Children.Add(listContainer);
            FilterUninstallItems(listContainer, "");
        }

        private void ShowLogsView()
        {
            _appsGrid.Visibility = Visibility.Collapsed;
            _tabContent.Visibility = Visibility.Visible;
            var box = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = Brushes.LightGray, FontFamily = new FontFamily("Consolas"),
                FontSize = 14, IsReadOnly = true, BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), AcceptsReturn = true,
                Text = _outputBox.Text
            };
            _tabContent.Content = box;
        }

        private void FilterUninstallItems(StackPanel container, string filter)
        {
            container.Children.Clear();
            var filtered = _allInstalledPrograms
                .Where(p => string.IsNullOrEmpty(filter) || p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Take(100);
            foreach (var app in filtered)
            {
                var row = new Border { Background = new SolidColorBrush(CardBg), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(12) };
                var dp = new DockPanel();
                var btn = new Button { Content = "Uninstall", Style = (Style)Application.Current.FindResource("DarkBtn"), Height = 28, Width = 80, VerticalAlignment = VerticalAlignment.Center };
                btn.Click += (_, _) => { if (MessageBox.Show($"Uninstall {app.DisplayName}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { SystemUtils.UninstallProgram(app, out _); ShowUninstallView(); } };
                DockPanel.SetDock(btn, Dock.Right); dp.Children.Add(btn);
                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = app.DisplayName, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold });
                info.Children.Add(new TextBlock { Text = app.Publisher, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xaa)), FontSize = 10 });
                dp.Children.Add(info);
                row.Child = dp; container.Children.Add(row);
            }
        }

        private RadioButton CreateTabButton(string text, string iconKey, bool isActive) => new()
        {
            Content = IconHelper.NavItem(iconKey, text),
            Style = (Style)Application.Current.FindResource("PageTab"),
            GroupName = "InstallTabs",
            IsChecked = isActive
        };



        private async Task InstallSelectedAsync()
        {
            var selected = _checkboxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (selected.Count == 0) return;
            AppendLog($"🚀 Installing {selected.Count} apps...");
            foreach (var app in selected)
            {
                if (app.Installer == "winget")
                {
                    AppendLog($"⬇ {app.Name} (WinGet)...");
                    var success = await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo("winget", $"install --id {app.PackageId} --silent --accept-package-agreements --accept-source-agreements") { CreateNoWindow = true, UseShellExecute = false };
                        var p = Process.Start(psi); p?.WaitForExit(); return p?.ExitCode == 0;
                    });
                    AppendLog(success ? "  ✅ Done" : "  ❌ Failed");
                }
                else if (app.Installer == "direct")
                {
                    AppendLog($"ℹ {app.Name}: Opening download link...");
                    Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(app.Name + " apk download")) { UseShellExecute = true });
                }
            }
        }

        private void AppendLog(string text) { Dispatcher.Invoke(() => { _outputBox.AppendText(text + "\n"); _outputBox.ScrollToEnd(); }); }
    }
}
