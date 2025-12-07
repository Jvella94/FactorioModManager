using System;
using System.IO;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class AppSettings
    {
        public string? ModPortalApiKey { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        private AppSettings _settings;

        public SettingsService()
        {
            var modsDir = ModPathHelper.GetModsDirectory();
            _settingsPath = Path.Combine(modsDir, "mod-manager-settings.json");
            _settings = LoadSettings();
        }

        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public string? GetApiKey() => _settings.ModPortalApiKey;

        public void SetApiKey(string? apiKey)
        {
            _settings.ModPortalApiKey = apiKey;
            SaveSettings();
        }

        public DateTime? GetLastUpdateCheck() => _settings.LastUpdateCheck;

        public void SetLastUpdateCheck(DateTime? date)
        {
            _settings.LastUpdateCheck = date;
            SaveSettings();
        }
    }
}
