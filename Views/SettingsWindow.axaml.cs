using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FactorioModManager.Views
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private string? _modsPath;
        private string? _apiKey;
        private string? _username;
        private string? _token;
        private bool _keepOldModFiles;

        public new event PropertyChangedEventHandler? PropertyChanged;

        public SettingsWindow() : this(new SettingsService())
        {
            // Parameterless constructor required for XAML designer
        }

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;

            // Load settings
            ModsPath = _settingsService.GetModsPath();
            ApiKey = _settingsService.GetApiKey();
            Username = _settingsService.GetUsername();
            Token = _settingsService.GetToken();
            KeepOldModFiles = _settingsService.GetKeepOldModFiles();

            DataContext = this;
        }

        public string? ModsPath
        {
            get => _modsPath;
            set
            {
                if (_modsPath != value)
                {
                    _modsPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? ApiKey
        {
            get => _apiKey;
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Token
        {
            get => _token;
            set
            {
                if (_token != value)
                {
                    _token = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool KeepOldModFiles
        {
            get => _keepOldModFiles;
            set
            {
                if (_keepOldModFiles != value)
                {
                    _keepOldModFiles = value;
                    OnPropertyChanged();
                }
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void BrowseModsPath(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folderPickerOptions = new FolderPickerOpenOptions
            {
                Title = "Select Factorio Mods Folder",
                AllowMultiple = false
            };

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (folders.Count > 0)
            {
                var selectedFolder = folders[0];
                ModsPath = selectedFolder.Path.LocalPath;
            }
        }

        private void SaveSettings(object? sender, RoutedEventArgs e)
        {
            _settingsService.SetApiKey(string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey);

            if (!string.IsNullOrWhiteSpace(ModsPath))
            {
                _settingsService.SetModsPath(ModsPath);
            }

            _settingsService.SetUsername(string.IsNullOrWhiteSpace(Username) ? null : Username);
            _settingsService.SetToken(string.IsNullOrWhiteSpace(Token) ? null : Token);
            _settingsService.SetKeepOldModFiles(KeepOldModFiles);

            Close(true);
        }

        private void CancelSettings(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
