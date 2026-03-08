using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using SONA.Pages;
using SONA.Services;

namespace SONA
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // ── Performance & Network Optimization ───────────────────────────
            System.Net.ServicePointManager.DefaultConnectionLimit = 100;
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.UseNagleAlgorithm = false;
            
            // Set process priority to Above Normal for better responsiveness
            try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal; } catch { }

            // ── Global crash guard ────────────────────────────────────────────
            DispatcherUnhandledException += (_, ex) =>
            {
                LoggingService.LogFatal("DispatcherUnhandledException", ex.Exception);
                MessageBox.Show($"An unexpected error occurred:\n\n{ex.Exception.Message}",
                    "SONA Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true; // Prevent app from silently dying
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                var err = ex.ExceptionObject as Exception;
                LoggingService.LogFatal("UnhandledException", err ?? new Exception(ex.ExceptionObject?.ToString()));
                MessageBox.Show($"Fatal error:\n\n{err?.Message ?? ex.ExceptionObject?.ToString()}",
                    "SONA Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (_, ex) =>
            {
                ex.SetObserved();
                LoggingService.LogFatal("UnobservedTaskException", ex.Exception);
            };

            try
            {
                // Enable hardware acceleration and high-quality rendering
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
                Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata(144)); // Target higher refresh rates if available

                // Kill orphaned WebView2 processes from previous sessions
                try
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName("msedgewebview2"))
                        try { proc.Kill(); } catch { }
                }
                catch { }

                AppConfig.EnsureDirectories();
                LoggingService.Initialize();

                base.OnStartup(e);

                SONA.Services.ThemeManager.ApplyTheme(AppConfig.GetString("theme", "dark"));
                SONA.Services.ThemeManager.ApplyAccentColor(AppConfig.GetString("accent_color", "#7c3aed"));

                // bool firstRunDone = FirstRunCompleted();
                // 
                // if (AppConfig.GetBool("run_first_run_wizard", true) && !firstRunDone)
                // {
                //     try
                //     {
                //         var wizard = new DependencyWizard();
                //         wizard.ShowDialog();
                //     }
                //     catch (Exception ex)
                //     {
                //         LoggingService.Log($"DependencyWizard skipped: {ex.Message}");
                //     }
                //     MarkFirstRunCompleted();
                // }

                // ── Launch main window first so user sees the UI right away ──
                try
                {
                    var mainWindow = new MainWindow();
                    this.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                catch (Exception ex)
                {
                    LoggingService.LogFatal("MainWindow creation failed", ex);
                    MessageBox.Show("SONA could not initialize the main window.\n\n" + ex.Message + "\n\nCheck logs for more details.", "Startup Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }

                // ── Then run install logic in the background ──────────────────
                // _ = RunInstallsAsync();
            }
            catch (Exception ex)
            {
                if (LoggingService.InitializeDone)
                    LoggingService.LogFatal("App OnStartup", ex);
                MessageBox.Show("SONA crashed on startup.\n\n" + ex.Message, "Fatal Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private async Task RunInstallsAsync()
        {
            // Only run installs if apps are actually missing
            bool anyMissing = false;
            foreach (var id in new[] { "brave","yacreader","poddr","retrobat","osu","tailscale","moonlight",
                                       "steam","epic","spotify","discord","whatsapp","battlenet",
                                       "nuclear","miru","librum","mangayomi","hydra" })
            {
                if (string.IsNullOrEmpty(AppManagerService.FindExecutable(id)))
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing) return; // All apps already installed — skip entirely

            // Show the floating install-status window on the UI thread
            InstallStatusWindow? statusWin = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                statusWin = new InstallStatusWindow();
                statusWin.Show();
            });

            try
            {
                await AppManagerService.AutoRunFirstTimeChecksAsync(msg =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() => statusWin?.Log(msg));
                });
            }
            finally
            {
                Application.Current.Dispatcher.InvokeAsync(() => statusWin?.MarkDone());
            }
        }

        private static bool FirstRunCompleted()
        {
            string flagFile = Path.Combine(AppConfig.AppDir, "firstrun.completed");
            return File.Exists(flagFile);
        }

        private static void MarkFirstRunCompleted()
        {
            string dir = AppConfig.AppDir;
            Directory.CreateDirectory(dir);
            try { File.Create(Path.Combine(dir, "firstrun.completed")).Close(); } catch { }
        }
    }

    public class BoolToStatusConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is bool b && b ? "✅ Installed" : "❌ Missing";

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

