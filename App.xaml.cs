using System;
using System.IO;

namespace EasyWords
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            // Bắt lỗi trên UI Thread
            this.DispatcherUnhandledException += (s, args) =>
            {
                LogCrash(args.Exception);
                System.Windows.MessageBox.Show(
                    $"Lỗi UI Crash: {args.Exception.Message}\n\nXem chi tiết tại crash_log.txt",
                    "EasyWords Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                args.Handled = true;
            };

            // Bắt lỗi trên các Thread chạy ngầm (AppDomain)
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    LogCrash(ex);
                    System.Windows.MessageBox.Show(
                        $"Lỗi System Crash: {ex.Message}\n\nXem chi tiết tại crash_log.txt",
                        "EasyWords Fatal Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            };
        }

        private void LogCrash(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                string errorText = $"[{DateTime.Now}] LỖI CRASH:\n" +
                                   $"Message: {ex.Message}\n" +
                                   $"StackTrace:\n{ex.StackTrace}\n" +
                                   new string('-', 50) + "\n";

                File.AppendAllText(logPath, errorText);
            }
            catch { }
        }
    }
}