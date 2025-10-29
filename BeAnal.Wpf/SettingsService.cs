using System;
using System.IO;
using System.Net;
using System.Text.Json;

namespace BeAnal.Wpf
{
    public static class SettingsService
    {
        private static readonly string _appDataFolder =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static readonly string _settingsFolder = Path.Combine(_appDataFolder, "BeAnal");
        private static readonly string _settingsFile = Path.Combine(_settingsFolder, "settings.json");

        public static void SaveSettings(Settings settings)
        {
            //Ensure that the directory exists!
            Directory.CreateDirectory(_settingsFolder);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFile, jsonString);
        }

        public static Settings LoadSettings()
        {
            if (!File.Exists(_settingsFile))
            {
                // If the file doesn't exist, return a new Settings object with default settings
                return new Settings();
            }

            try
            {
                string jsonString = File.ReadAllText(_settingsFile);
                var settings = JsonSerializer.Deserialize<Settings>(jsonString);
                // if the deserialization returns null, return a new default settings
                return settings ?? new Settings();
            }
            catch (Exception)
            {
                // iif there is an error reading or parsing the file, return defaults
                return new Settings();
            }

        }
    }
}