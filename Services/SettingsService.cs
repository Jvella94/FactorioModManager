using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace FactorioModManager.Services
{
    public class Settings
    {
        public string? FactorioModsPath { get; set; }
        public string? ApiKey { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
        public bool KeepOldModFiles { get; set; } = false; // ADDED - default to deleting old files
    }


    public class SettingsService
    {
        private readonly string _settingsPath;
        // FIXED: Made readonly (IDE0044)
        private readonly Settings _settings;
        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public SettingsService()
        {
            var modsDir = ModPathHelper.GetModsDirectory();
            _settingsPath = Path.Combine(modsDir, "mod-manager-settings.json");
            _settings = LoadSettings();
        }

        private Settings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                return new Settings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            catch (Exception ex)
            {
                LogService.LogDebug($"Error loading settings: {ex.Message}");
                return new Settings();
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

        public string? GetApiKey() => _settings.ApiKey;

        public void SetApiKey(string? apiKey)
        {
            _settings.ApiKey = apiKey;
            SaveSettings();
        }

        public DateTime? GetLastUpdateCheck() => _settings.LastUpdateCheck;

        public void SetLastUpdateCheck(DateTime? date)
        {
            _settings.LastUpdateCheck = date;
            SaveSettings();
        }

        public bool GetKeepOldModFiles()
        {
            return _settings.KeepOldModFiles;
        }

        public void SetKeepOldModFiles(bool keepOldFiles)
        {
            _settings.KeepOldModFiles = keepOldFiles;
            SaveSettings();
        }

        public void SetModsPath(string path)
        {
            _settings.FactorioModsPath = path;
            SaveSettings();
        }

        public string? GetModsPath()
        {
            return _settings.FactorioModsPath;
        }
    }
}
