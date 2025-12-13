using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Platform;
using FactorioModManager.Services.Settings;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IPlatformService _platformService;
        private readonly ILogService _logService;
        private readonly CompositeDisposable _disposables = [];

        private string? _modsPath;
        private string? _apiKey;
        private string? _username;
        private string? _token;
        private bool _keepOldModFiles;
        private string? _validationError;

        private string? _factorioExePath;
        private string? _factorioDataPath;

        private string? _factorioPathStatusText;
        private string? _factorioDataPathStatusText;

        private int _updateConcurrency = 3; // sensible default

        public string? FactorioExePath
        {
            get => _factorioExePath;
            set => this.RaiseAndSetIfChanged(ref _factorioExePath, value);
        }

        public string? FactorioDataPath
        {
            get => _factorioDataPath;
            set => this.RaiseAndSetIfChanged(ref _factorioDataPath, value);
        }

        public string? FactorioPathStatusText
        {
            get => _factorioPathStatusText;
            private set => this.RaiseAndSetIfChanged(ref _factorioPathStatusText, value);
        }

        public string? FactorioDataPathStatusText
        {
            get => _factorioDataPathStatusText;
            private set => this.RaiseAndSetIfChanged(ref _factorioDataPathStatusText, value);
        }

        // Commands for UI actions (bound from XAML)
        public ReactiveCommand<Unit, Unit> BrowseModsCommand { get; }

        public ReactiveCommand<Unit, Unit> BrowseFactorioCommand { get; }
        public ReactiveCommand<Unit, Unit> AutoDetectFactorioCommand { get; }
        public ReactiveCommand<Unit, Unit> TestFactorioCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseDataCommand { get; }
        public ReactiveCommand<Unit, Unit> AutoDetectDataCommand { get; }

        public string? ModsPath
        {
            get => _modsPath;
            set => this.RaiseAndSetIfChanged(ref _modsPath, value);
        }

        public string? ApiKey
        {
            get => _apiKey;
            set => this.RaiseAndSetIfChanged(ref _apiKey, value);
        }

        public string? Username
        {
            get => _username;
            set => this.RaiseAndSetIfChanged(ref _username, value);
        }

        public string? Token
        {
            get => _token;
            set => this.RaiseAndSetIfChanged(ref _token, value);
        }

        public bool KeepOldModFiles
        {
            get => _keepOldModFiles;
            set => this.RaiseAndSetIfChanged(ref _keepOldModFiles, value);
        }

        // New: concurrency for Update All
        public int UpdateConcurrency
        {
            get => _updateConcurrency;
            set
            {
                var clamped = Math.Max(1, value);
                this.RaiseAndSetIfChanged(ref _updateConcurrency, clamped);
            }
        }

        private bool _checkForAppUpdates;

        public bool CheckForAppUpdates
        {
            get => _checkForAppUpdates;
            set => this.RaiseAndSetIfChanged(ref _checkForAppUpdates, value);
        }

        // New: auto-check mods on startup
        private bool _autoCheckModUpdates;

        public bool AutoCheckModUpdates
        {
            get => _autoCheckModUpdates;
            set => this.RaiseAndSetIfChanged(ref _autoCheckModUpdates, value);
        }

        private DateTime? _lastAppUpdateCheck;

        public DateTime? LastAppUpdateCheck
        {
            get => _lastAppUpdateCheck;
            set => this.RaiseAndSetIfChanged(ref _lastAppUpdateCheck, value);
        }

        // ✅ Validation error display
        public string? ValidationError
        {
            get => _validationError;
            private set => this.RaiseAndSetIfChanged(ref _validationError, value);
        }

        private bool _showHiddenDependencies;

        public bool ShowHiddenDependencies
        {
            get => _showHiddenDependencies;
            set => this.RaiseAndSetIfChanged(ref _showHiddenDependencies, value);
        }

        // NEW: verbose detection logging toggle exposed to UI
        private bool _verboseDetectionLogging;

        public bool VerboseDetectionLogging
        {
            get => _verboseDetectionLogging;
            set
            {
                this.RaiseAndSetIfChanged(ref _verboseDetectionLogging, value);

                // Apply immediately to settings/log service
                try
                {
                    _settingsService.SetVerboseDetectionLogging(value);
                }
                catch { }

                // Provide immediate UI feedback
                VerboseStatusMessage = value ? "Verbose logging enabled" : "Verbose logging disabled";

                // Clear feedback after 3 seconds
                _verboseClearDisposable?.Dispose();
                _verboseClearDisposable = Observable.Timer(TimeSpan.FromSeconds(3))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(_ => VerboseStatusMessage = null);
            }
        }

        private IDisposable? _verboseClearDisposable;
        private string? _verboseStatusMessage;

        public string? VerboseStatusMessage
        {
            get => _verboseStatusMessage;
            private set => this.RaiseAndSetIfChanged(ref _verboseStatusMessage, value);
        }

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public SettingsWindowViewModel(ISettingsService settingsService, IPlatformService platformService, ILogService logService)
        {
            _settingsService = settingsService;
            _platformService = platformService;
            _logService = logService;

            // Create commands
            BrowseModsCommand = ReactiveCommand.CreateFromTask(async () => await BrowseModsAsync());
            BrowseFactorioCommand = ReactiveCommand.CreateFromTask(async () => await BrowseFactorioAsync());
            AutoDetectFactorioCommand = ReactiveCommand.CreateFromTask(async () => await AutoDetectFactorioAsync());
            TestFactorioCommand = ReactiveCommand.CreateFromTask(async () => await TestFactorioAsync());
            BrowseDataCommand = ReactiveCommand.CreateFromTask(async () => await BrowseDataAsync());
            AutoDetectDataCommand = ReactiveCommand.CreateFromTask(async () => await AutoDetectDataAsync());

            // Load settings
            ModsPath = _settingsService.GetModsPath();
            ApiKey = _settingsService.GetApiKey();
            Username = _settingsService.GetUsername();
            Token = _settingsService.GetToken();
            KeepOldModFiles = _settingsService.GetKeepOldModFiles();
            FactorioExePath = _settingsService.GetFactorioExecutablePath();
            FactorioDataPath = _settingsService.GetFactorioDataPath();
            CheckForAppUpdates = _settingsService.GetCheckForAppUpdates();
            AutoCheckModUpdates = _settingsService.GetAutoCheckModUpdates();
            LastAppUpdateCheck = _settingsService.GetLastAppUpdateCheck();
            ShowHiddenDependencies = _settingsService.GetShowHiddenDependencies();

            // Load update concurrency
            UpdateConcurrency = _settingsService.GetUpdateConcurrency();

            // Load verbose detection logging setting
            VerboseDetectionLogging = _settingsService.GetVerboseDetectionLogging();

            // ✅ SaveCommand with validation
            var canSave = this.WhenAnyValue(x => x.ModsPath)
                .Select(_ => Validate())
                .ObserveOn(RxApp.MainThreadScheduler);

            SaveCommand = ReactiveCommand.Create(SaveSettings, canSave);
            CancelCommand = ReactiveCommand.Create(() => { });

            // ✅ Clear validation error when path changes
            this.WhenAnyValue(x => x.ModsPath,
                x => x.FactorioExePath,
                x => x.FactorioDataPath)
              .Subscribe(_ => ValidationError = null)
              .DisposeWith(_disposables);
        }

        private async Task BrowseModsAsync()
        {
            var path = await _platformService.PickFolderAsync("Select Factorio Mods Folder");
            if (!string.IsNullOrEmpty(path))
            {
                ModsPath = path;
            }
        }

        private async Task BrowseFactorioAsync()
        {
            var pattern = OperatingSystem.IsWindows() ? ["factorio.exe"] : new[] { "factorio" };
            var path = await _platformService.PickFileAsync("Select Factorio Executable", pattern);
            if (!string.IsNullOrEmpty(path))
            {
                FactorioExePath = path;
                FactorioPathStatusText = "Selected executable";
            }
        }

        private async Task AutoDetectFactorioAsync()
        {
            try
            {
                string? detected = FolderPathHelper.DetectFactorioExecutable(_logService);
                if (!string.IsNullOrEmpty(detected))
                {
                    FactorioExePath = detected;
                    FactorioPathStatusText = $"✓ Auto-detected: {detected}";
                }
                else
                {
                    FactorioPathStatusText = "⚠ Could not auto-detect Factorio executable";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Auto-detect error", ex);
                FactorioPathStatusText = $"❌ Error: {ex.Message}";
            }
        }

        private async Task TestFactorioAsync()
        {
            var path = FactorioExePath;
            if (string.IsNullOrEmpty(path))
            {
                path = FolderPathHelper.DetectFactorioExecutable(_logService);
            }

            if (string.IsNullOrEmpty(path))
            {
                FactorioPathStatusText = "❌ Invalid path or file does not exist";
                return;
            }

            var ok = await _platformService.LaunchProcessAsync(path);
            FactorioPathStatusText = ok ? "✓ Launched successfully" : "❌ Launch failed";
        }

        private async Task BrowseDataAsync()
        {
            var path = await _platformService.PickFolderAsync("Select Factorio Data Folder");
            if (!string.IsNullOrEmpty(path))
            {
                FactorioDataPath = path;
                FactorioDataPathStatusText = "Selected data folder";
            }
        }

        private async Task AutoDetectDataAsync()
        {
            try
            {
                var detected = FolderPathHelper.GetFactorioDataPath(_logService, null, FactorioExePath);
                if (!string.IsNullOrEmpty(detected))
                {
                    FactorioDataPath = detected;
                    FactorioDataPathStatusText = $"✓ Auto-detected: {detected}";
                }
                else
                {
                    FactorioDataPathStatusText = "⚠ Could not auto-detect Factorio data folder";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Auto-detect data path error", ex);
                FactorioDataPathStatusText = $"❌ Error: {ex.Message}";
            }
        }

        internal bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ModsPath))
            {
                ValidationError = "Mods path is required";
                return false;
            }

            if (!System.IO.Directory.Exists(ModsPath))
            {
                ValidationError = "Directory does not exist";
                return false;
            }

            // Validate update concurrency
            if (UpdateConcurrency <= 0)
            {
                ValidationError = "Update concurrency must be at least 1";
                return false;
            }

            ValidationError = null;
            return true;
        }

        private void SaveSettings()
        {
            if (!Validate())
                return;

            _settingsService.SetApiKey(string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey);

            if (!string.IsNullOrWhiteSpace(ModsPath))
            {
                _settingsService.SetModsPath(ModsPath);
            }
            if (!string.IsNullOrWhiteSpace(FactorioExePath))
            {
                _settingsService.SetFactorioExecutablePath(FactorioExePath);
            }

            if (!string.IsNullOrWhiteSpace(FactorioDataPath))
            {
                _settingsService.SetFactorioDataPath(FactorioDataPath);
            }

            _settingsService.SetUsername(string.IsNullOrWhiteSpace(Username) ? null : Username);
            _settingsService.SetToken(string.IsNullOrWhiteSpace(Token) ? null : Token);
            _settingsService.SetKeepOldModFiles(KeepOldModFiles);
            _settingsService.SetCheckForAppUpdates(CheckForAppUpdates);
            _settingsService.SetAutoCheckModUpdates(AutoCheckModUpdates);
            _settingsService.SetShowHiddenDependencies(ShowHiddenDependencies);
            _settingsService.SetUpdateConcurrency(UpdateConcurrency);

            // Persist verbose detection logging setting
            _settingsService.SetVerboseDetectionLogging(VerboseDetectionLogging);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _verboseClearDisposable?.Dispose();
                _disposables?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}