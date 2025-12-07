using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using ReactiveUI;
using FactorioModManager.Services;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM : ViewModelBase
    {
        private ObservableCollection<ModViewModel> _mods;
        private ObservableCollection<ModGroupViewModel> _groups;
        private ObservableCollection<ModViewModel> _selectedMods;
        private ModViewModel? _selectedMod;
        private ModGroupViewModel? _selectedGroup;
        private string _searchText = string.Empty;
        private bool _showDisabled = true;
        private bool _filterBySelectedGroup = false;
        private string _statusText = "Ready";
        private string? _selectedAuthorFilter;
        private string _authorSearchText = string.Empty;

        private readonly ModService _modService;
        private readonly ModGroupService _groupService;
        private readonly FactorioApiService _apiService;
        private readonly ModMetadataService _metadataService;
        private readonly SettingsService _settingsService;

        private static readonly char[] DependencySeparators = [' ', '>', '<', '=', '!', '?', '('];

        public MainWindowVM()
        {
            _mods = [];
            _groups = [];
            _selectedMods = [];

            _modService = new ModService();
            _groupService = new ModGroupService();
            _apiService = new FactorioApiService();
            _metadataService = new ModMetadataService();
            _settingsService = new SettingsService();

            InitializeCommands();
            SetupObservables();
        }

        // Properties
        public ObservableCollection<ModViewModel> Mods
        {
            get => _mods;
            set => this.RaiseAndSetIfChanged(ref _mods, value);
        }

        public ObservableCollection<ModViewModel> FilteredMods { get; } = [];

        public ObservableCollection<ModGroupViewModel> Groups
        {
            get => _groups;
            set => this.RaiseAndSetIfChanged(ref _groups, value);
        }

        public ObservableCollection<ModViewModel> SelectedMods
        {
            get => _selectedMods;
            set => this.RaiseAndSetIfChanged(ref _selectedMods, value);
        }

        public ObservableCollection<string> Authors { get; } = [];
        public ObservableCollection<string> FilteredAuthors { get; } = [];
        private Dictionary<string, int> _authorModCounts = [];

        public ModViewModel? SelectedMod
        {
            get => _selectedMod;
            set
            {
                var oldMod = _selectedMod;
                this.RaiseAndSetIfChanged(ref _selectedMod, value);

                if (value != null && oldMod != value)
                {
                    OnModSelected(value); // Call navigation tracking
                    _ = LoadThumbnailAsync(value);
                }
            }
        }

        public ModGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        public bool ShowDisabled
        {
            get => _showDisabled;
            set => this.RaiseAndSetIfChanged(ref _showDisabled, value);
        }

        public bool FilterBySelectedGroup
        {
            get => _filterBySelectedGroup;
            set => this.RaiseAndSetIfChanged(ref _filterBySelectedGroup, value);
        }

        public string? SelectedAuthorFilter
        {
            get => _selectedAuthorFilter;
            set => this.RaiseAndSetIfChanged(ref _selectedAuthorFilter, value);
        }

        public string AuthorSearchText
        {
            get => _authorSearchText;
            set => this.RaiseAndSetIfChanged(ref _authorSearchText, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public string ModCountText => $"Mods: {FilteredMods.Count} / {Mods.Count}";

        private void SetupObservables()
        {
            // Set up filtering
            this.WhenAnyValue(
                x => x.SearchText,
                x => x.ShowDisabled,
                x => x.SelectedAuthorFilter,
                x => x.SelectedGroup,
                x => x.FilterBySelectedGroup)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredMods());

            // Filter authors when search text changes
            this.WhenAnyValue(x => x.AuthorSearchText)
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredAuthors());

            // Sync AuthorSearchText when SelectedAuthorFilter changes
            this.WhenAnyValue(x => x.SelectedAuthorFilter)
                .Subscribe(author =>
                {
                    if (!string.IsNullOrEmpty(author))
                    {
                        AuthorSearchText = author;
                    }
                });
        }
    }
}
