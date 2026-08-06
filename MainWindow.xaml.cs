using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace EasyWords
{
    public partial class MainWindow : Window
    {
        private string _engPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eng.txt");
        private List<string> _activeWordList = new List<string>();
        private string _typedBuffer = "";

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc? _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.Visibility = Visibility.Collapsed;

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDictionary();
            InitSystemTray();

            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private void InitSystemTray()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();

                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
                else
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon.Visible = true;
                _notifyIcon.Text = "EasyWords";

                _notifyIcon.DoubleClick += (s, e) =>
                {
                    this.Visibility = Visibility.Visible;
                };

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Show", null, (s, e) => this.Visibility = Visibility.Visible);
                contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SystemTray Error: " + ex.Message);
                if (_notifyIcon != null) _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            ResetBuffer();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication();
        }

        private void ExitApplication()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            UnhookWindowsHookEx(_hookID);
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            ExitApplication();
        }

        private void LoadDictionary()
        {
            if (File.Exists(_engPath))
            {
                _activeWordList = File.ReadAllLines(_engPath)
                                      .Select(w => w.Trim())
                                      .Where(w => !string.IsNullOrWhiteSpace(w))
                                      .ToList();
            }
            else
            {
                _activeWordList = new List<string>();
            }
        }

        private void ResetBuffer()
        {
            _typedBuffer = "";
            Dispatcher.Invoke(() =>
            {
                if (SuggestionList != null) SuggestionList.ItemsSource = null;
                this.Visibility = Visibility.Collapsed;
            });
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                System.Windows.Forms.Keys key = (System.Windows.Forms.Keys)vkCode;

                if (key == System.Windows.Forms.Keys.Space ||
                    key == System.Windows.Forms.Keys.Return ||
                    key == System.Windows.Forms.Keys.Tab ||
                    key == System.Windows.Forms.Keys.Escape)
                {
                    ResetBuffer();
                }
                else if (key == System.Windows.Forms.Keys.Back)
                {
                    if (_typedBuffer.Length > 0)
                    {
                        _typedBuffer = _typedBuffer.Substring(0, _typedBuffer.Length - 1);
                        Dispatcher.Invoke(() => FilterAndShowSuggestions(_typedBuffer));
                    }
                    else
                    {
                        ResetBuffer();
                    }
                }
                else
                {
                    char c = GetCharFromVkCode((uint)vkCode);
                    if (char.IsLetterOrDigit(c))
                    {
                        _typedBuffer += c;
                        Dispatcher.Invoke(() => FilterAndShowSuggestions(_typedBuffer));
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void FilterAndShowSuggestions(string query)
        {
            string lastWord = query.Split(' ').LastOrDefault() ?? "";

            if (string.IsNullOrEmpty(lastWord) || lastWord.Length < 2)
            {
                this.Visibility = Visibility.Collapsed;
                return;
            }

            var matches = _activeWordList
                .Where(w => w.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            if (matches.Count > 0)
            {
                if (SuggestionList != null) SuggestionList.ItemsSource = matches;

                GetCursorPos(out POINT point);
                this.Left = point.X + 15;
                this.Top = point.Y + 15;
                this.Visibility = Visibility.Visible;
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
            }
        }

        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SuggestionList.SelectedItem == null) return;

            string selectedWord = SuggestionList.SelectedItem.ToString() ?? "";
            string lastWord = _typedBuffer.Split(' ').LastOrDefault() ?? "";
            int backspaceCount = lastWord.Length;

            this.Visibility = Visibility.Collapsed;
            _typedBuffer = "";
            SuggestionList.SelectedItem = null;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                for (int i = 0; i < backspaceCount; i++)
                {
                    System.Windows.Forms.SendKeys.SendWait("{BACKSPACE}");
                }

                Thread.Sleep(30);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.Clipboard.SetText(selectedWord);
                });

                System.Windows.Forms.SendKeys.SendWait("^v");
            });
        }

        private char GetCharFromVkCode(uint vkCode)
        {
            byte[] keyState = new byte[256];
            GetKeyboardState(keyState);

            uint scanCode = MapVirtualKey(vkCode, 0);
            StringBuilder sb = new StringBuilder(2);

            int result = ToAscii(vkCode, scanCode, keyState, sb, 0);
            if (result == 1)
            {
                return sb[0];
            }
            return '\0';
        }

        // Win32 imports
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule?.ModuleName != null)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, StringBuilder lpChar, uint uFlags);
    }
}
