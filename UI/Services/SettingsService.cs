using FactorioModManager.Services.Infrastructure;
using System;
using System.IO;
using System.Text.Json;

namespace FactorioModManager.Services
{
    public class Settings
    {
        public string? FactorioModsPath { get; set; }
        public string? ApiKey { get; set; }
        public string? Username { get; set; }
        public string? Token { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
        public bool KeepOldModFiles { get; set; } = false;
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private readonly Settings _settings;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "FactorioModManager");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");

            _settings = LoadSettings();
        }

        private Settings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                // Try to load defaults from Factorio's player-data.json
                var (Username, Token) = LoadFactorioDefaults();
                return new Settings
                {
                    FactorioModsPath = ModPathHelper.GetModsDirectory(),
                    Username = Username,
                    Token = Token
                };
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();

                // If mods path is not set, use default
                if (string.IsNullOrEmpty(settings.FactorioModsPath))
                {
                    settings.FactorioModsPath = ModPathHelper.GetModsDirectory();
                }

                // If username/token not set, try to load from Factorio
                if (string.IsNullOrEmpty(settings.Username) || string.IsNullOrEmpty(settings.Token))
                {
                    var (Username, Token) = LoadFactorioDefaults();
                    settings.Username ??= Username;
                    settings.Token ??= Token;
                }

                return settings;
            }
            catch
            {
                var (Username, Token) = LoadFactorioDefaults();
                return new Settings
                {
                    FactorioModsPath = ModPathHelper.GetModsDirectory(),
                    Username = Username,
                    Token = Token
                };
            }
        }

        private static (string? Username, string? Token) LoadFactorioDefaults()
        {
            try
            {
                var playerDataPath = ModPathHelper.GetPlayerDataPath();

                if (File.Exists(playerDataPath))
                {
                    var json = File.ReadAllText(playerDataPath);

                    // Parse the JSON to extract service-username and service-token
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var username = root.TryGetProperty("service-username", out var usernameElement)
                        ? usernameElement.GetString()
                        : null;

                    var token = root.TryGetProperty("service-token", out var tokenElement)
                        ? tokenElement.GetString()
                        : null;

                    LogService.Instance.LogDebug($"Loaded Factorio credentials from player-data.json: Username={username}, Token={(token != null ? "***" : "null")}");

                    return (username, token);
                }
                else
                {
                    LogService.Instance.LogDebug($"player-data.json not found at {playerDataPath}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.LogDebug($"Error loading Factorio player-data.json: {ex.Message}");
            }

            return (null, null);
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public string GetModsPath() 
        {
            return _settings.FactorioModsPath ?? ModPathHelper.GetModsDirectory();
        }

        public void SetModsPath(string path)
        {
            _settings.FactorioModsPath = path;
            SaveSettings();
        }

        public string? GetApiKey()
        {
            return _settings.ApiKey;
        }

        public void SetApiKey(string? apiKey)
        {
            _settings.ApiKey = apiKey;
            SaveSettings();
        }

        public string? GetUsername()
        {
            return _settings.Username;
        }

        public void SetUsername(string? username)
        {
            _settings.Username = username;
            SaveSettings();
        }

        public string? GetToken()
        {
            return _settings.Token;
        }

        public void SetToken(string? token)
        {
            _settings.Token = token;
            SaveSettings();
        }

        public DateTime? GetLastUpdateCheck()
        {
            return _settings.LastUpdateCheck;
        }

        public void SetLastUpdateCheck(DateTime dateTime)
        {
            _settings.LastUpdateCheck = dateTime;
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
    }
}
