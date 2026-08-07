using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyWords.Features
{
    public static class FavWords
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "favs.json");
        private static HashSet<string> _favs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static FavWords()
        {
            Load();
        }

        public static List<string> GetFavList()
        {
            return _favs.ToList();
        }

        public static bool IsFav(string word)
        {
            return _favs.Contains(word);
        }

        public static void ToggleFav(string word)
        {
            if (_favs.Contains(word))
                _favs.Remove(word);
            else
                _favs.Add(word);
            Save();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var data = JsonSerializer.Deserialize<List<string>>(json);
                    if (data != null) _favs = new HashSet<string>(data, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_favs);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}