using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace SONA.Services
{
    public static class LoggingService
    {
        private static string _currentLogPath;
        private static readonly object _lock = new object();
        public static bool InitializeDone { get; private set; }

        public static void Initialize()
        {
            try
            {
                ManageLogs();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentLogPath = Path.Combine(AppConfig.LogsDir, $"session_{timestamp}.log");
                
                Log("=== SONA Session Started ===");
                Log($"Time: {DateTime.Now}");
                Log($"OS: {Environment.OSVersion}");
                Log($"Runtime: {Environment.Version}");
                Log("============================");

                InitializeDone = true;

                // Global exception handling
                AppDomain.CurrentDomain.UnhandledException += (s, e) => 
                    LogFatal("AppDomain Unhandled Exception", e.ExceptionObject as Exception);
                
                if (Application.Current != null)
                {
                    Application.Current.DispatcherUnhandledException += (s, e) =>
                    {
                        LogFatal("Dispatcher Unhandled Exception", e.Exception);
                        e.Handled = false; // Let it crash but log first
                    };
                }
            }
            catch (Exception ex)
            {
                // Last resort if logging fails
                string crashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logging_failure.txt");
                File.WriteAllText(crashPath, ex.ToString());
            }
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(_currentLogPath)) return;

            lock (_lock)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                    File.AppendAllText(_currentLogPath, logEntry);
                }
                catch { }
            }
        }

        public static void LogFatal(string context, Exception ex)
        {
            string message = $"FATAL ERROR in {context}: {Environment.NewLine}{ex}";
            Log(message);
            // Also write a separate crash file in the app directory for immediate visibility if needed
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_crash.txt"), message);
            }
            catch { }
        }

        private static void ManageLogs()
        {
            try
            {
                var directory = new DirectoryInfo(AppConfig.LogsDir);
                var files = directory.GetFiles("session_*.log")
                                     .OrderByDescending(f => f.CreationTime)
                                     .ToList();

                // Keep only the last 4, because we are about to create a new one (total 5)
                if (files.Count >= 5)
                {
                    for (int i = 4; i < files.Count; i++)
                    {
                        try { files[i].Delete(); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
