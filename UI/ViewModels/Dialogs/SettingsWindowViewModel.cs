using FactorioModManager.Services;
using ReactiveUI;
using System.Reactive;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class SettingsWindowViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;

        private string? _modsPath;

        public string? ModsPath
        {
            get => _modsPath;
            set => this.RaiseAndSetIfChanged(ref _modsPath, value);
        }

        private string? _apiKey;

        public string? ApiKey
        {
            get => _apiKey;
            set => this.RaiseAndSetIfChanged(ref _apiKey, value);
        }

        private string? _username;

        public string? Username
        {
            get => _username;
            set => this.RaiseAndSetIfChanged(ref _username, value);
        }

        private string? _token;

        public string? Token
        {
            get => _token;
            set => this.RaiseAndSetIfChanged(ref _token, value);
        }

        private bool _keepOldModFiles;

        public bool KeepOldModFiles
        {
            get => _keepOldModFiles;
            set => this.RaiseAndSetIfChanged(ref _keepOldModFiles, value);
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

            SaveCommand = ReactiveCommand.Create(SaveSettings);
            CancelCommand = ReactiveCommand.Create(() => { });
        }

        private void SaveSettings()
        {
            _settingsService.SetApiKey(string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey);

            if (!string.IsNullOrWhiteSpace(ModsPath))
            {
                _settingsService.SetModsPath(ModsPath);
            }

            _settingsService.SetUsername(string.IsNullOrWhiteSpace(Username) ? null : Username);
            _settingsService.SetToken(string.IsNullOrWhiteSpace(Token) ? null : Token);
            _settingsService.SetKeepOldModFiles(KeepOldModFiles);
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(ModsPath))
            {
                return false;
            }

            return System.IO.Directory.Exists(ModsPath);
        }
    }
}