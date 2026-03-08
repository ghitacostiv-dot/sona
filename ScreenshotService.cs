using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace SONA.Services
{
    public class ScreenshotService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_P = 0x50;
        private const int HOTKEY_ID = 9000;

        private IntPtr _windowHandle;
        private HwndSource? _source;

        public void Hook(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            if (_windowHandle == IntPtr.Zero)
            {
                // Can happen if called before window handle is created, usually call in SourceInitialized
                return;
            }
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(HwndHook);
            RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_ALT | MOD_SHIFT, VK_P);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                TakeScreenshot();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void TakeScreenshot()
        {
            try
            {
                int left = (int)SystemParameters.VirtualScreenLeft;
                int top = (int)SystemParameters.VirtualScreenTop;
                int width = (int)SystemParameters.VirtualScreenWidth;
                int height = (int)SystemParameters.VirtualScreenHeight;

                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(left, top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    }
                    string dir = Path.Combine(AppConfig.DataDir, "screenshots");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    bmp.Save(file, ImageFormat.Png);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Super Screenshot captured!\nSaved to: {file}", "SONA Screenshot", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error taking screenshot: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _windowHandle = IntPtr.Zero;
            }
        }
    }
}
