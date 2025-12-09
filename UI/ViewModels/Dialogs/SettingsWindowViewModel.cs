using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        private string? _modsPath;
        private string? _apiKey;
        private string? _username;
        private string? _token;
        private bool _keepOldModFiles;
        private string? _validationError;

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

        // ✅ Validation error display
        public string? ValidationError
        {
            get => _validationError;
            private set => this.RaiseAndSetIfChanged(ref _validationError, value);
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

            // ✅ SaveCommand with validation
            var canSave = this.WhenAnyValue(x => x.ModsPath)
                .Select(_ => Validate())
                .ObserveOn(RxApp.MainThreadScheduler);

            SaveCommand = ReactiveCommand.Create(SaveSettings, canSave);
            CancelCommand = ReactiveCommand.Create(() => { });

            // ✅ Clear validation error when path changes
            this.WhenAnyValue(x => x.ModsPath)
                .Subscribe(_ => ValidationError = null);
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

            _settingsService.SetUsername(string.IsNullOrWhiteSpace(Username) ? null : Username);
            _settingsService.SetToken(string.IsNullOrWhiteSpace(Token) ? null : Token);
            _settingsService.SetKeepOldModFiles(KeepOldModFiles);
        }
    }
}