using System;
using System.Collections.Generic;
using System.IO;

namespace EasyWords.Features
{
    public static class FavWords
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favs.txt");
        private static HashSet<string> _favs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static FavWords()
        {
            Load();
        }

        public static void Load()
        {
            _favs.Clear();
            if (File.Exists(FilePath))
            {
                try
                {
                    var lines = File.ReadAllLines(FilePath);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _favs.Add(line.Trim());
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi khi tải FavWords: {ex.Message}");
                }
            }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, _favs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi khi lưu FavWords: {ex.Message}");
            }
        }

        public static bool IsFav(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return false;
            return _favs.Contains(word.Trim());
        }

        public static void ToggleFav(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            word = word.Trim();

            if (_favs.Contains(word))
            {
                _favs.Remove(word);
            }
            else
            {
                _favs.Add(word);
            }

            Save();
        }
    }
}
