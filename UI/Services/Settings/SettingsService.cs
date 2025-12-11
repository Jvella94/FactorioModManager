using FactorioModManager.Services.Infrastructure;
using System;
using System.IO;
using System.Text.Json;

namespace FactorioModManager.Services.Settings
{
    public interface ISettingsService
    {
        string GetModsPath();

        void SetModsPath(string path);

        string? GetApiKey();

        void SetApiKey(string? apiKey);

        string? GetUsername();

        void SetUsername(string? username);

        string? GetToken();

        void SetToken(string? token);

        bool GetKeepOldModFiles();

        void SetKeepOldModFiles(bool keep);

        DateTime? GetLastModUpdateCheck();

        void SetLastModUpdateCheck(DateTime dateTime);

        string? GetFactorioExecutablePath();

        void SetFactorioExecutablePath(string path);

        DateTime? GetLastAppUpdateCheck();

        void SetLastAppUpdateCheck(DateTime timestamp);

        bool GetCheckForAppUpdates();

        void SetCheckForAppUpdates(bool enabled);

        // Factorio version & DLC info
        string? GetFactorioVersion();

        void SetFactorioVersion(string? version);

        bool GetHasSpaceAgeDLC();

        void SetHasSpaceAgeDlc(bool value);

        string? GetFactorioDataPath();

        void SetFactorioDataPath(string? path);

        bool GetShowHiddenDependencies();

        void SetShowHiddenDependencies(bool value);

        public event Action? FactorioPathChanged;
    }

    public class AppSettings
    {
        public string? FactorioModsPath { get; set; }
        public string? ApiKey { get; set; }
        public string? Username { get; set; }
        public string? Token { get; set; }
        public DateTime? LastModUpdateCheck { get; set; }
        public bool KeepOldModFiles { get; set; } = false;
        public string? FactorioExePath { get; set; }
        public DateTime? LastAppUpdateCheck { get; set; }
        public bool CheckForAppUpdates { get; set; } = true;
        public string? FactorioVersion { get; set; }
        public bool HasSpaceAgeDlc { get; set; }
        public string? FactorioDataPath { get; set; }
        public bool ShowHiddenDependencies { get; set; } = false;
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsPath;
        private readonly AppSettings _settings;
        private readonly ILogService _logService;

        public SettingsService(ILogService logService)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "FactorioModManager");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
            _settings = LoadSettings();
            _logService = logService;
        }

        private AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                // Try to load defaults from Factorio's player-data.json
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

                // If mods path is not set, use default
                if (string.IsNullOrEmpty(settings.FactorioModsPath))
                {
                    settings.FactorioModsPath = FolderPathHelper.GetModsDirectory();
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

                    // Parse the JSON to extract service-username and service-token
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

        private void SaveSettings()
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

        public string GetModsPath()
        {
            return _settings.FactorioModsPath ?? FolderPathHelper.GetModsDirectory();
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

        public DateTime? GetLastModUpdateCheck()
        {
            return _settings.LastModUpdateCheck;
        }

        public void SetLastModUpdateCheck(DateTime dateTime)
        {
            _settings.LastModUpdateCheck = dateTime;
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

        public string? GetFactorioExecutablePath()
        {
            return _settings.FactorioExePath;
        }

        public void SetFactorioExecutablePath(string path)
        {
            _settings.FactorioExePath = path;
            SaveSettings();
            FactorioPathChanged?.Invoke();
        }

        public DateTime? GetLastAppUpdateCheck() => _settings.LastAppUpdateCheck;

        public void SetLastAppUpdateCheck(DateTime timestamp)
        {
            _settings.LastAppUpdateCheck = timestamp;
            SaveSettings();
        }

        public bool GetCheckForAppUpdates() => _settings.CheckForAppUpdates;

        public void SetCheckForAppUpdates(bool enabled)
        {
            _settings.CheckForAppUpdates = enabled;
            SaveSettings();
        }

        public string? GetFactorioVersion() => _settings.FactorioVersion;

        public void SetFactorioVersion(string? version)
        {
            _settings.FactorioVersion = version;
            SaveSettings();
        }

        public bool GetHasSpaceAgeDLC() => _settings.HasSpaceAgeDlc;

        public void SetHasSpaceAgeDlc(bool value)
        {
            _settings.HasSpaceAgeDlc = value;
            SaveSettings();
        }

        public string? GetFactorioDataPath() => _settings.FactorioDataPath;

        public void SetFactorioDataPath(string? path)
        {
            _settings.FactorioDataPath = path;
            SaveSettings();
        }

        public event Action? FactorioPathChanged;

        public bool GetShowHiddenDependencies()
        {
            return _settings.ShowHiddenDependencies;
        }

        public void SetShowHiddenDependencies(bool value)
        {
            _settings.ShowHiddenDependencies = value;
            SaveSettings();
        }
    }
}