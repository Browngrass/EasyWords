using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyWords.Features
{
    public static class UserHistory
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
        public static Dictionary<string, int> UsageCounts { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        static UserHistory()
        {
            Load();
        }

        public static void RecordWordUsage(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            if (UsageCounts.ContainsKey(word))
                UsageCounts[word]++;
            else
                UsageCounts[word] = 1;

            Save();
        }

        public static int GetCount(string word)
        {
            return UsageCounts.TryGetValue(word, out int count) ? count : 0;
        }

        private static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(UsageCounts);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (data != null) UsageCounts = new Dictionary<string, int>(data, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
        }
    }
}
