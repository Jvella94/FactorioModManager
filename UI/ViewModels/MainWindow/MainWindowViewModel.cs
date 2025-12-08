using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<ModViewModel> _mods;
        private ObservableCollection<ModGroupViewModel> _groups;
        private ObservableCollection<ModViewModel> _selectedMods;
        private ObservableCollection<string> _installedVersions = new();
        private ModViewModel? _selectedMod;
        private ModGroupViewModel? _selectedGroup;
        private string _searchText = string.Empty;
        private bool _showDisabled = true;
        private bool _filterBySelectedGroup = false;
        private string _statusText = "Ready";
        private string? _selectedAuthorFilter;
        private string _authorSearchText = string.Empty;

        private readonly IModService _modService;
        private readonly IModGroupService _groupService;
        private readonly IFactorioApiService _apiService;
        private readonly IModMetadataService _metadataService;
        private readonly ISettingsService _settingsService;
        private readonly IUIService _uiService;
        private readonly ILogService _logService;

        public MainWindowViewModel(
            IModService modService,
            IModGroupService groupService,
            IFactorioApiService apiService,
            IModMetadataService metadataService,
            ISettingsService settingsService,
            IUIService uiService,
            ILogService logService)
        {
            _mods = [];
            _groups = [];
            _selectedMods = [];

            _modService = modService;
            _groupService = groupService;
            _apiService = apiService;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _uiService = uiService;
            _logService = logService;

            InitializeCommands();
            SetupObservables();
        }

        // ... rest of the properties remain the same

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
                    OnModSelected(value);
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
        
        public string ModCountSummary
        {
            get
            {
                var enabled = Mods.Count(m => m.IsEnabled);
                var total = Mods.Count;
                var updates = Mods.Count(m => m.HasUpdate);
                return $"Enabled: {enabled}/{total} | Updates: {updates}";
            }
        }

        public ObservableCollection<string> InstalledVersions
        {
            get => _installedVersions;
            set => this.RaiseAndSetIfChanged(ref _installedVersions, value);
        }

        private async Task RefreshSelectedModVersions()
        {
            if (SelectedMod != null)
            {
                var versions = ServiceContainer.Instance
                    .Resolve<IModService>()
                    .GetInstalledVersions(SelectedMod.Name);

                InstalledVersions.Clear();
                foreach (var version in versions.OrderByDescending(v => v))
                {
                    InstalledVersions.Add(version);
                }
            }
        }

        public string UnusedInternalWarning => $"⚠ {UnusedInternalCount} unused internal dependencies";
        public int UnusedInternalCount => Mods.Count(m => m.IsUnusedInternal);
        public bool HasUnusedInternals => UnusedInternalCount > 0;

        private void SetupObservables()
        {
            this.WhenAnyValue(
                x => x.SearchText,
                x => x.ShowDisabled,
                x => x.SelectedAuthorFilter,
                x => x.SelectedGroup,
                x => x.FilterBySelectedGroup)
                .Throttle(TimeSpan.FromMilliseconds(Constants.Throttle.SearchMs))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredMods());

            this.WhenAnyValue(x => x.AuthorSearchText)
                .Throttle(TimeSpan.FromMilliseconds(Constants.Throttle.AuthorSearchMs))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredAuthors());

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
