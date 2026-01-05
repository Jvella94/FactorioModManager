using Avalonia.Controls;
using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly CompositeDisposable _disposables = [];

        // ✅ Simple collections instead of DynamicData
        private readonly ObservableCollection<ModViewModel> _allMods = [];

        private readonly ObservableCollection<ModViewModel> _filteredMods = [];
        private readonly ObservableCollection<string> _authors = [];
        private readonly ObservableCollection<string> _filteredAuthors = [];

        // Custom mod lists (user snapshots)
        private ObservableCollection<CustomModList> _modLists = [];

        public ObservableCollection<CustomModList> ModLists
        {
            get => _modLists;
            set => this.RaiseAndSetIfChanged(ref _modLists, value);
        }

        private ObservableCollection<ModGroupViewModel> _groups = [];
        private ObservableCollection<ModViewModel> _selectedMods = [];

        private ModViewModel? _selectedMod;
        private ModGroupViewModel? _selectedGroup;
        public ModManagementViewModel ModManagement { get; }
        private string _searchText = string.Empty;
        private string _statusText = "Ready";
        private string? _selectedAuthorFilter;
        private string _authorSearchText = string.Empty;
        private bool _showOnlyUnusedInternals = false;
        private bool _showOnlyPendingUpdates = false;
        private bool _showCategoryColumn = true;
        private bool _showSizeColumn = true;
        private bool _togglingMod = false;
        private bool _areGroupsVisible = true;
        private double _groupsColumnWidth = 200.0;

        // Debounce for ApplyModFilter to avoid frequent re-filtering under heavy updates
        private static readonly TimeSpan _applyFilterDebounce = TimeSpan.FromMilliseconds(150);

        private ProgressTimerHelper? _applyFilterTimerHelper;
        private volatile bool _applyFilterPending = false;

        public bool ShowCategoryColumn
        {
            get => _showCategoryColumn;
            set => this.RaiseAndSetIfChanged(ref _showCategoryColumn, value);
        }

        public bool ShowSizeColumn
        {
            get => _showSizeColumn;
            set => this.RaiseAndSetIfChanged(ref _showSizeColumn, value);
        }

        public bool AreGroupsVisible
        {
            get => _areGroupsVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref _areGroupsVisible, value);
                // Effective column width depends on visibility
                this.RaisePropertyChanged(nameof(EffectiveGroupsColumnGridLength));
            }
        }

        public double GroupsColumnWidth
        {
            get => _groupsColumnWidth;
            set
            {
                this.RaiseAndSetIfChanged(ref _groupsColumnWidth, value);
                // notify GridLength wrapper property as well
                this.RaisePropertyChanged(nameof(GroupsColumnGridLength));
                this.RaisePropertyChanged(nameof(EffectiveGroupsColumnGridLength));
            }
        }

        // Bindable GridLength wrapper for XAML ColumnDefinition.Width
        public GridLength GroupsColumnGridLength
        {
            get => new(GroupsColumnWidth, GridUnitType.Pixel);
            set
            {
                // avoid recursion if same
                if (Math.Abs(GroupsColumnWidth - value.Value) < 0.5)
                    return;
                GroupsColumnWidth = value.Value;
                this.RaisePropertyChanged(nameof(GroupsColumnGridLength));
            }
        }

        // New: effective grid length that collapses when groups hidden
        public GridLength EffectiveGroupsColumnGridLength => AreGroupsVisible
            ? new GridLength(GroupsColumnWidth, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

        // New: effective splitter column width (6 when visible, 0 when hidden)
        public GridLength EffectiveSplitterGridLength => AreGroupsVisible
            ? new GridLength(6, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);

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
        private readonly IAppUpdateChecker _appUpdateChecker;
        private readonly IDependencyFlow _dependencyFlow;
        private readonly IModVersionManager _modVersionManager;
        private readonly IFactorioLauncher _factorioLauncher;
        private readonly IThumbnailCache _thumbnailCache;
        private readonly IModFilterService _modFilterService;
        private readonly IModListService _modListService;

        // Expose the concrete DownloadProgressViewModel for XAML binding to presentation properties
        public DownloadProgressViewModel DownloadProgress { get; }

        public MainWindowViewModel(
            IModService modService,
            IModGroupService groupService,
            IFactorioApiService apiService,
            IModMetadataService metadataService,
            ISettingsService settingsService,
            IUIService uiService,
            ILogService logService,
            IDownloadService downloadService,
            IErrorMessageService errorMessageService,
            IAppUpdateChecker appUpdateChecker,
            IDependencyFlow dependencyFlow,
            IModVersionManager modVersionManager,
            IFactorioLauncher factorioLauncher,
            IThumbnailCache thumbnailCache,
            IModFilterService modFilterService,
            IModListService modListService,
            IDownloadProgress downloadProgress)
        {
            _modListService = modListService;
            _modService = modService;
            _groupService = groupService;
            _apiService = apiService;
            _metadataService = metadataService;
            _settingsService = settingsService;
            _uiService = uiService;
            _logService = logService;
            _downloadService = downloadService;
            _errorMessageService = errorMessageService;
            _appUpdateChecker = appUpdateChecker;
            _dependencyFlow = dependencyFlow;
            _modVersionManager = modVersionManager;
            _factorioLauncher = factorioLauncher;
            _thumbnailCache = thumbnailCache;
            _modFilterService = modFilterService;
            DownloadProgress = downloadProgress as DownloadProgressViewModel ?? throw new InvalidOperationException("DownloadProgress must be a DownloadProgressViewModel");
            ModManagement = new ModManagementViewModel();

            // Load persisted groups visibility
            try
            {
                AreGroupsVisible = _settingsService.GetShowGroupsPanel();
                // Load persisted column width
                GroupsColumnWidth = _settingsService.GetGroupsColumnWidth();
            }
            catch { }

            // Load persisted column visibility for Category and Size
            try
            {
                ShowCategoryColumn = _settingsService.GetShowCategoryColumn();
                ShowSizeColumn = _settingsService.GetShowSizeColumn();
            }
            catch { }

            // Watch for settings changes to update visibility
            try
            {
                _settingsService.ShowGroupsPanelChanged += () =>
                {
                    AreGroupsVisible = _settingsService.GetShowGroupsPanel();
                    // also refresh stored width when settings indicate change
                    try { GroupsColumnWidth = _settingsService.GetGroupsColumnWidth(); } catch { }
                };
            }
            catch { }

            // Ensure ShowHiddenDependencies propagates to existing mod VMs
            try
            {
                // Initialize existing mods later when they are created via UpdateModsCache.
                _settingsService.ShowHiddenDependenciesChanged += () =>
                {
                    try
                    {
                        var val = _settingsService.GetShowHiddenDependencies();
                        // preserve selection name before changing lists
                        var prevName = SelectedMod?.Name;
                        foreach (var m in _allMods)
                        {
                            m.ShowHiddenDependencies = val;
                        }

                        // Reapply filter so selection is restored consistently
                        ScheduleApplyModFilter();

                        // If ApplyModFilter didn't restore selection, try to restore from all mods
                        if (!string.IsNullOrEmpty(prevName) && SelectedMod == null)
                        {
                            var fallback = _allMods.FirstOrDefault(x => x.Name.Equals(prevName, StringComparison.OrdinalIgnoreCase));
                            if (fallback != null)
                            {
                                SelectedMod = fallback;
                            }
                        }
                    }
                    catch { }
                };
            }
            catch { }

            // Watch for changes to category/size column visibility
            try
            {
                _settingsService.ShowCategoryColumnChanged += () => ShowCategoryColumn = _settingsService.GetShowCategoryColumn();
                _settingsService.ShowSizeColumnChanged += () => ShowSizeColumn = _settingsService.GetShowSizeColumn();
            }
            catch { }

            SetupReactiveFiltering();
            InitializeCommands();

            // Load saved custom mod lists
            try
            {
                var lists = _modListService.LoadLists();
                foreach (var l in lists) ModLists.Add(l);
            }
            catch { }

            _settingsService.FactorioPathChanged += () => DetectFactorioVersionAndDLC();

            // react to mods path changes (reload lightweight)
            _settingsService.ModsPathChanged += async () =>
            {
                await RefreshModsAsync();
            };

            // react to data path changes (re-detect Factorio version / DLC)
            _settingsService.FactorioDataPathChanged += () => DetectFactorioVersionAndDLC();
        }

        /// <summary>
        /// Schedule a debounced ApplyModFilter() call.
        /// Multiple calls within the debounce window coalesce into a single filter application.
        /// </summary>
        private void ScheduleApplyModFilter()
        {
            _applyFilterPending = true;
            try
            {
                _applyFilterTimerHelper ??= new ProgressTimerHelper(_applyFilterDebounce, FlushApplyFilterToUi);
                _applyFilterTimerHelper.Schedule();
            }
            catch { }
        }

        private void FlushApplyFilterToUi()
        {
            if (!_applyFilterPending)
                return;

            _applyFilterPending = false;
            _uiService.Post(() =>
            {
                try
                {
                    ApplyModFilter();
                }
                catch { }
            });
        }

        /// <summary>
        /// Sets up reactive filtering with throttling
        /// </summary>
        private void SetupReactiveFiltering()
        {
            // Throttle search text changes
            this.WhenAnyValue(
                x => x.SearchText,
                x => x.SelectedAuthorFilter,
                x => x.ActiveFilterGroup,
                x => x.ShowOnlyUnusedInternals,
                x => x.ShowOnlyPendingUpdates)
                .Throttle(TimeSpan.FromMilliseconds(150))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ScheduleApplyModFilter())
                .DisposeWith(_disposables);

            // Throttle author search
            this.WhenAnyValue(x => x.AuthorSearchText)
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyAuthorFilter())
                .DisposeWith(_disposables);

            // Update HasSelectedMod
            this.WhenAnyValue(x => x.SelectedMod)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(HasSelectedMod)))
                .DisposeWith(_disposables);

            // Sync author search with selected author
            this.WhenAnyValue(x => x.SelectedAuthorFilter)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(author =>
                {
                    if (!string.IsNullOrEmpty(author))
                    {
                        AuthorSearchText = author;
                    }
                })
                .DisposeWith(_disposables);

            _allMods.CollectionChanged += (s, e) =>
            {
                // When collection changes, subscribe to new items
                if (e.NewItems != null)
                {
                    foreach (ModViewModel mod in e.NewItems)
                    {
                        mod.WhenAnyValue(
                            x => x.IsEnabled,
                            x => x.HasUpdate,
                            x => x.IsUnusedInternal)
                            .Throttle(TimeSpan.FromMilliseconds(100))
                            .ObserveOn(RxApp.MainThreadScheduler)
                            .Subscribe(_ =>
                            {
                                this.RaisePropertyChanged(nameof(EnabledCountText));
                                this.RaisePropertyChanged(nameof(UpdatesAvailableCount));
                                this.RaisePropertyChanged(nameof(HasUpdates));
                                this.RaisePropertyChanged(nameof(UpdatesCountText));
                                this.RaisePropertyChanged(nameof(UnusedInternalCount));
                                this.RaisePropertyChanged(nameof(HasUnusedInternals));
                                this.RaisePropertyChanged(nameof(UnusedInternalWarning));

                                // Reapply filter when relevant flags change so the filtered collection updates
                                // only when filter depends on those flags (avoid unnecessary re-filtering).
                                if (ShowOnlyPendingUpdates || ShowOnlyUnusedInternals)
                                {
                                    ScheduleApplyModFilter();
                                }
                            })
                            .DisposeWith(_disposables);
                    }
                }
            };

            // If the global HasUpdates becomes false while the Pending Updates filter is active, clear it
            this.WhenAnyValue(x => x.HasUpdates)
                .Throttle(TimeSpan.FromMilliseconds(50))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(hasUpdates =>
                {
                    try
                    {
                        if (!hasUpdates && ShowOnlyPendingUpdates)
                        {
                            ShowOnlyPendingUpdates = false;
                            SetStatus("No pending updates remain. Pending updates filter cleared.");
                        }
                    }
                    catch { }
                })
                .DisposeWith(_disposables);
        }

        /// <summary>
        /// Apply filtering to mods collection
        /// </summary>
        /// <summary>
        /// Apply filtering to mods collection
        /// </summary>
        private void ApplyModFilter()
        {
            var filtered = _modFilterService.ApplyFilter(_allMods,
                _searchText,
                _selectedAuthorFilter,
                _activeFilterGroup,
                _showOnlyUnusedInternals,
                _showOnlyPendingUpdates);

            // ✅ Preserve selection by mod name (handles VM instances being recreated on reload)
            var previousSelectedName = SelectedMod?.Name;

            _filteredMods.Clear();
            foreach (var mod in filtered)
            {
                _filteredMods.Add(mod);
            }

            // Try to restore selection by name
            if (!string.IsNullOrEmpty(previousSelectedName))
            {
                var newSelected = _filteredMods.FirstOrDefault(m => m.Name.Equals(previousSelectedName, StringComparison.OrdinalIgnoreCase));
                if (newSelected != null)
                {
                    // Use property setter so side-effects (OnModSelected, thumbnail load) run
                    SelectedMod = newSelected;
                    // Ensure thumbnail load is triggered even if setter didn't (defensive)
                    _ = LoadThumbnailAsync(newSelected);
                }
                else
                {
                    // If previously selected mod was filtered out, clear selection
                    if (SelectedMod != null && !_filteredMods.Contains(SelectedMod))
                    {
                        SelectedMod = null;
                    }
                }
            }
            else
            {
                // No previous selection - ensure we clear if selection not in filtered
                if (SelectedMod != null && !_filteredMods.Contains(SelectedMod))
                {
                    SelectedMod = null;
                }
            }

            this.RaisePropertyChanged(nameof(ModCountText));
            this.RaisePropertyChanged(nameof(EnabledCountText));
            this.RaisePropertyChanged(nameof(UpdatesAvailableCount));
            this.RaisePropertyChanged(nameof(HasUpdates));
            this.RaisePropertyChanged(nameof(UpdatesCountText));
        }

        /// <summary>
        /// Apply filtering to authors collection
        /// </summary>
        private void ApplyAuthorFilter()
        {
            if (string.IsNullOrEmpty(_authorSearchText))
            {
                _filteredAuthors.Clear();
                foreach (var author in _authors)
                {
                    _filteredAuthors.Add(author);
                }
                return;
            }

            var filtered = _authors
                .Where(a => a.Contains(_authorSearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _filteredAuthors.Clear();
            foreach (var author in filtered)
            {
                _filteredAuthors.Add(author);
            }
        }

        public async void InitializeStartupTasks()
        {
            await Task.Run(() => DetectFactorioVersionAndDLC());

            if (_settingsService.GetCheckForAppUpdates())
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    await CheckForAppUpdatesAsync();
                });
            }
        }

        private void DetectFactorioVersionAndDLC()
        {
            try
            {
                var (version, hasSpaceAge) = _factorioLauncher.DetectVersionAndDLC();

                if (!string.IsNullOrEmpty(version))
                {
                    _settingsService.SetFactorioVersion(version);
                    _logService.Log($"Detected Factorio version: {version}");
                }

                _settingsService.SetHasSpaceAgeDlc(hasSpaceAge);
                this.RaisePropertyChanged(nameof(FactorioVersion));
                this.RaisePropertyChanged(nameof(FactorioVersionText));
                this.RaisePropertyChanged(nameof(HasFactorioVersion));
                this.RaisePropertyChanged(nameof(HasSpaceAgeDlc));
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Error detecting Factorio DLC/version: {ex.Message}");
                SetStatus("Error detecting Factorio version or DLC.", LogLevel.Warning);
            }
        }

        private async Task CheckForAppUpdatesAsync()
        {
            try
            {
                var currentVersion = "1.0.0"; // Replace with Assembly.GetExecutingAssembly().GetName().Version.ToString()
                var updateInfo = await _appUpdateChecker.CheckForUpdatesAsync(currentVersion);

                if (updateInfo?.IsNewer == true)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"New version {updateInfo.Version} available! Check Settings → Help.", LogLevel.Info);
                        _uiService.ShowMessageAsync("Update Available",
                            $"A new version {updateInfo.Version} of Factorio Mod Manager is available!\n\n" +
                            $"Release notes: {updateInfo.HtmlUrl}");
                    });
                }

                _settingsService.SetLastAppUpdateCheck(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logService.LogError("App update check failed", ex);
            }
        }

        // Properties
        public ObservableCollection<ModViewModel> FilteredMods => _filteredMods;

        public ObservableCollection<string> Authors => _authors;
        public ObservableCollection<string> FilteredAuthors => _filteredAuthors;
        public int AllModsCount => _allMods.Count;

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

                    // Update SourceUrl using metadata service
                    value.SourceUrl = _metadataService.GetSourceUrl(value.Name);
                    // Populate InstalledDependencies as (Name, InstalledVersion) tuples
                    try
                    {
                        var installedDeps = new List<(string Name, string? InstalledVersion)>();
                        foreach (var raw in value.Dependencies)
                        {
                            var name = DependencyHelper.ExtractDependencyName(raw);
                            if (string.IsNullOrEmpty(name)) continue;
                            var ver = _modVersionManager?.GetInstalledVersions(name).FirstOrDefault();
                            if (!string.IsNullOrEmpty(ver))
                                installedDeps.Add((name, ver));
                        }
                        value.InstalledDependencies = installedDeps;
                    }
                    catch { }
                }
            }
        }

        private CustomModList? _selectedModList;

        public CustomModList? SelectedModList
        {
            get => _selectedModList;
            set => this.RaiseAndSetIfChanged(ref _selectedModList, value);
        }

        /// <summary>
        /// Gets the detected Factorio version
        /// </summary>
        public string? FactorioVersion => _settingsService.GetFactorioVersion();

        /// <summary>
        /// Gets whether Space Age DLC is detected
        /// </summary>
        public bool HasSpaceAgeDlc => _settingsService.GetHasSpaceAgeDLC();

        /// <summary>
        /// Gets the formatted Factorio version display text
        /// </summary>
        public string FactorioVersionText
        {
            get
            {
                var version = FactorioVersion;
                return string.IsNullOrEmpty(version) ? "" : $"{version}";
            }
        }

        /// <summary>
        /// Gets whether to show Factorio version info in status bar
        /// </summary>
        public bool HasFactorioVersion => !string.IsNullOrEmpty(FactorioVersion);

        public ModGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
        }

        private ModGroupViewModel? _activeFilterGroup;

        public ModGroupViewModel? ActiveFilterGroup
        {
            get => _activeFilterGroup;
            set
            {
                var old = _activeFilterGroup;
                this.RaiseAndSetIfChanged(ref _activeFilterGroup, value);

                // Update IsActiveFilter flag on groups for UI overlay
                try
                {
                    old?.IsActiveFilter = false;
                    _activeFilterGroup?.IsActiveFilter = true;
                }
                catch { }

                // notify filter change
                this.RaisePropertyChanged(nameof(ActiveFilterGroup));

                // Reapply filters when active filter changes
                ScheduleApplyModFilter();
            }
        }

        // Public toggle used from UI double-click handler
        public void ToggleActiveFilter(ModGroupViewModel? group)
        {
            if (group == null)
                ActiveFilterGroup = null;
            else if (ActiveFilterGroup == group)
                ActiveFilterGroup = null;
            else
                ActiveFilterGroup = group;
        }

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
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

        public string EnabledCountText
        {
            get
            {
                var enabled = _allMods.Count(m => m.IsEnabled);
                var total = _allMods.Count;
                return $"Enabled: {enabled}/{total}";
            }
        }

        public int UpdatesAvailableCount => _allMods.Count(m => m.HasUpdate);

        public bool HasUpdates => UpdatesAvailableCount > 0;

        public string UpdatesCountText => HasUpdates ? $"Updates: {UpdatesAvailableCount}" : string.Empty;

        public string UnusedInternalWarning => $"⚠ {UnusedInternalCount} unused internal dependencies";
        public int UnusedInternalCount => _allMods.Count(m => m.IsUnusedInternal);
        public bool HasUnusedInternals => UnusedInternalCount > 0;

        public bool ShowOnlyUnusedInternals
        {
            get => _showOnlyUnusedInternals;
            set => this.RaiseAndSetIfChanged(ref _showOnlyUnusedInternals, value);
        }

        public bool ShowOnlyPendingUpdates
        {
            get => _showOnlyPendingUpdates;
            set => this.RaiseAndSetIfChanged(ref _showOnlyPendingUpdates, value);
        }

        // Create a named snapshot of current mod enabled states
        private void CreateModList()
        {
            var name = $"New List {ModLists.Count + 1}";
            var list = new CustomModList { Name = name, Description = "Snapshot" };
            foreach (var m in _allMods)
            {
                list.Entries.Add(new ModListEntry { Name = m.Name, Enabled = m.IsEnabled, Version = m.Version });
            }
            _modListService.AddList(list);
            ModLists.Add(list);
            SetStatus($"Created mod list: {name}");
        }

        private async Task ApplyModList(string name)
        {
            var list = ModLists.FirstOrDefault(l => l.Name == name);
            if (list == null) return;

            // Build preview items
            var previewItems = new List<(string Name, string Title, bool CurrentEnabled, bool TargetEnabled, string? CurrentVersion, string? TargetVersion, List<string> InstalledVersions)>();
            foreach (var vm in _allMods)
            {
                var entry = list.Entries.FirstOrDefault(e => e.Name.Equals(vm.Name, StringComparison.OrdinalIgnoreCase));
                var target = entry?.Enabled ?? false;
                var targetVersion = entry?.Version;
                var installedVersions = new List<string>();
                try { installedVersions = [.. _modVersionManager.GetInstalledVersions(vm.Name)]; } catch { }

                previewItems.Add((vm.Name, vm.Title, vm.IsEnabled, target, vm.Version, targetVersion, installedVersions));
            }

            // Show preview dialog on UI thread
            var owner = _uiService.GetMainWindow();

            // Build strongly-typed preview items for UI service
            var previewModels = new List<ModListPreviewItem>();
            foreach (var vm in _allMods)
            {
                var entry = list.Entries.FirstOrDefault(e => e.Name.Equals(vm.Name, StringComparison.OrdinalIgnoreCase));
                var target = entry?.Enabled ?? false;
                var targetVersion = entry?.Version;
                var installedVersions = new List<string>();
                try { installedVersions = [.. _modVersionManager.GetInstalledVersions(vm.Name)]; } catch { }

                previewModels.Add(new ModListPreviewItem
                {
                    Name = vm.Name,
                    Title = vm.Title,
                    CurrentEnabled = vm.IsEnabled,
                    TargetEnabled = target,
                    CurrentVersion = vm.Version,
                    TargetVersion = targetVersion,
                    InstalledVersions = installedVersions
                });
            }

            var result = await _uiService.ShowModListPreviewAsync(previewModels, name, owner);
            if (result == null) return; // cancelled

            // Prepare activation candidates (name, version) for confirmation UI
            var activationCandidates = result.Where(r => !string.IsNullOrEmpty(r.ApplyVersion) && r.ApplyEnabled)
                                             .Select(r => (r.Name, Version: r.ApplyVersion!))
                                             .ToList();

            var skipActivations = false;
            HashSet<string>? allowedActivations = null;

            if (activationCandidates.Count > 0)
            {
                // If Factorio is running, offer to apply without activations to avoid corrupting active files
                try
                {
                    if (_factorioLauncher != null && _factorioLauncher.IsFactorioRunning())
                    {
                        var proceed = await _uiService.ShowConfirmationAsync(
                            "Factorio is running",
                            "Factorio appears to be running. Active version changes require Factorio to be closed.\n\nChoose 'Apply without activations' to apply enabled/disabled changes only, or 'Cancel' to abort.",
                            owner,
                            yesButtonText: "Apply without activations",
                            noButtonText: "Cancel",
                            yesButtonColor: "#FFA000",
                            noButtonColor: "#3A3A3A");

                        if (!proceed)
                            return;

                        // User chose to proceed but without activations
                        skipActivations = true;
                    }
                }
                catch { }

                // If activations are allowed and there are multiple, show detailed checkbox dialog
                if (!skipActivations && activationCandidates.Count > 1)
                {
                    var header = "The following activations will be applied. Uncheck any you don't want to activate:";
                    var lines = string.Join("\n", activationCandidates.Select(a => $"- {a.Name}@{a.Version}"));
                    var message = header + "\n\n" + lines;

                    var selected = await _uiService.ShowActivationConfirmationAsync(
                        "Confirm Activate Versions",
                        message,
                        activationCandidates,
                        owner);

                    if (selected == null)
                        return; // cancelled

                    allowedActivations = new HashSet<string>(selected.Select(s =>
                    {
                        var at = s.IndexOf('@');
                        return at >= 0 ? s[..at] : s;
                    }), StringComparer.OrdinalIgnoreCase);
                }
                else if (!skipActivations && activationCandidates.Count == 1)
                {
                    // Single activation allowed automatically
                    allowedActivations = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { activationCandidates[0].Name };
                }
            }

            // Apply according to user's selections
            foreach (var r in result)
            {
                var vm = _allMods.FirstOrDefault(m => m.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase));
                if (vm == null) continue;

                // Apply enabled state
                if (vm.IsEnabled != r.ApplyEnabled)
                {
                    vm.IsEnabled = r.ApplyEnabled;
                    _modService.ToggleMod(vm.Name, r.ApplyEnabled);
                }

                // Apply version if requested (and non-null) AND the target is enabled
                if (!string.IsNullOrEmpty(r.ApplyVersion) && r.ApplyEnabled)
                {
                    try
                    {
                        // Set SelectedMod so SetActiveVersion operates on correct VM
                        SelectedMod = vm;

                        // Only persist/apply version if activations aren't skipped and this mod was approved in the activation dialog (if shown)
                        var shouldApplyVersion = !skipActivations && (allowedActivations == null || allowedActivations.Contains(vm.Name));
                        if (shouldApplyVersion)
                        {
                            _modService.SaveModState(vm.Name, enabled: true, version: r.ApplyVersion);

                            // Activate the selected version (updates FilePath, Version, etc.)
                            try { await SetActiveVersion(r.ApplyVersion); } catch { }
                        }
                    }
                    catch { }
                }
                else if (!string.IsNullOrEmpty(r.ApplyVersion) && !r.ApplyEnabled)
                {
                    // If target is disabled but a version was supplied, still persist version if desired
                    try
                    {
                        _modService.SaveModState(vm.Name, enabled: false, version: r.ApplyVersion);
                    }
                    catch { }
                }
            }

            SetStatus($"Applied mod list: {name}");
        }

        private void DeleteModList(string name)
        {
            _modListService.DeleteList(name);
            var item = ModLists.FirstOrDefault(l => l.Name == name);
            if (item != null) ModLists.Remove(item);
            SetStatus($"Deleted mod list: {name}");
        }

        private void RenameModList(string oldName, string newName)
        {
            var item = ModLists.FirstOrDefault(l => l.Name == oldName);
            if (item == null) return;
            var updated = new CustomModList { Name = newName, Description = item.Description, Entries = item.Entries };
            _modListService.UpdateList(oldName, updated);
            item.Name = newName;
            SetStatus($"Renamed mod list from '{oldName}' to '{newName}'");
        }

        private static void StartRenameModList(CustomModList? list)
        {
            if (list == null) return;
            list.IsRenaming = true;
            list.EditedName = list.Name;
        }

        private void ConfirmRenameModList(CustomModList? list)
        {
            if (list == null) return;
            if (string.IsNullOrWhiteSpace(list.EditedName)) return;

            var oldName = list.Name;
            var newName = list.EditedName.Trim();
            if (oldName == newName)
            {
                list.IsRenaming = false;
                return;
            }

            RenameModList(oldName, newName);
            list.IsRenaming = false;
        }

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
            var userMessage = _errorMessageService.GetUserFriendlyMessage(ex, context);
            SetStatus(userMessage, LogLevel.Error);
            _logService.LogError(context, ex);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables?.Dispose();
                _navigationHistory.Clear();
                SelectedMods.Clear();

                // Don't dispose mods here - let GC handle it
                _allMods.Clear();
                _filteredMods.Clear();

                try { _applyFilterTimerHelper?.Dispose(); } catch { }

                foreach (var group in Groups)
                {
                    group?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}