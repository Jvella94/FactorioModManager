using FactorioModManager.Services.Infrastructure;
using System;
using System.IO;
using System.Text.Json;

namespace FactorioModManager.Services.Settings
{
    /// <summary>
    /// Base class that centralizes loading/saving AppSettings and provides helpers
    /// to update settings with automatic persistence. Services that manage
    /// application settings can inherit from this to avoid duplicated code.
    /// </summary>
    public abstract class SettingsServiceBase
    {
        protected readonly string _settingsPath;
        protected readonly AppSettings _settings;
        protected readonly ILogService _logService;

        protected SettingsServiceBase(ILogService logService)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "FactorioModManager");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
            _logService = logService;
            _settings = LoadSettingsInternal();
            _logService.SetVerboseEnabled(_settings.VerboseDetectionLogging);
        }

        private AppSettings LoadSettingsInternal()
        {
            if (!File.Exists(_settingsPath))
            {
                var (Username, Token) = LoadFactorioDefaults();
                return new AppSettings
                {
                    FactorioModsPath = FolderPathHelper.GetModsDirectory(),
                    Username = Username,
                    Token = Token
                };
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                if (string.IsNullOrEmpty(settings.FactorioModsPath))
                {
                    settings.FactorioModsPath = FolderPathHelper.GetModsDirectory();
                }

                if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Token))
                {
                    var (Username, Token) = LoadFactorioDefaults();
                    settings.Username ??= Username;
                    settings.Token ??= Token;
                }

                if (settings.UpdateConcurrency <= 0)
                    settings.UpdateConcurrency = 3;

                return settings;
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load settings, using defaults", ex);
                var (Username, Token) = LoadFactorioDefaults();
                return new AppSettings
                {
                    FactorioModsPath = FolderPathHelper.GetModsDirectory(),
                    Username = Username,
                    Token = Token
                };
            }
        }

        private (string? Username, string? Token) LoadFactorioDefaults()
        {
            try
            {
                var playerDataPath = FolderPathHelper.GetPlayerDataPath();

                if (File.Exists(playerDataPath))
                {
                    var json = File.ReadAllText(playerDataPath);

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var username = root.TryGetProperty("service-username", out var usernameElement)
                        ? usernameElement.GetString()
                        : null;

                    var token = root.TryGetProperty("service-token", out var tokenElement)
                        ? tokenElement.GetString()
                        : null;

                    _logService.LogDebug($"Loaded Factorio credentials from player-data.json: Username={username}, Token={(token != null ? "***" : "null")}");

                    return (username, token);
                }
                else
                {
                    _logService.LogDebug($"player-data.json not found at {playerDataPath}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error loading Factorio player-data.json: {ex.Message}", ex);
            }

            return (null, null);
        }

        protected void SaveSettingsInternal()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, Constants.JsonHelper.IndentedOnly);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving settings: {ex.Message}", ex);
            }
        }

        protected void UpdateAndSave(Action<AppSettings> updater)
        {
            updater(_settings);
            SaveSettingsInternal();
        }
    }
}