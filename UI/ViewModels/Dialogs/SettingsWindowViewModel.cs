using FactorioModManager.Services.Settings;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly CompositeDisposable _disposables = [];

        private string? _modsPath;
        private string? _apiKey;
        private string? _username;
        private string? _token;
        private bool _keepOldModFiles;
        private string? _validationError;

        private string? _factorioExePath;

        public string? FactorioExePath
        {
            get => _factorioExePath;
            set => this.RaiseAndSetIfChanged(ref _factorioExePath, value);
        }

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

        private bool _checkForAppUpdates;

        public bool CheckForAppUpdates
        {
            get => _checkForAppUpdates;
            set => this.RaiseAndSetIfChanged(ref _checkForAppUpdates, value);
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

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public SettingsWindowViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            // Load settings
            ModsPath = _settingsService.GetModsPath();
            ApiKey = _settingsService.GetApiKey();
            Username = _settingsService.GetUsername();
            Token = _settingsService.GetToken();
            KeepOldModFiles = _settingsService.GetKeepOldModFiles();
            FactorioExePath = _settingsService.GetFactorioExecutablePath();
            CheckForAppUpdates = _settingsService.GetCheckForAppUpdates();
            LastAppUpdateCheck = _settingsService.GetLastAppUpdateCheck();
            ShowHiddenDependencies = _settingsService.GetShowHiddenDependencies();

            // ✅ SaveCommand with validation
            var canSave = this.WhenAnyValue(x => x.ModsPath)
                .Select(_ => Validate())
                .ObserveOn(RxApp.MainThreadScheduler);

            SaveCommand = ReactiveCommand.Create(SaveSettings, canSave);
            CancelCommand = ReactiveCommand.Create(() => { });

            // ✅ Clear validation error when path changes
            this.WhenAnyValue(x => x.ModsPath,
                x => x.FactorioExePath)
              .Subscribe(_ => ValidationError = null)
              .DisposeWith(_disposables);
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

            _settingsService.SetUsername(string.IsNullOrWhiteSpace(Username) ? null : Username);
            _settingsService.SetToken(string.IsNullOrWhiteSpace(Token) ? null : Token);
            _settingsService.SetKeepOldModFiles(KeepOldModFiles);
            _settingsService.SetCheckForAppUpdates(CheckForAppUpdates);
            _settingsService.SetShowHiddenDependencies(ShowHiddenDependencies);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}