using DynamicData;
using DynamicData.Binding;
using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        // ✅ NEW: Source cache for mods
        private readonly SourceCache<ModViewModel, string> _modsCache;

        private ReadOnlyObservableCollection<ModViewModel> _filteredMods = null!;
        private ReadOnlyObservableCollection<string> _authors = null!;
        private ReadOnlyObservableCollection<string> _filteredAuthors = null!;

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

        public bool HasSelectedMod => SelectedMod != null;

        private readonly IModService _modService;
        private readonly IModGroupService _groupService;
        private readonly IFactorioApiService _apiService;
        private readonly IModMetadataService _metadataService;
        private readonly ISettingsService _settingsService;
        private readonly IUIService _uiService;
        private readonly ILogService _logService;
        private readonly IDownloadService _downloadService;
        private readonly IErrorMessageService _errorMessageService;

        private readonly Dictionary<string, int> _authorModCounts = [];

        public MainWindowViewModel(
            IModService modService,
            IModGroupService groupService,
            IFactorioApiService apiService,
            IModMetadataService metadataService,
            ISettingsService settingsService,
            IUIService uiService,
            ILogService logService,
            IDownloadService downloadService,
            IErrorMessageService errorMessageService)
        {
            _modService = modService;
            _groupService = groupService;
            _apiService = apiService;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _uiService = uiService;
            _logService = logService;
            _downloadService = downloadService;
            _errorMessageService = errorMessageService;

            // ✅ Initialize source cache (keyed by mod name)
            _modsCache = new SourceCache<ModViewModel, string>(mod => mod.Name);

            _groups = [];
            _selectedMods = [];

            SetupDynamicData();
            InitializeCommands();
        }

        /// <summary>
        /// Sets up DynamicData reactive pipelines
        /// </summary>
        /// <summary>
        /// Sets up DynamicData reactive pipelines
        /// </summary>
        private void SetupDynamicData()
        {
            // ✅ Filtered and sorted mods pipeline - ADD ObserveOn BEFORE SortAndBind
            _modsCache
                .Connect()
                .Filter(this.WhenAnyValue(
                    x => x.SearchText,
                    x => x.ShowDisabled,
                    x => x.SelectedAuthorFilter,
                    x => x.SelectedGroup,
                    x => x.FilterBySelectedGroup)
                    .Throttle(TimeSpan.FromMilliseconds(Constants.Throttle.SearchMs))
                    .Select(_ => CreateModFilterPredicate()))
                .ObserveOn(RxApp.MainThreadScheduler) // ✅ ADD THIS - Marshal to UI thread
                .SortAndBind(
                    out _filteredMods,
                    SortExpressionComparer<ModViewModel>.Descending(m => m.LastUpdated ?? DateTime.MinValue))
                .DisposeMany()
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(ModCountText));
                });

            // ✅ Authors pipeline - ADD ObserveOn BEFORE SortAndBind
            _modsCache
                .Connect()
                .Transform(mod => mod.Author)
                .Filter(author => !string.IsNullOrEmpty(author))
                .GroupWithImmutableState(author => author!)
                .Transform(group => $"{group.Key} ({group.Count})")
                .ObserveOn(RxApp.MainThreadScheduler) // ✅ ADD THIS - Marshal to UI thread
                .SortAndBind(
                    out _authors,
                    SortExpressionComparer<string>.Descending(a => ExtractAuthorCount(a)))
                .Subscribe();

            // ✅ Filtered authors pipeline - ADD ObserveOn BEFORE Bind
            Observable.Return(_authors)
                .Select(_ => _authors.AsObservableChangeSet())
                .Switch()
                .Filter(this.WhenAnyValue(x => x.AuthorSearchText)
                    .Throttle(TimeSpan.FromMilliseconds(Constants.Throttle.AuthorSearchMs))
                    .Select(searchText => CreateAuthorFilterPredicate(searchText)))
                .ObserveOn(RxApp.MainThreadScheduler) // ✅ ADD THIS - Marshal to UI thread
                .Bind(out _filteredAuthors)
                .Subscribe();

            // ✅ Sync author search with selected author
            this.WhenAnyValue(x => x.SelectedAuthorFilter)
                .ObserveOn(RxApp.MainThreadScheduler) // ✅ Ensure UI thread
                .Subscribe(author =>
                {
                    if (!string.IsNullOrEmpty(author))
                    {
                        AuthorSearchText = author;
                    }
                });

            // ✅ Update HasSelectedMod
            this.WhenAnyValue(x => x.SelectedMod)
                .ObserveOn(RxApp.MainThreadScheduler) // ✅ Ensure UI thread
                .Subscribe(_ => this.RaisePropertyChanged(nameof(HasSelectedMod)));

            // ✅ Update ModCountSummary when mods change - Already has ObserveOn, keep it
            _modsCache
                .Connect()
                .WhenPropertyChanged(m => m.IsEnabled)
                .Merge(_modsCache.Connect().WhenPropertyChanged(m => m.HasUpdate))
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(ModCountSummary));
                    this.RaisePropertyChanged(nameof(UnusedInternalCount));
                    this.RaisePropertyChanged(nameof(HasUnusedInternals));
                    this.RaisePropertyChanged(nameof(UnusedInternalWarning));
                });
        }

        private static int ExtractAuthorCount(string authorWithCount)
        {
            var lastParenIndex = authorWithCount.LastIndexOf('(');
            if (lastParenIndex < 0)
                return 0;

            var countPart = authorWithCount[(lastParenIndex + 1)..].TrimEnd(')');

            if (int.TryParse(countPart, out var count))
                return count;

            return 0;
        }

        /// <summary>
        /// Creates filter predicate for mods based on current settings
        /// </summary>
        private Func<ModViewModel, bool> CreateModFilterPredicate()
        {
            var searchText = SearchText;
            var showDisabled = ShowDisabled;
            var selectedAuthorFilter = SelectedAuthorFilter;
            var selectedGroup = SelectedGroup;
            var filterByGroup = FilterBySelectedGroup;

            return mod =>
            {
                if (!showDisabled && !mod.IsEnabled)
                    return false;

                if (!string.IsNullOrEmpty(searchText) &&
                    !mod.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(selectedAuthorFilter))
                {
                    var authorName = ExtractAuthorName(selectedAuthorFilter);
                    if (mod.Author != authorName)
                        return false;
                }

                if (filterByGroup && selectedGroup != null)
                {
                    if (!selectedGroup.ModNames.Contains(mod.Title))
                        return false;
                }

                return true;
            };
        }

        /// <summary>
        /// Creates filter predicate for authors
        /// </summary>
        private static Func<string, bool> CreateAuthorFilterPredicate(string? searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return _ => true;

            var searchLower = searchText.ToLower();
            return author => author.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
        }

        // ✅ Properties now expose DynamicData collections
        public ReadOnlyObservableCollection<ModViewModel> FilteredMods => _filteredMods;

        public ReadOnlyObservableCollection<string> Authors => _authors;
        public ReadOnlyObservableCollection<string> FilteredAuthors => _filteredAuthors;

        // ✅ Expose all mods count
        public int AllModsCount => _modsCache.Count;

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

        public string ModCountText => $"Mods: {FilteredMods.Count} / {AllModsCount}";

        public string ModCountSummary
        {
            get
            {
                var allMods = _modsCache.Items.ToList();
                var enabled = allMods.Count(m => m.IsEnabled);
                var total = allMods.Count;
                var updates = allMods.Count(m => m.HasUpdate);
                return $"Enabled: {enabled}/{total} | Updates: {updates}";
            }
        }

        public string UnusedInternalWarning => $"⚠ {UnusedInternalCount} unused internal dependencies";

        public int UnusedInternalCount => _modsCache.Items.Count(m => m.IsUnusedInternal);

        public bool HasUnusedInternals => UnusedInternalCount > 0;

        /// <summary>
        /// Helper to set status text and log message with proper error handling
        /// </summary>
        private void SetStatus(string message, LogLevel level = LogLevel.Info)
        {
            StatusText = message;
            _logService.Log(message, level);
        }

        /// <summary>
        /// Handles exceptions with user-friendly messages
        /// </summary>
        private void HandleError(Exception ex, string context)
        {
            // Get user-friendly message
            var userMessage = _errorMessageService.GetUserFriendlyMessage(ex, context);
            SetStatus(userMessage, LogLevel.Error);

            // Log technical details
            var technicalMessage = _errorMessageService.GetTechnicalMessage(ex);
            _logService.LogError(technicalMessage, ex);
        }
    }
}