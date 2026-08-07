using System;
using System.IO;
using System.Text;
using System.Windows;

namespace EasyWords
{
    public partial class AddWordWindow : Window
    {
        public AddWordWindow()
        {
            InitializeComponent();
            WordInput.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newWord = WordInput.Text.Trim().ToLower();
            string lang = LangCombo.SelectedIndex == 0 ? "ENG" : "VIE";
            CustomWordsWindow.AddCustomWord(newWord, lang);

            if (string.IsNullOrWhiteSpace(newWord))
            {
                System.Windows.MessageBox.Show("Vui lòng nhập từ cần thêm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Lấy file theo Lựa chọn ComboBox (0: ENG, 1: VIE)
            string fileName = LangCombo.SelectedIndex == 0 ? "eng.txt" : "vie.txt";
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            try
            {
                // Ghi thêm từ mới vào cuối file
                File.AppendAllText(filePath, newWord + Environment.NewLine, Encoding.UTF8);

                System.Windows.MessageBox.Show($"Đã thêm '{newWord}' vào {fileName}!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

                // Nạp lại từ điển trong MainWindow nếu đang chạy
                MainWindow.Instance?.Dispatcher.Invoke(() =>
                {
                    MainWindow.Instance.LoadDictionary();
                });

                this.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Lỗi khi lưu từ mới: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}