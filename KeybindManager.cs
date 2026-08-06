using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace EasyWords.Features
{
    public static class KeybindManager
    {
        private const int VK_TAB = 0x09;
        private const int VK_RETURN = 0x0D; // Enter
        private const int VK_ESCAPE = 0x1B; // Esc
        private const int VK_UP = 0x26;     // Up Arrow
        private const int VK_DOWN = 0x28;   // Down Arrow

        public static bool IsNavigatingWithKeys = false;

        public static bool HandleHookKeys(int vkCode, System.Windows.Controls.ListBox suggestionList, Window window, Action applySelection)
        {
            bool isVisible = false;
            window.Dispatcher.Invoke(() => isVisible = window.Visibility == Visibility.Visible);
            if (!isVisible) return false;

            bool isShiftPressed = (GetKeyState(0x10) & 0x8000) != 0;

            // 1. Tab / Up down
            if (vkCode == VK_TAB || vkCode == VK_DOWN || vkCode == VK_UP)
            {
                window.Dispatcher.Invoke(() =>
                {
                    IsNavigatingWithKeys = true;
                    suggestionList.Focus();

                    int totalItems = suggestionList.Items.Count;
                    if (totalItems == 0) return;

                    bool moveUp = (vkCode == VK_TAB && isShiftPressed) || vkCode == VK_UP;

                    if (moveUp)
                    {
                        if (suggestionList.SelectedIndex <= 0)
                            suggestionList.SelectedIndex = totalItems - 1;
                        else
                            suggestionList.SelectedIndex--;
                    }
                    else
                    {
                        if (suggestionList.SelectedIndex < 0 || suggestionList.SelectedIndex >= totalItems - 1)
                            suggestionList.SelectedIndex = 0;
                        else
                            suggestionList.SelectedIndex++;
                    }

                    if (suggestionList.SelectedItem != null)
                    {
                        suggestionList.ScrollIntoView(suggestionList.SelectedItem);
                    }

                    IsNavigatingWithKeys = false;
                });
                return true;
            }

            // 2. ENTER
            if (vkCode == VK_RETURN)
            {
                bool handled = false;
                window.Dispatcher.Invoke(() =>
                {
                    if (suggestionList.SelectedIndex >= 0 && suggestionList.SelectedItem != null)
                    {
                        applySelection();
                        handled = true;
                    }
                });

                if (handled) return true;
            }

            // 3. ESCAPE
            if (vkCode == VK_ESCAPE)
            {
                window.Dispatcher.Invoke(() =>
                {
                    window.Visibility = Visibility.Collapsed;
                });
                return true;
            }

            return false;
        }

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}
