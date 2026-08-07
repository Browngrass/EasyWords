using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace EasyWords
{
    public static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "EasyWords";

        private static bool _isAutoCompleteEnabled = false;
        private static bool _isAutoSelectFirstEnabled = false;
        private static bool _runAtStartup = false;
        public static bool IsAutoHideEnabled { get; set; } = true;

        static AppSettings()
        {
            Load();
        }

        public static bool IsAutoCompleteEnabled
        {
            get => _isAutoCompleteEnabled;
            set { if (_isAutoCompleteEnabled != value) { _isAutoCompleteEnabled = value; Save(); } }
        }

        public static bool IsAutoSelectFirstEnabled
        {
            get => _isAutoSelectFirstEnabled;
            set { if (_isAutoSelectFirstEnabled != value) { _isAutoSelectFirstEnabled = value; Save(); } }
        }

        public static bool RunAtStartup
        {
            get => _runAtStartup;
            set
            {
                if (_runAtStartup != value)
                {
                    _runAtStartup = value;
                    SetStartupRegistry(value);
                    Save();
                }
            }
        }

        private static void SetStartupRegistry(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi ghi Registry Startup: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                var data = new SettingsData
                {
                    IsAutoCompleteEnabled = _isAutoCompleteEnabled,
                    IsAutoSelectFirstEnabled = _isAutoSelectFirstEnabled,
                    RunAtStartup = _runAtStartup
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        _isAutoCompleteEnabled = data.IsAutoCompleteEnabled;
                        _isAutoSelectFirstEnabled = data.IsAutoSelectFirstEnabled;
                        _runAtStartup = data.RunAtStartup;
                    }
                }
            }
            catch { }
        }

        private class SettingsData
        {
            public bool IsAutoCompleteEnabled { get; set; } = false;
            public bool IsAutoSelectFirstEnabled { get; set; } = false;
            public bool RunAtStartup { get; set; } = false;
        }
    }
}