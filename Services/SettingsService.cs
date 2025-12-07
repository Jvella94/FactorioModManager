using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

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
        // FIXED: Made readonly (IDE0044)
        private readonly AppSettings _settings;
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

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
                LogService.LogDebug($"Error loading settings: {ex.Message}");
                return new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, SerializerOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Error saving settings: {ex.Message}");
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
