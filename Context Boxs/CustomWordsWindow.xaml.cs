using EasyWords.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace EasyWords
{
    public partial class CustomWordsWindow : Window
    {
        private static readonly string CustomJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "custom_words.json");

        public class CustomWordItem
        {
            public string Word { get; set; } = "";
            public string Lang { get; set; } = "ENG";
        }

        private List<CustomWordItem> _customWords = new();

        public CustomWordsWindow()
        {
            InitializeComponent();
            LoadCustomWords();
            LoadFavWords();
        }

        #region Custom Words Logic
        private void LoadCustomWords()
        {
            try
            {
                if (File.Exists(CustomJsonPath))
                {
                    string json = File.ReadAllText(CustomJsonPath);
                    _customWords = JsonSerializer.Deserialize<List<CustomWordItem>>(json) ?? new List<CustomWordItem>();
                }
            }
            catch { _customWords = new List<CustomWordItem>(); }

            CustomWordsList.ItemsSource = null;
            CustomWordsList.ItemsSource = _customWords;
        }

        public static void AddCustomWord(string word, string lang)
        {
            var words = new List<CustomWordItem>();
            try
            {
                if (File.Exists(CustomJsonPath))
                {
                    string json = File.ReadAllText(CustomJsonPath);
                    words = JsonSerializer.Deserialize<List<CustomWordItem>>(json) ?? new List<CustomWordItem>();
                }
            }
            catch { }

            if (!words.Any(w => w.Word.Equals(word, StringComparison.OrdinalIgnoreCase) && w.Lang == lang))
            {
                words.Add(new CustomWordItem { Word = word, Lang = lang });
                File.WriteAllText(CustomJsonPath, JsonSerializer.Serialize(words, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private void SaveCustomAndSync()
        {
            File.WriteAllText(CustomJsonPath, JsonSerializer.Serialize(_customWords, new JsonSerializerOptions { WriteIndented = true }));
            LoadCustomWords();

            MainWindow.Instance?.Dispatcher.Invoke(() =>
            {
                MainWindow.Instance.LoadDictionary();
            });
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is CustomWordItem item)
            {
                string oldWord = item.Word;
                string newWord = Microsoft.VisualBasic.Interaction.InputBox("Nhập từ mới thay thế:", "Sửa từ custom", oldWord);

                newWord = newWord.Trim().ToLower();
                if (!string.IsNullOrWhiteSpace(newWord) && newWord != oldWord)
                {
                    string fileName = item.Lang == "ENG" ? "eng.txt" : "vie.txt";
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                    if (File.Exists(filePath))
                    {
                        var lines = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
                        int index = lines.FindIndex(x => x.Trim().Equals(oldWord, StringComparison.OrdinalIgnoreCase));
                        if (index != -1) lines[index] = newWord;
                        else lines.Add(newWord);
                        File.WriteAllLines(filePath, lines, Encoding.UTF8);
                    }

                    item.Word = newWord;
                    SaveCustomAndSync();
                }
            }
        }

        private void RemoveCustomButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is CustomWordItem item)
            {
                var result = System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa từ '{item.Word}' khỏi từ điển?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    string fileName = item.Lang == "ENG" ? "eng.txt" : "vie.txt";
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                    if (File.Exists(filePath))
                    {
                        var lines = File.ReadAllLines(filePath, Encoding.UTF8)
                                        .Where(x => !x.Trim().Equals(item.Word, StringComparison.OrdinalIgnoreCase))
                                        .ToArray();
                        File.WriteAllLines(filePath, lines, Encoding.UTF8);
                    }

                    _customWords.Remove(item);
                    SaveCustomAndSync();
                }
            }
        }
        #endregion

        #region Favorites Logic
        private void LoadFavWords()
        {
            FavWordsList.ItemsSource = null;
            FavWordsList.ItemsSource = FavWords.GetFavList();
        }

        private void UnfavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is string word)
            {
                FavWords.ToggleFav(word);
                LoadFavWords();

                MainWindow.Instance?.Dispatcher.Invoke(() =>
                {
                    MainWindow.Instance.LoadDictionary();
                });
            }
        }
        #endregion

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}