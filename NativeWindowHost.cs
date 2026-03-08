using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;

namespace SONA.Controls
{
    public class NativeWindowHost : HwndHost
    {
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);


        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "CreateWindowEx", CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateWindowEx(int dwExStyle,
                                                      string lpszClassName,
                                                      string lpszWindowName,
                                                      int style,
                                                      int x, int y,
                                                      int width, int height,
                                                      IntPtr hWndParent,
                                                      IntPtr hMenu,
                                                      IntPtr hInst,
                                                      [MarshalAs(UnmanagedType.AsAny)] object pvParam);

        private const int WS_VISIBLE = 0x10000000;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private IntPtr _appWindow = IntPtr.Zero;
        private Process? _process;
        private EnumWindowsProc? _enumWindowsProc; // Keep delegate alive

        public string? ProcessNameToKill { get; set; }
        public bool IsFloating { get; set; } = true;
        public bool KeepNativeControls { get; set; } = false;
        public bool IsSticky { get; set; } = false;
        private bool _hasResizedOnce = false;
        public double FloatingOffsetX { get; set; } = 0;
        public double FloatingOffsetY { get; set; } = 0;
        public double FloatingWidth { get; set; } = 0;
        public double FloatingHeight { get; set; } = 0;

        public NativeWindowHost()
        {
            _enumWindowsProc = OnEnumWindow;
            Focusable = true;
        }

        public void ApplyWindowStyles()
        {
            if (_appWindow == IntPtr.Zero) return;
            try
            {
                int style = GetWindowLong(_appWindow, GWL_STYLE);
                if (IsFloating)
                {
                    if (KeepNativeControls)
                    {
                        style |= WS_CAPTION;
                        style |= WS_THICKFRAME;
                        style |= WS_SYSMENU;
                        style |= WS_MINIMIZEBOX;
                        style |= WS_MAXIMIZEBOX;
                        style &= ~WS_CHILD;
                    }
                    else
                    {
                        style &= ~WS_BORDER;
                        style &= ~WS_CAPTION;
                        style &= ~WS_THICKFRAME;
                        style &= ~WS_SYSMENU;
                        style &= ~WS_MINIMIZEBOX;
                        style &= ~WS_MAXIMIZEBOX;
                    }
                }
                else
                {
                    style |= WS_CHILD;
                    style &= ~WS_POPUP;
                }
                SetWindowLong(_appWindow, GWL_STYLE, style);
                SetForegroundWindow(_appWindow);
                ForceResize();
            }
            catch { }
        }

        public void BringToFront()
        {
            if (_appWindow == IntPtr.Zero) return;
            try
            {
                SetForegroundWindow(_appWindow);
                SetFocus(_appWindow);
                SetActiveWindow(_appWindow);
            }
            catch { }
        }

        public async Task LoadAppAsync(string exePath, string arguments = "", bool killExisting = true)
        {
            try
            {
                // If not killing, check if we already have a window or a running process with a window
                if (!killExisting && !string.IsNullOrEmpty(ProcessNameToKill))
                {
                    var potentials = Process.GetProcessesByName(ProcessNameToKill);
                    foreach (var p in potentials)
                    {
                        IntPtr handle = GetProcessWindow(p);
                        if (handle != IntPtr.Zero)
                        {
                            _appWindow = handle;
                            _process = p;
                            AttachWindow();
                            return;
                        }
                    }
                }

                if (killExisting) KillExistingProcess();

                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(exePath)
                };

                _process = Process.Start(psi);
                if (_process == null) return;

                // Wait for a suitable window to be created
                _appWindow = await FindBestWindowHandle(_process, 100);
                
                if (_appWindow != IntPtr.Zero)
                {
                    AttachWindow();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NativeWindowHost] Error: {ex.Message}");
                throw;
            }
        }

        private void KillExistingProcess()
        {
            try
            {
                // Kill specific process tree if we have it
                if (_process != null)
                {
                    try { if (!_process.HasExited) _process.Kill(true); } catch { }
                    _process.Dispose();
                    _process = null;
                }

                // Aggressively kill by name if provided (useful for apps that spawn orphans)
                if (!string.IsNullOrEmpty(ProcessNameToKill))
                {
                    foreach (var proc in Process.GetProcessesByName(ProcessNameToKill))
                    {
                        try { proc.Kill(true); } catch { }
                    }
                }

                _appWindow = IntPtr.Zero;
            }
            catch { }
        }

        private async Task<IntPtr> FindBestWindowHandle(Process process, int maxAttempts)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                try { process.Refresh(); } catch { }
                
                // 1. Try windows of the specific process we just started
                if (!process.HasExited)
                {
                    IntPtr handle = GetProcessWindow(process);
                    if (handle != IntPtr.Zero) return handle;
                }

                // 2. If it exited (common for launchers) or didn't have a window yet, 
                // check all processes with the target name.
                if (!string.IsNullOrEmpty(ProcessNameToKill))
                {
                    var potentials = Process.GetProcessesByName(ProcessNameToKill);
                    foreach (var p in potentials)
                    {
                        IntPtr handle = GetProcessWindow(p);
                        if (handle != IntPtr.Zero) return handle;
                    }
                }

                await Task.Delay(250);
            }
            return IntPtr.Zero;
        }

        private IntPtr GetProcessWindow(Process p)
        {
            try
            {
                // Try MainWindowHandle first (fast)
                if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                {
                    var title = new System.Text.StringBuilder(256);
                    GetWindowText(p.MainWindowHandle, title, 256);
                    if (title.Length > 0) return p.MainWindowHandle;
                }

                // Iterate through all windows of this process (slow but thorough)
                var windows = GetAllProcessWindows((uint)p.Id);
                foreach (var handle in windows)
                {
                    if (IsWindowVisible(handle))
                    {
                        var title = new System.Text.StringBuilder(256);
                        GetWindowText(handle, title, 256);
                        // Filter out transparent/helper/overlay windows
                        if (title.Length > 0 && !title.ToString().Contains("Overlay", StringComparison.OrdinalIgnoreCase))
                        {
                            return handle;
                        }
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        private List<IntPtr> _tempWindows = new();

        private List<IntPtr> GetAllProcessWindows(uint processId)
        {
            _tempWindows = new List<IntPtr>();
            EnumWindows(_enumWindowsProc!, (IntPtr)processId);
            return _tempWindows;
        }

        private bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            uint targetPid = (uint)lParam;
            GetWindowThreadProcessId(hWnd, out uint windowProcessId);
            if (windowProcessId == targetPid)
            {
                _tempWindows.Add(hWnd);
            }
            return true;
        }

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        const uint WM_MOUSEWHEEL = 0x020A;
        const uint WM_MOUSEHWHEEL = 0x020E;
        const uint WM_MOUSEMOVE = 0x0200;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_RBUTTONDOWN = 0x0204;
        const uint WM_RBUTTONUP = 0x0205;
        const uint WM_MBUTTONDOWN = 0x0207;
        const uint WM_MBUTTONUP = 0x0208;
        const uint WM_KEYDOWN = 0x0100;
        const uint WM_KEYUP = 0x0101;
        const uint WM_CHAR = 0x0102;

        private void AttachWindow()
        {
            if (_appWindow == IntPtr.Zero || this.Handle == IntPtr.Zero) return;

            if (IsFloating)
            {
                // For floating mode, we don't SetParent.
                // We just set initial position and size.
                if (!KeepNativeControls)
                {
                    try
                    {
                        int style = GetWindowLong(_appWindow, GWL_STYLE);
                        style &= ~WS_BORDER;
                        style &= ~WS_CAPTION;
                        style &= ~WS_THICKFRAME;
                        style &= ~WS_SYSMENU;
                        style &= ~WS_MINIMIZEBOX;
                        style &= ~WS_MAXIMIZEBOX;
                        // WS_CHILD not allowed in top-level
                        SetWindowLong(_appWindow, GWL_STYLE, style);
                    }
                    catch { }
                }
                ForceResize();
                SetForegroundWindow(_appWindow);
                return;
            }

            // Set the parent to our control's HWND
            SetParent(_appWindow, this.Handle);

            if (!KeepNativeControls)
            {
                // Aggressively strip styles for a truly "embedded" look
                int style = GetWindowLong(_appWindow, GWL_STYLE);
                style &= ~WS_BORDER;
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style &= ~WS_SYSMENU;
                style &= ~WS_MINIMIZEBOX;
                style &= ~WS_MAXIMIZEBOX;
                style |= WS_CHILD;
                
                SetWindowLong(_appWindow, GWL_STYLE, style);

                // Strip extended styles (like being a tool window or having a taskbar entry)
                int exStyle = GetWindowLong(_appWindow, GWL_EXSTYLE);
                exStyle |= WS_EX_TOOLWINDOW;
                SetWindowLong(_appWindow, GWL_EXSTYLE, exStyle);
            }
            else
            {
                // Just make it a child, but keep the frame
                int style = GetWindowLong(_appWindow, GWL_STYLE);
                style |= WS_CHILD;
                // Ensure WS_POPUP is removed if it exists, as it conflicts with WS_CHILD
                style &= ~WS_POPUP;
                SetWindowLong(_appWindow, GWL_STYLE, style);
            }

            // Force initial focus and activation to prevent GUI locking
            SetForegroundWindow(_appWindow);
            SetFocus(_appWindow);

            // Force initial resize and positioning
            ForceResize();
        }

        public void ForceResize()
        {
            if (_appWindow == IntPtr.Zero) return;

            if (IsFloating)
            {
                // Get screen coordinates of this control
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget == null) return;

                var transform = source.CompositionTarget.TransformToDevice;
                Point locationFromScreen = this.PointToScreen(new Point(0, 0));
                
                // Use the actual size of this control in SONA
                int width = (int)(this.ActualWidth * transform.M11);
                int height = (int)(this.ActualHeight * transform.M22);

                if (width <= 0) width = 1270; // Fallbacks if not yet rendered
                if (height <= 0) height = 720;

                if (!IsSticky && _hasResizedOnce) return;

                int x = (int)(locationFromScreen.X + FloatingOffsetX * transform.M11);
                int y = (int)(locationFromScreen.Y + FloatingOffsetY * transform.M22);
                int w = (int)((FloatingWidth > 0 ? FloatingWidth : this.ActualWidth) * transform.M11);
                int h = (int)((FloatingHeight > 0 ? FloatingHeight : this.ActualHeight) * transform.M22);
                w = Math.Max(1, w);
                h = Math.Max(1, h);
                MoveWindow(_appWindow, x, y, w, h, true);
                _hasResizedOnce = true;
            }
            else
            {
                OnWindowPositionChanged(new Rect(0, 0, ActualWidth, ActualHeight));
            }
        }

        private IntPtr _hwndHost = IntPtr.Zero;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hwndHost = CreateWindowEx(0, "static", "",
                                      WS_CHILD | WS_VISIBLE,
                                      0, 0,
                                      (int)ActualWidth, (int)ActualHeight,
                                      hwndParent.Handle,
                                      IntPtr.Zero,
                                      IntPtr.Zero,
                                      0);

            if (_appWindow != IntPtr.Zero)
            {
                AttachWindow();
            }

            return new HandleRef(this, _hwndHost);
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            if (_appWindow != IntPtr.Zero && !IsFloating)
            {
                MoveWindow(_appWindow, 0, 0, (int)rcBoundingBox.Width, (int)rcBoundingBox.Height, true);
            }
            base.OnWindowPositionChanged(rcBoundingBox);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ForceResize();
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch { }
        }

        protected override bool TabIntoCore(TraversalRequest request)
        {
            if (_appWindow != IntPtr.Zero)
            {
                FocusChildWindow();
                return true;
            }
            return base.TabIntoCore(request);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            if (_appWindow != IntPtr.Zero)
            {
                FocusChildWindow();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (_appWindow != IntPtr.Zero)
            {
                FocusChildWindow();
                // Capture mouse to ensure subsequent messages route through us
                SetCapture(this.Handle);
                ForwardMouseButton(e, true);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            try { ReleaseCapture(); } catch { }
            if (_appWindow != IntPtr.Zero)
            {
                ForwardMouseButton(e, false);
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            if (_appWindow == IntPtr.Zero) return;
            var p = e.GetPosition(this);
            var screen = PointToScreen(p);
            int x = (int)screen.X;
            int y = (int)screen.Y;
            int lParam = (y << 16) | (x & 0xFFFF);
            int wParam = GetMouseKeyStateFlags();
            SendMessage(GetInputTarget(), WM_MOUSEMOVE, (IntPtr)wParam, (IntPtr)lParam);
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            if (_appWindow == IntPtr.Zero) return;
            var p = e.GetPosition(this);
            var screen = PointToScreen(p);
            int x = (int)screen.X;
            int y = (int)screen.Y;
            int lParam = (y << 16) | (x & 0xFFFF);
            int keyFlags = GetMouseKeyStateFlags() & 0xFFFF;
            int wParam = keyFlags | ((short)e.Delta << 16);
            SendMessage(GetInputTarget(), WM_MOUSEWHEEL, (IntPtr)wParam, (IntPtr)lParam);
            e.Handled = true;
        }

        private void ForwardMouseButton(MouseButtonEventArgs e, bool down)
        {
            var p = e.GetPosition(this);
            var screen = PointToScreen(p);
            int x = (int)screen.X;
            int y = (int)screen.Y;
            int lParam = (y << 16) | (x & 0xFFFF);
            int wParam = GetMouseKeyStateFlags();
            uint msg = 0;
            if (e.ChangedButton == MouseButton.Left) msg = down ? WM_LBUTTONDOWN : WM_LBUTTONUP;
            else if (e.ChangedButton == MouseButton.Right) msg = down ? WM_RBUTTONDOWN : WM_RBUTTONUP;
            else if (e.ChangedButton == MouseButton.Middle) msg = down ? WM_MBUTTONDOWN : WM_MBUTTONUP;
            if (msg != 0)
            {
                SendMessage(GetInputTarget(), msg, (IntPtr)wParam, (IntPtr)lParam);
            }
        }

        private int GetMouseKeyStateFlags()
        {
            int flags = 0;
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) flags |= 0x0008; // MK_CONTROL
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) flags |= 0x0004; // MK_SHIFT
            if (Mouse.LeftButton == MouseButtonState.Pressed) flags |= 0x0001; // MK_LBUTTON
            if (Mouse.RightButton == MouseButtonState.Pressed) flags |= 0x0002; // MK_RBUTTON
            if (Mouse.MiddleButton == MouseButtonState.Pressed) flags |= 0x0010; // MK_MBUTTON
            return flags;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (_appWindow == IntPtr.Zero) return;
            int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
            SendMessage(GetInputTarget(), WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            e.Handled = true;
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            if (_appWindow == IntPtr.Zero) return;
            int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
            SendMessage(GetInputTarget(), WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
            e.Handled = true;
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);
            if (_appWindow == IntPtr.Zero) return;
            foreach (var ch in e.Text)
            {
                SendMessage(GetInputTarget(), WM_CHAR, (IntPtr)ch, IntPtr.Zero);
            }
            e.Handled = true;
        }

        protected override bool TranslateAcceleratorCore(ref MSG msg, ModifierKeys modifiers)
        {
            if (_appWindow != IntPtr.Zero)
            {
                SendMessage(GetInputTarget(), (uint)msg.message, msg.wParam, msg.lParam);
                return true;
            }
            return base.TranslateAcceleratorCore(ref msg, modifiers);
        }

        private IntPtr GetInputTarget()
        {
            // Attempt to find Chromium render host child (Electron)
            try
            {
                var candidates = new List<IntPtr>();
                EnumChildWindows(_appWindow, (h, l) =>
                {
                    candidates.Add(h);
                    return true;
                }, IntPtr.Zero);

                // Prefer Electron/Chromium render widgets or browser windows
                foreach (var h in candidates)
                {
                    var cls = new System.Text.StringBuilder(256);
                    GetClassName(h, cls, cls.Capacity);
                    var name = cls.ToString();
                    if (name.Contains("Chrome_RenderWidgetHostHWND") ||
                        name.Contains("Chrome_WidgetWin") ||
                        name.Contains("CefBrowserWindow") ||
                        name.Contains("Intermediate D3D Window"))
                    {
                        return h;
                    }
                }

                // Fallback to the deepest child (last)
                if (candidates.Count > 0) return candidates[candidates.Count - 1];
                return _appWindow;
            }
            catch { return _appWindow; }
        }

        private void FocusChildWindow()
        {
            try
            {
                uint thisThread = GetCurrentThreadId();
                uint targetThread = GetWindowThreadProcessId(_appWindow, out _);
                AttachThreadInput(thisThread, targetThread, true);
                SetForegroundWindow(_appWindow);
                SetFocus(_appWindow);
                SetActiveWindow(_appWindow);
                AttachThreadInput(thisThread, targetThread, false);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                KillExistingProcess();
            }
        }
    }
}
