using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace EasyWords
{
    public class PopupManager
    {
        private readonly Window _window;
        private readonly ContextBoxRules _boxRules;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        public PopupManager(Window window, Action onHideAction)
        {
            _window = window;
            // Đổi thời gian mặc định thành 5.0 giây
            _boxRules = new ContextBoxRules(_window, onHideAction, autoHideSeconds: 5.0);
        }

        public void Show(int x, int y)
        {
            _window.Dispatcher.Invoke(() =>
            {
                var source = PresentationSource.FromVisual(_window);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                _window.Left = x / dpiX;
                _window.Top = y / dpiY;

                if (_window.Visibility != Visibility.Visible)
                    _window.Visibility = Visibility.Visible;

                // Nếu người dùng bật Auto Hide thì mới chạy Timer 5s
                if (AppSettings.IsAutoHideEnabled)
                {
                    _boxRules.StartTracking();
                }
            });
        }

        public void Hide()
        {
            _boxRules.Hide();
        }

        public void ResetTimer()
        {
            if (AppSettings.IsAutoHideEnabled)
            {
                _boxRules.ResetTimer();
            }
        }
    }
}