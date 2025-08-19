using System;
using System.IO;
using System.Text.Json;

namespace Chess_Project.Configuration
{
    public static class PreferencesManager
    {
        private static readonly string _filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "preferences.json");

        public static Preferences Load()
        {
            if (!File.Exists(_filePath))
                return new Preferences();

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        }

        public static void Save(Preferences preferences)
        {
            string json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
