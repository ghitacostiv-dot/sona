using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SONA.Services;

namespace SONA
{
    public class DebloatPage : UserControl
    {
        private TextBox _outputBox = new();
        private readonly ContentControl _contentArea = new();
        private RadioButton _tabTweaks = new();
        private RadioButton _tabBloat = new();
        private RadioButton _tabLogs = new();
        
        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0xf5, 0x9e, 0x0b)); // Orange Accent

        private static readonly (string Name, string Description, string Command, bool IsScript)[] Tweaks =
        {
            ("Disable Xbox Game DVR", "Reduces CPU overhead in games", "reg add HKCU\\System\\GameConfigStore /v GameDVR_Enabled /t REG_DWORD /d 0 /f", false),
            ("Disable Cortana", "Disables Microsoft Cortana", "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search /v AllowCortana /t REG_DWORD /d 0 /f", false),
            ("Disable Telemetry", "Reduces Microsoft telemetry/data collection", "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection /v AllowTelemetry /t REG_DWORD /d 0 /f", false),
            ("Disable Advertising ID", "Stops personalized ad tracking", "reg add HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo /v Enabled /t REG_DWORD /d 0 /f", false),
            ("Enable Game Mode", "Windows Game Mode for better performance", "reg add HKCU\\Software\\Microsoft\\GameBar /v AllowAutoGameMode /t REG_DWORD /d 1 /f", false),
            ("High Performance Plan", "Enables max performance power plan", "powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", false),
            ("Disable Search Indexing", "Stops Windows indexing service", "sc stop WSearch & sc config WSearch start=disabled", false),
            ("Disable Print Spooler", "Saves RAM if no printer connected", "sc stop Spooler & sc config Spooler start=disabled", false),
            ("Disable Fax Service", "Removes unused fax service", "sc stop Fax & sc config Fax start=disabled", false),
            ("Disable Error Reporting", "Stops crash report uploads", "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting /v Disabled /t REG_DWORD /d 1 /f", false),
            ("Clear Temp Files", "Deletes Windows temp directory", "rd /s /q %temp% & mkdir %temp%", false),
            ("Flush DNS Cache", "Clears DNS resolver cache", "ipconfig /flushdns", false),
            ("Disable Animations", "Disables UI animations for speed", "reg add HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects /v VisualFXSetting /t REG_DWORD /d 2 /f", false),
            ("Enable Long Paths", "Enables Win32 long path support", "reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f", false),
            ("Disable Fast Startup", "Fixes shutdown/hibernate issues", "reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power /v HiberbootEnabled /t REG_DWORD /d 0 /f", false),
        };

        private static readonly (string Name, string PackageName)[] Bloatware =
        {
            ("Candy Crush", "king.com.CandyCrushSaga"), ("Twitter", "9E2F88E3840D"), ("Xbox App", "Microsoft.XboxApp"), ("3D Viewer", "Microsoft.Microsoft3DViewer"), ("Solitaire", "Microsoft.MicrosoftSolitaireCollection"), ("Tips", "Microsoft.WindowsTips"), ("Weather", "Microsoft.BingWeather"), ("Skype", "Microsoft.SkypeApp"),
        };

        public DebloatPage()
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) });

            // LEFT PANEL
            var leftDock = new DockPanel();
            
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 24, 0, 0) };
            _tabTweaks = CreateTabButton("Tweaks", true);
            _tabBloat = CreateTabButton("Uninstaller", false);
            _tabLogs = CreateTabButton("Detailed Logs", false);
            
            tabRow.Children.Add(_tabTweaks);
            tabRow.Children.Add(_tabBloat);
            tabRow.Children.Add(_tabLogs);
 
            leftDock.Children.Add(tabRow);
            DockPanel.SetDock(tabRow, Dock.Top);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _contentArea;
            leftDock.Children.Add(scroll);

            Grid.SetColumn(leftDock, 0);
            rootGrid.Children.Add(leftDock);

            // RIGHT PANEL (Activity Log)
            var rightBorder = new Border { BorderThickness = new Thickness(1, 0, 0, 0) };
            rightBorder.SetResourceReference(Border.BackgroundProperty, "Bg2Brush");
            rightBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var sideDock = new DockPanel();
            
            var sideHeader = new TextBlock { Text = "CLEANUP_STATUS", Foreground = _accentBrush, FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(12, 12, 12, 6) };
            DockPanel.SetDock(sideHeader, Dock.Top);
            sideDock.Children.Add(sideHeader);

            _outputBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = Brushes.LightGray,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 12, 12),
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
            };
            sideDock.Children.Add(_outputBox);
            rightBorder.Child = sideDock;
            Grid.SetColumn(rightBorder, 1);
            rootGrid.Children.Add(rightBorder);

            Content = rootGrid;

            // Wire events after UI is ready to avoid null refs during initialization
            _tabTweaks.Checked += (_, _) => { if (IsLoaded) ShowTweaksView(); };
            _tabBloat.Checked += (_, _) => { if (IsLoaded) ShowBloatView(); };
            _tabLogs.Checked += (_, _) => { if (IsLoaded) ShowLogsView(); };
            ShowTweaksView();
        }

        private RadioButton CreateTabButton(string text, bool isActive)
        {
            return new RadioButton
            {
                Content = text,
                Style = (Style)Application.Current.FindResource("PageTab"),
                GroupName = "DebloatTabs",
                IsChecked = isActive
            };
        }

        private void ShowTweaksView()
        {
            var stack = new StackPanel { Margin = new Thickness(24) };
            
            var headGrid = new Grid { Margin = new Thickness(0,0,0,24) };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var icon = IconHelper.Img("nav/debloat", 40);
            icon.Margin = new Thickness(0, 0, 16, 0);
            headerRow.Children.Add(icon);
            headerRow.Children.Add(new TextBlock { Text = "System Optimizer", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            headGrid.Children.Add(headerRow);
            
            // Chris Titus Tech Utility CTA
            var titusBtn = new Button 
            {
                Content = "Launch Windows Utility (Chris Titus Tech)",
                Style = (Style)Application.Current.FindResource("AccentBtn"),
                Background = _accentBrush,
                Height = 44,
                Padding = new Thickness(20, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FontWeight = FontWeights.Bold
            };
            titusBtn.Click += (_, _) => _ = RunCommandAsync("Windows Utility", "powershell -Command \"irm christitus.com/win | iex\"");
            headGrid.Children.Add(titusBtn);

            stack.Children.Add(headGrid);
            stack.Children.Add(new TextBlock { Text = "Apply local performance tweaks and disable telemetry. Requires Admin.", Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0, -8, 0, 24) });

            var wrap = new WrapPanel();
            foreach (var tweak in Tweaks)
            {
                var card = new Border { Width = 280, Height = 100, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 16, 16), Padding = new Thickness(16) };
                card.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
                var dp = new DockPanel();
                var btn = new Button { Content = "Apply", Style = (Style)Application.Current.FindResource("DarkBtn"), Height = 28, Width = 60, VerticalAlignment = VerticalAlignment.Bottom };
                btn.Click += (_, _) => _ = RunCommandAsync(tweak.Name, tweak.Command);
                DockPanel.SetDock(btn, Dock.Right); dp.Children.Add(btn);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
                info.Children.Add(new TextBlock { Text = tweak.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextTrimming = TextTrimming.CharacterEllipsis });
                info.Children.Add(new TextBlock { Text = tweak.Description, Foreground = Brushes.Gray, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                dp.Children.Add(info);
                card.Child = dp; wrap.Children.Add(card);
            }
            stack.Children.Add(wrap);
            _contentArea.Content = stack;
        }

        private void ShowBloatView()
        {
            var stack = new StackPanel { Margin = new Thickness(24) };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,24) };
            var icon = IconHelper.Img("nav/debloat", 40);
            icon.Margin = new Thickness(0, 0, 16, 0);
            headerRow.Children.Add(icon);
            headerRow.Children.Add(new TextBlock { Text = "Bloatware Remover", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(headerRow);

            var checkboxes = new Dictionary<string, CheckBox>();
            var wrap = new WrapPanel();
            foreach (var bloat in Bloatware)
            {
                var row = new Border { Width = 350, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 0, 16, 16) };
                row.SetResourceReference(Border.BackgroundProperty, "Bg3Brush");
                var dp = new DockPanel();
                var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                checkboxes[bloat.PackageName] = cb;
                dp.Children.Add(cb);
                
                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = bloat.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                info.Children.Add(new TextBlock { Text = bloat.PackageName, Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
                dp.Children.Add(info);
                row.Child = dp; wrap.Children.Add(row);
            }
            stack.Children.Add(wrap);

            var removeBtn = new Button { Content = "🗑 Remove Selected", Style = (Style)Application.Current.FindResource("AccentBtn"), Background = _accentBrush, Height = 40, Width = 200, Margin = new Thickness(0, 24, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            removeBtn.Click += (_, _) => { foreach (var kv in checkboxes) if (kv.Value.IsChecked == true) _ = RunCommandAsync($"Remove {kv.Key}", $"powershell -Command \"Get-AppxPackage {kv.Key} | Remove-AppxPackage\""); };
            stack.Children.Add(removeBtn);

            _contentArea.Content = stack;
        }

        private void ShowLogsView()
        {
            var logGrid = new Grid();
            logGrid.SetResourceReference(Grid.BackgroundProperty, "BgBrush");
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var box = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.LightGray, FontFamily = new FontFamily("Consolas"), FontSize = 14, IsReadOnly = true, BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24), AcceptsReturn = true, Text = _outputBox.Text };
            box.TextChanged += (s, e) => _outputBox.Text = box.Text;
            scroll.Content = box;
            logGrid.Children.Add(scroll);
            _contentArea.Content = logGrid;
        }

        private async Task RunCommandAsync(string name, string command)
        {
            AppendLog($"▶ {name}");
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c {command}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var proc = Process.Start(psi);
                if (proc != null) { await proc.WaitForExitAsync(); AppendLog(proc.ExitCode == 0 ? "  ✅ Success" : "  ❌ Failed"); }
            }
            catch (Exception ex) { AppendLog($"  ⚠ {ex.Message}"); }
        }

        private void AppendLog(string text) { Dispatcher.Invoke(() => { _outputBox.AppendText(text + "\n"); _outputBox.ScrollToEnd(); }); }
    }
}
