using FactorioModManager.Services.Infrastructure;
using System;

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

        // Concurrency for Update All
        int GetUpdateConcurrency();

        void SetUpdateConcurrency(int concurrency);

        // Verbose detection logging
        bool GetVerboseDetectionLogging();

        void SetVerboseDetectionLogging(bool enabled);

        // Auto-check mods on startup (new)
        bool GetAutoCheckModUpdates();

        void SetAutoCheckModUpdates(bool enabled);

        // Groups panel visibility
        bool GetShowGroupsPanel();

        void SetShowGroupsPanel(bool value);

        double GetGroupsColumnWidth();

        void SetGroupsColumnWidth(double width);

        // New: column visibility for mods list
        bool GetShowCategoryColumn();

        void SetShowCategoryColumn(bool value);

        bool GetShowSizeColumn();

        void SetShowSizeColumn(bool value);

        public event Action? FactorioPathChanged;

        // New optional events for reactive updates
        public event Action? ModsPathChanged;

        public event Action? ShowHiddenDependenciesChanged;

        // Fired when Factorio data path changes (used to re-run DLC/version detection)
        public event Action? FactorioDataPathChanged;

        public event Action? ShowGroupsPanelChanged;

        // New events for column visibility changes
        public event Action? ShowCategoryColumnChanged;

        public event Action? ShowSizeColumnChanged;
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

        // New: concurrency limit for Update All
        public int UpdateConcurrency { get; set; } = 3;

        // New: verbose detection logging
        public bool VerboseDetectionLogging { get; set; } = false;

        // New: auto-check mods on startup
        public bool AutoCheckModUpdates { get; set; } = true;

        // New: remember groups panel visibility
        public bool ShowGroupsPanel { get; set; } = true;

        // Persist last width of the groups column (pixels)
        public double GroupsColumnWidth { get; set; } = 200.0;

        // Persist visibility of category and size columns (default true = visible)
        public bool ShowCategoryColumn { get; set; } = true;

        public bool ShowSizeColumn { get; set; } = true;
    }

    public class SettingsService(ILogService logService) : SettingsServiceBase(logService), ISettingsService
    {
        public string GetModsPath()
        {
            return _settings.FactorioModsPath ?? FolderPathHelper.GetModsDirectory();
        }

        public void SetModsPath(string path)
        {
            // Only update and raise event when actual value changed
            var current = _settings.FactorioModsPath ?? string.Empty;
            var candidate = path ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateAndSave(s => s.FactorioModsPath = path);
            _logService.LogDebug($"Settings: ModsPath changed from '{current}' to '{candidate}'");
            ModsPathChanged?.Invoke();
        }

        public string? GetApiKey() => _settings.ApiKey;

        public void SetApiKey(string? apiKey)
        {
            var current = _settings.ApiKey ?? string.Empty;
            var candidate = apiKey ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.Ordinal))
                return;

            UpdateAndSave(s => s.ApiKey = apiKey);
            _logService.LogDebug("Settings: ApiKey changed (masked)");
        }

        public string? GetUsername() => _settings.Username;

        public void SetUsername(string? username)
        {
            var current = _settings.Username ?? string.Empty;
            var candidate = username ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.Ordinal))
                return;

            UpdateAndSave(s => s.Username = username);
            _logService.LogDebug($"Settings: Username changed from '{current}' to '{candidate}'");
        }

        public string? GetToken() => _settings.Token;

        public void SetToken(string? token)
        {
            var current = _settings.Token ?? string.Empty;
            var candidate = token ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.Ordinal))
                return;

            UpdateAndSave(s => s.Token = token);
            _logService.LogDebug("Settings: Token changed (masked)");
        }

        public DateTime? GetLastModUpdateCheck() => _settings.LastModUpdateCheck;

        public void SetLastModUpdateCheck(DateTime dateTime) => UpdateAndSave(s => s.LastModUpdateCheck = dateTime);

        public bool GetKeepOldModFiles() => _settings.KeepOldModFiles;

        public void SetKeepOldModFiles(bool keepOldFiles)
        {
            if (_settings.KeepOldModFiles == keepOldFiles)
                return;

            UpdateAndSave(s => s.KeepOldModFiles = keepOldFiles);
            _logService.LogDebug($"Settings: KeepOldModFiles changed to {keepOldFiles}");
        }

        public string? GetFactorioExecutablePath() => _settings.FactorioExePath;

        public void SetFactorioExecutablePath(string path)
        {
            var current = _settings.FactorioExePath ?? string.Empty;
            var candidate = path ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateAndSave(s => s.FactorioExePath = path);
            _logService.LogDebug($"Settings: FactorioExePath changed from '{current}' to '{candidate}'");
            FactorioPathChanged?.Invoke();
        }

        public DateTime? GetLastAppUpdateCheck() => _settings.LastAppUpdateCheck;

        public void SetLastAppUpdateCheck(DateTime timestamp) => UpdateAndSave(s => s.LastAppUpdateCheck = timestamp);

        public bool GetCheckForAppUpdates() => _settings.CheckForAppUpdates;

        public void SetCheckForAppUpdates(bool enabled)
        {
            if (_settings.CheckForAppUpdates == enabled)
                return;

            UpdateAndSave(s => s.CheckForAppUpdates = enabled);
            _logService.LogDebug($"Settings: CheckForAppUpdates changed to {enabled}");
        }

        public string? GetFactorioVersion() => _settings.FactorioVersion;

        public void SetFactorioVersion(string? version)
        {
            var current = _settings.FactorioVersion ?? string.Empty;
            var candidate = version ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.Ordinal))
                return;

            UpdateAndSave(s => s.FactorioVersion = version);
            _logService.LogDebug($"Settings: FactorioVersion changed from '{current}' to '{candidate}'");
        }

        public bool GetHasSpaceAgeDLC() => _settings.HasSpaceAgeDlc;

        public void SetHasSpaceAgeDlc(bool value)
        {
            if (_settings.HasSpaceAgeDlc == value)
                return;

            UpdateAndSave(s => s.HasSpaceAgeDlc = value);
            _logService.LogDebug($"Settings: HasSpaceAgeDlc changed to {value}");
        }

        public string? GetFactorioDataPath() => _settings.FactorioDataPath;

        public void SetFactorioDataPath(string? path)
        {
            var current = _settings.FactorioDataPath ?? string.Empty;
            var candidate = path ?? string.Empty;
            if (string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase))
                return;

            UpdateAndSave(s => s.FactorioDataPath = path);
            _logService.LogDebug($"Settings: FactorioDataPath changed from '{current}' to '{candidate}'");
            FactorioDataPathChanged?.Invoke();
        }

        public bool GetVerboseDetectionLogging() => _settings.VerboseDetectionLogging;

        public void SetVerboseDetectionLogging(bool enabled)
        {
            if (_settings.VerboseDetectionLogging == enabled)
                return;

            UpdateAndSave(s => s.VerboseDetectionLogging = enabled);
            _logService.SetVerboseEnabled(enabled);
            _logService.LogDebug($"Settings: VerboseDetectionLogging changed to {enabled}");
        }

        public event Action? FactorioPathChanged;

        public event Action? ModsPathChanged;

        public event Action? ShowHiddenDependenciesChanged;

        public event Action? FactorioDataPathChanged;

        public event Action? ShowGroupsPanelChanged;

        public event Action? ShowCategoryColumnChanged;

        public event Action? ShowSizeColumnChanged;

        public bool GetShowHiddenDependencies() => _settings.ShowHiddenDependencies;

        public void SetShowHiddenDependencies(bool value)
        {
            if (_settings.ShowHiddenDependencies == value)
                return;

            UpdateAndSave(s => s.ShowHiddenDependencies = value);
            _logService.LogDebug($"Settings: ShowHiddenDependencies changed to {value}");
            ShowHiddenDependenciesChanged?.Invoke();
        }

        public int GetUpdateConcurrency() => _settings.UpdateConcurrency <= 0 ? 1 : _settings.UpdateConcurrency;

        public void SetUpdateConcurrency(int concurrency)
        {
            if (_settings.UpdateConcurrency == concurrency)
                return;

            UpdateAndSave(s => s.UpdateConcurrency = concurrency);
            _logService.LogDebug($"Settings: UpdateConcurrency changed to {concurrency}");
        }

        // Persist column width for Groups panel
        public double GetGroupsColumnWidth() => _settings.GroupsColumnWidth <= 0 ? 200.0 : _settings.GroupsColumnWidth;

        public void SetGroupsColumnWidth(double width)
        {
            if (Math.Abs(_settings.GroupsColumnWidth - width) < 0.5)
                return;
            UpdateAndSave(s => s.GroupsColumnWidth = width);
            _logService.LogDebug($"Settings: GroupsColumnWidth changed to {width}");
            ShowGroupsPanelChanged?.Invoke();
        }

        public bool GetAutoCheckModUpdates() => _settings.AutoCheckModUpdates;

        public void SetAutoCheckModUpdates(bool enabled)
        {
            if (_settings.AutoCheckModUpdates == enabled)
                return;

            UpdateAndSave(s => s.AutoCheckModUpdates = enabled);
            _logService.LogDebug($"Settings: AutoCheckModUpdates changed to {enabled}");
        }

        public bool GetShowGroupsPanel() => _settings.ShowGroupsPanel;

        public void SetShowGroupsPanel(bool value)
        {
            if (_settings.ShowGroupsPanel == value)
                return;

            UpdateAndSave(s => s.ShowGroupsPanel = value);
            _logService.LogDebug($"Settings: ShowGroupsPanel changed to {value}");
            ShowGroupsPanelChanged?.Invoke();
        }

        public bool GetShowCategoryColumn() => _settings.ShowCategoryColumn;

        public void SetShowCategoryColumn(bool value)
        {
            if (_settings.ShowCategoryColumn == value)
                return;

            UpdateAndSave(s => s.ShowCategoryColumn = value);
            _logService.LogDebug($"Settings: ShowCategoryColumn changed to {value}");
            ShowCategoryColumnChanged?.Invoke();
        }

        public bool GetShowSizeColumn() => _settings.ShowSizeColumn;

        public void SetShowSizeColumn(bool value)
        {
            if (_settings.ShowSizeColumn == value)
                return;

            UpdateAndSave(s => s.ShowSizeColumn = value);
            _logService.LogDebug($"Settings: ShowSizeColumn changed to {value}");
            ShowSizeColumnChanged?.Invoke();
        }
    }
}