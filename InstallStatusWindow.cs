using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SONA.Services;

namespace SONA.Pages
{
    /// <summary>
    /// Small floating window shown during first-run app installation.
    /// It receives progress messages and auto-closes when all installs complete.
    /// </summary>
    public class InstallStatusWindow : Window
    {
        private readonly StackPanel _logPanel;
        private readonly ScrollViewer _scroll;
        private readonly TextBlock _title;
        private readonly ProgressBar _progress;
        private readonly Button _closeBtn;

        public InstallStatusWindow()
        {
            Title           = "SONA — First-Run Setup";
            Width           = 480;
            Height          = 520;
            WindowStyle     = WindowStyle.ToolWindow;
            ResizeMode      = ResizeMode.NoResize;
            Background      = new SolidColorBrush(Color.FromRgb(0x0d, 0x0d, 0x14));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost         = true;
            ShowInTaskbar   = false;

            var root = new DockPanel { Margin = new Thickness(16) };

            // ── Header ───────────────────────────────────────────────────────
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(header, Dock.Top);

            _title = new TextBlock
            {
                Text       = "🔧 First-Run Setup — Installing Apps",
                Foreground = Brushes.White,
                FontSize   = 17,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            header.Children.Add(_title);

            var sub = new TextBlock
            {
                Text       = "Apps are being installed silently. You can continue using SONA in the background.",
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                FontSize   = 12,
                Margin     = new Thickness(0, 4, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            header.Children.Add(sub);

            _progress = new ProgressBar
            {
                Height = 6,
                IsIndeterminate = true,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
                Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x3a)),
                BorderThickness = new Thickness(0)
            };
            header.Children.Add(_progress);
            root.Children.Add(header);

            // ── Log list ─────────────────────────────────────────────────────
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _logPanel = new StackPanel();
            _scroll.Content = _logPanel;
            root.Children.Add(_scroll);

            // ── Close button (shown after completion) ─────────────────────────
            _closeBtn = new Button
            {
                Content = "Close",
                Margin  = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(0x7c, 0x3a, 0xed)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Visibility = Visibility.Collapsed
            };
            _closeBtn.Click += (_, _) => Close();
            DockPanel.SetDock(_closeBtn, Dock.Bottom);
            root.Children.Insert(0, _closeBtn); // add at top of DockPanel so it docks bottom

            Content = root;
        }

        /// <summary>Appends a status message to the log panel (thread-safe).</summary>
        public void Log(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var entry = new TextBlock
                {
                    Text         = message,
                    Foreground   = message.StartsWith("⚠️") || message.StartsWith("❌")
                                       ? new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44))
                                       : message.StartsWith("✅")
                                           ? new SolidColorBrush(Color.FromRgb(0x10, 0xb9, 0x81))
                                           : new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    FontSize     = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin       = new Thickness(0, 2, 0, 2)
                };
                _logPanel.Children.Add(entry);
                _scroll.ScrollToEnd();
            });
        }

        /// <summary>Marks setup as complete and shows the Close button.</summary>
        public void MarkDone()
        {
            Dispatcher.InvokeAsync(() =>
            {
                _title.Text = "✅ First-Run Setup — Complete";
                _progress.IsIndeterminate = false;
                _progress.Value = 100;
                _closeBtn.Visibility = Visibility.Visible;
                Topmost = false;
            });
        }
    }
}
