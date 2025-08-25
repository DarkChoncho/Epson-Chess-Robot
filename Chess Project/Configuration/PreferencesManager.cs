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
            {
                // Ensure directory exists before creating empty file
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                var defaultPrefs = new Preferences();
                Save(defaultPrefs);  // Write defaults immediately
                return defaultPrefs;
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Preferences>(json) ?? new Preferences();
        }

        public static void Save(Preferences preferences)
        {
            // Ensure directory exists before writing
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            string json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
