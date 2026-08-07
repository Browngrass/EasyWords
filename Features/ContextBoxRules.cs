using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace EasyWords
{
    public class ContextBoxRules
    {
        private readonly Window _window;
        private readonly DispatcherTimer _autoHideTimer;
        private readonly Action? _onHideAction;
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static LowLevelMouseProc? _mouseProc;
        private static ContextBoxRules? _activeInstance;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        public ContextBoxRules(Window window, Action? onHideAction = null, double autoHideSeconds = 3.0)
        {
            _window = window;
            _onHideAction = onHideAction;

            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(autoHideSeconds)
            };
            _autoHideTimer.Tick += (s, e) => Hide();
        }

        public void StartTracking()
        {
            _activeInstance = this;

            _autoHideTimer.Stop();
            _autoHideTimer.Start();

            InstallMouseHook();
        }

        public void ResetTimer()
        {
            if (_window.Visibility == Visibility.Visible)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }
        }

        public void Hide()
        {
            _window.Dispatcher.Invoke(() =>
            {
                _autoHideTimer.Stop();
                UninstallMouseHook();

                if (_window.Visibility != Visibility.Collapsed)
                {
                    _window.Visibility = Visibility.Collapsed;
                    _onHideAction?.Invoke();
                }
            });
        }

        #region Win32 Mouse Hook (Click Outside)
        private static void InstallMouseHook()
        {
            if (_mouseHookID != IntPtr.Zero) return;

            _mouseProc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule?.ModuleName != null)
                {
                    _mouseHookID = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        private static void UninstallMouseHook()
        {
            if (_mouseHookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookID);
                _mouseHookID = IntPtr.Zero;
                _mouseProc = null;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _activeInstance != null && _activeInstance._window.Visibility == Visibility.Visible)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    // Chỉ định rõ System.Windows.Point để tránh xung đột với System.Drawing.Point
                    System.Windows.Point clickPoint = new System.Windows.Point(hookStruct.pt.x, hookStruct.pt.y);

                    _activeInstance._window.Dispatcher.Invoke(() =>
                    {
                        var winLeft = _activeInstance._window.Left;
                        var winTop = _activeInstance._window.Top;
                        var winWidth = _activeInstance._window.ActualWidth;
                        var winHeight = _activeInstance._window.ActualHeight;

                        var source = PresentationSource.FromVisual(_activeInstance._window);
                        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                        Rect winRect = new Rect(winLeft * dpiX, winTop * dpiY, winWidth * dpiX, winHeight * dpiY);

                        if (!winRect.Contains(clickPoint))
                        {
                            _activeInstance.Hide();
                        }
                    });
                }
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}