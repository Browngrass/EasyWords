using EasyWords.Features;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EasyWords
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        public static bool IsInjecting = false;

        private string _engPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eng.txt");
        private string _viePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vie.txt");
        private string _currentLang = "ENG";

        private string[] _activeWordList = Array.Empty<string>();
        private string _typedBuffer = "";
        private POINT _lastValidCaretPoint = new POINT { X = -1, Y = -1 };

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        private const byte VK_BACK = 0x08;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private PopupManager _popupManager;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        // --- WIN32 HOTKEY CONSTANTS ---
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_H = 0x48; // Phím 'H'

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                SettingsMenu.OpenMenu(btn);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            _popupManager = new PopupManager(this, () => ResetBuffer());

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }

            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.WindowStyle = WindowStyle.None;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowActivated = false;
            this.Visibility = Visibility.Collapsed;

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDictionary();
            InitSystemTray();

            _hookID = SetHook(_proc);
            GC.KeepAlive(_proc);

            // --- Đăng ký Global Hotkey Ctrl + Shift + H ---
            WindowInteropHelper helper = new WindowInteropHelper(this);
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(HwndHook);

            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_H);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleWindowVisibility();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void ToggleWindowVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (this.Visibility == Visibility.Visible)
                {
                    this.Visibility = Visibility.Collapsed;
                }
                else
                {
                    this.Visibility = Visibility.Visible;
                    this.Activate();
                }
            });
        }

        public void FilterAndShowSuggestions(string query)
        {
            // 1. Nếu tắt trong Settings thì ngắt
            if (!AppSettings.IsAutoCompleteEnabled)
            {
                _popupManager.Hide();
                if (KeybindHelpPanel != null) KeybindHelpPanel.Visibility = Visibility.Visible;
                return;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                _popupManager.Hide();
                // Hiện lại bảng keybind khi người dùng xóa hết chữ
                if (KeybindHelpPanel != null) KeybindHelpPanel.Visibility = Visibility.Visible;
                return;
            }

            string lastWord = query.Split(' ').LastOrDefault()?.ToLower() ?? "";

            if (string.IsNullOrEmpty(lastWord))
            {
                _popupManager.Hide();
                if (KeybindHelpPanel != null) KeybindHelpPanel.Visibility = Visibility.Visible;
                return;
            }

            if (_activeWordList == null || _activeWordList.Length == 0)
            {
                _popupManager.Hide();
                if (KeybindHelpPanel != null) KeybindHelpPanel.Visibility = Visibility.Visible;
                return;
            }

            var matches = _activeWordList
                .Where(w => !string.IsNullOrEmpty(w) && w.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(w => new WordItem
                {
                    Word = w,
                    IsFav = FavWords.IsFav(w)
                })
                .ToList();

            if (matches.Count > 0)
            {
                // ẨN BẢNG KEYBIND KHI CÓ GỢI Ý SHOW RA
                if (KeybindHelpPanel != null)
                {
                    KeybindHelpPanel.Visibility = Visibility.Collapsed;
                }

                SuggestionList.ItemsSource = matches;
                SuggestionList.SelectedIndex = AppSettings.IsAutoSelectFirstEnabled ? 0 : -1;

                if (GetCaretPositionModern(out POINT caretPoint) || GetCaretPositionWin32(out caretPoint))
                {
                    _lastValidCaretPoint = caretPoint;
                    _popupManager.Show(caretPoint.X, caretPoint.Y);
                }
                else if (_lastValidCaretPoint.X != -1 && _lastValidCaretPoint.Y != -1)
                {
                    _popupManager.Show(_lastValidCaretPoint.X, _lastValidCaretPoint.Y);
                }
                else
                {
                    var cursorPoint = System.Windows.Forms.Cursor.Position;
                    _popupManager.Show(cursorPoint.X, cursorPoint.Y + 20);
                }
            }
            else
            {
                // Không tìm thấy từ khớp -> Ẩn popup và hiện lại keybind
                _popupManager.Hide();
                if (KeybindHelpPanel != null) KeybindHelpPanel.Visibility = Visibility.Visible;
            }
        }

        private bool GetCaretPositionModern(out POINT point)
        {
            point = new POINT { X = 0, Y = 0 };
            try
            {
                AutomationElement focusedElement = AutomationElement.FocusedElement;
                if (focusedElement == null) return false;

                if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object patternObj))
                {
                    TextPattern textPattern = (TextPattern)patternObj;
                    TextPatternRange[] selection = textPattern.GetSelection();

                    if (selection != null && selection.Length > 0)
                    {
                        Rect[] boundingRects = selection[0].GetBoundingRectangles();
                        if (boundingRects != null && boundingRects.Length > 0)
                        {
                            Rect rect = boundingRects[0];
                            point.X = (int)rect.Left;
                            point.Y = (int)rect.Bottom;
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private bool GetCaretPositionWin32(out POINT point)
        {
            point = new POINT { X = 0, Y = 0 };

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            uint threadId = GetWindowThreadProcessId(hwnd, out _);

            GUITHREADINFO guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);

            if (GetGUIThreadInfo(threadId, ref guiInfo))
            {
                if (guiInfo.hwndFocus != IntPtr.Zero && guiInfo.rcCaret.Bottom != 0)
                {
                    point.X = guiInfo.rcCaret.Left;
                    point.Y = guiInfo.rcCaret.Bottom;

                    ClientToScreen(guiInfo.hwndFocus, ref point);
                    return true;
                }
            }

            return false;
        }

        private void SuggestionList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
        }

        private void InitSystemTray()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();

                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
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

                _notifyIcon.DoubleClick += (s, e) => ToggleWindowVisibility();

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Hiện/Ẩn (Ctrl+Shift+H)", null, (s, e) => ToggleWindowVisibility());
                contextMenu.Items.Add("Thoát", null, (s, e) => ExitApplication());
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
            this.Visibility = Visibility.Collapsed;
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
            WindowInteropHelper helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            ExitApplication();
        }

        public void LoadDictionary()
        {
            string selectedPath = (_currentLang == "VIE") ? _viePath : _engPath;

            if (File.Exists(selectedPath))
            {
                try
                {
                    _activeWordList = File.ReadAllLines(selectedPath, Encoding.UTF8)
                                          .Select(w => w.Trim().ToLower())
                                          .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length >= 2 && w.Length <= 30)
                                          .Distinct()
                                          .ToArray();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi đọc từ điển: " + ex.Message);
                    _activeWordList = Array.Empty<string>();
                }
            }
            else
            {
                _activeWordList = Array.Empty<string>();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void LangBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentLang == "ENG")
            {
                _currentLang = "VIE";
                LangBtn.Content = "VIE";
                LangBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 150, 80));
            }
            else
            {
                _currentLang = "ENG";
                LangBtn.Content = "ENG";
                LangBtn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176));
            }

            LoadDictionary();

            if (!string.IsNullOrEmpty(_typedBuffer))
            {
                FilterAndShowSuggestions(_typedBuffer);
            }
        }

        private void ResetBuffer()
        {
            _typedBuffer = "";
            Dispatcher.Invoke(() =>
            {
                if (SuggestionList != null) SuggestionList.ItemsSource = null;

                if (KeybindHelpPanel != null)
                {
                    KeybindHelpPanel.Visibility = Visibility.Visible;
                }

                this.Visibility = Visibility.Collapsed;
            });
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                uint vkCode = hookStruct.vkCode;
                bool isInjected = (hookStruct.flags & 0x10) != 0;

                if ((isInjected && MainWindow.IsInjecting) || vkCode == 0)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    var mainWin = MainWindow.Instance;
                    if (mainWin != null && mainWin.Visibility == Visibility.Visible)
                    {
                        mainWin.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainWin._popupManager.Hide();
                        }));
                    }
                }

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    var mainWin = MainWindow.Instance;
                    if (mainWin != null)
                    {
                        if (vkCode == 0x09 || vkCode == 0x26 || vkCode == 0x28)
                        {
                            mainWin._popupManager.ResetTimer();
                        }

                        if (KeybindManager.HandleHookKeys((int)vkCode, mainWin.SuggestionList, mainWin, mainWin.ApplySelectedWord))
                        {
                            return (IntPtr)1;
                        }

                        char c = '\0';

                        if (vkCode == 0xE7)
                        {
                            c = (char)hookStruct.scanCode;
                        }
                        else if (vkCode >= 'A' && vkCode <= 'Z')
                        {
                            c = char.ToLower((char)vkCode);
                        }
                        else if (vkCode >= '0' && vkCode <= '9')
                        {
                            c = (char)vkCode;
                        }

                        if (c != '\0' && !char.IsControl(c))
                        {
                            mainWin._typedBuffer += c;

                            mainWin.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                mainWin.FilterAndShowSuggestions(mainWin._typedBuffer);
                            }));
                        }
                        else if (vkCode == 0x08)
                        {
                            if (mainWin._typedBuffer.Length > 0)
                            {
                                mainWin._typedBuffer = mainWin._typedBuffer.Substring(0, mainWin._typedBuffer.Length - 1);
                                mainWin.Dispatcher.BeginInvoke(new Action(() => mainWin.FilterAndShowSuggestions(mainWin._typedBuffer)));
                            }
                        }
                        else if (vkCode == 0x20 || vkCode == 0x0D)
                        {
                            mainWin._typedBuffer = "";
                            mainWin.Dispatcher.BeginInvoke(new Action(() => mainWin._popupManager.Hide()));
                        }
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void ApplySelectedWord()
        {
            if (SuggestionList.SelectedItem is not WordItem selectedItem) return;

            string selectedWord = selectedItem.Word;
            UserHistory.RecordWordUsage(selectedWord);

            string lastWord = _typedBuffer.Split(' ').LastOrDefault() ?? "";
            int backspaceCount = lastWord.Length;

            _popupManager.Hide();
            _typedBuffer = "";
            SuggestionList.SelectedItem = null;

            if (backspaceCount <= 0) return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                MainWindow.IsInjecting = true;
                try
                {
                    for (int i = 0; i < backspaceCount; i++)
                    {
                        keybd_event(VK_BACK, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_BACK, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }

                    Thread.Sleep(15);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            try
                            {
                                System.Windows.Clipboard.SetText(selectedWord);
                                break;
                            }
                            catch
                            {
                                Thread.Sleep(10);
                            }
                        }
                    });

                    System.Windows.Forms.SendKeys.SendWait("^v");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi ApplySelectedWord: " + ex.Message);
                }
                finally
                {
                    MainWindow.IsInjecting = false;
                }
            });
        }

        private void SuggestionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KeybindManager.IsNavigatingWithKeys) return;

            if (SuggestionList.SelectedItem != null && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                ApplySelectedWord();
            }
        }

        private void FavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string word)
            {
                FavWords.ToggleFav(word);
                FilterAndShowSuggestions(_typedBuffer);
                e.Handled = true;
            }
        }

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

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndFocusList;
            public RECT rcCaret;
        }

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        public static void TrimMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            }
        }
    }
}