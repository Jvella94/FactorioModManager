using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;

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
        private bool _togglingMod = false;

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
        private readonly IModUpdateService _modUpdateService;
        private readonly IModDependencyResolver _modDependencyResolver;
        private readonly IModVersionManager _modVersionManager;
        private readonly IFactorioLauncher _factorioLauncher;
        private readonly IErrorMapper _errorMapper;
        private readonly IThumbnailCache _thumbnailCache;

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
            IModUpdateService modUpdateService,
            IModDependencyResolver modDependencyResolver,
            IModVersionManager modVersionManager,
            IFactorioLauncher factorioLauncher,
            IErrorMapper errorMapper,
            IThumbnailCache thumbnailCache)
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
            _appUpdateChecker = appUpdateChecker;
            _modUpdateService = modUpdateService;
            _modDependencyResolver = modDependencyResolver;
            _modVersionManager = modVersionManager;
            _factorioLauncher = factorioLauncher;
            _errorMapper = errorMapper;
            _thumbnailCache = thumbnailCache;
            ModManagement = new ModManagementViewModel();

            SetupReactiveFiltering();
            InitializeCommands();

            _settingsService.FactorioPathChanged += () => DetectFactorioVersionAndDLC();
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
                x => x.SelectedGroup,
                x => x.ShowOnlyUnusedInternals,
                x => x.ShowOnlyPendingUpdates)
                .Throttle(TimeSpan.FromMilliseconds(150))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => ApplyModFilter())
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
                                this.RaisePropertyChanged(nameof(ModCountSummary));
                                this.RaisePropertyChanged(nameof(UnusedInternalCount));
                                this.RaisePropertyChanged(nameof(HasUnusedInternals));
                                this.RaisePropertyChanged(nameof(UnusedInternalWarning));
                            })
                            .DisposeWith(_disposables);
                    }
                }
            };
        }

        /// <summary>
        /// Apply filtering to mods collection
        /// </summary>
        /// <summary>
        /// Apply filtering to mods collection
        /// </summary>
        private void ApplyModFilter()
        {
            var filtered = _allMods
                .Where(mod =>
                {
                    if (!string.IsNullOrEmpty(_searchText) &&
                        !mod.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (!string.IsNullOrEmpty(_selectedAuthorFilter))
                    {
                        var authorName = ExtractAuthorName(_selectedAuthorFilter);
                        if (mod.Author != authorName)
                            return false;
                    }

                    // Filter for unused internal mods
                    if (_showOnlyUnusedInternals && !mod.IsUnusedInternal)
                        return false;

                    // Filter for mods with pending updates
                    if (_showOnlyPendingUpdates && !mod.HasUpdate)
                        return false;

                    return true;
                })
                .OrderByDescending(m => m.LastUpdated ?? DateTime.MinValue)
                .ToList();

            // ✅ Preserve selection
            var currentSelection = SelectedMod;
            var wasInFiltered = currentSelection != null && _filteredMods.Contains(currentSelection);

            _filteredMods.Clear();
            foreach (var mod in filtered)
            {
                _filteredMods.Add(mod);
            }

            // ✅ Restore selection if the mod is still in filtered results
            if (currentSelection != null && filtered.Contains(currentSelection))
            {
                // Don't trigger OnModSelected again, just restore the selection
                _selectedMod = currentSelection;
                this.RaisePropertyChanged(nameof(SelectedMod));
            }
            else if (currentSelection != null && wasInFiltered)
            {
                // Selection was filtered out, clear it
                _selectedMod = null;
                this.RaisePropertyChanged(nameof(SelectedMod));
            }

            this.RaisePropertyChanged(nameof(ModCountText));
            this.RaisePropertyChanged(nameof(ModCountSummary));
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

        private static string ExtractAuthorName(string authorFilter)
        {
            var parenIndex = authorFilter.IndexOf('(');
            return parenIndex > 0 ? authorFilter[..parenIndex].Trim() : authorFilter.Trim();
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
                var exePath = _settingsService.GetFactorioExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    SetStatus("Factorio executable not found. Please configure the path in settings.", LogLevel.Warning);
                    return;
                }

                // Version detection
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                    _settingsService.SetFactorioVersion(fvi.ProductVersion);
                    _logService.Log($"Detected Factorio version: {fvi.ProductVersion}");

                    this.RaisePropertyChanged(nameof(FactorioVersion));
                    this.RaisePropertyChanged(nameof(FactorioVersionText));
                    this.RaisePropertyChanged(nameof(HasFactorioVersion));
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"Failed to read Factorio file version: {ex.Message}");
                    SetStatus("Failed to detect Factorio version.", LogLevel.Warning);
                }

                // DLC detection
                var exeDir = Path.GetDirectoryName(exePath);
                var binFolder = Path.GetDirectoryName(exeDir);
                var rootDir = Path.GetDirectoryName(binFolder);

                var dataDir = _settingsService.GetFactorioDataPath() ?? Path.Combine(rootDir ?? string.Empty, "data");
                if (!Directory.Exists(dataDir))
                {
                    _logService.LogWarning($"Factorio data directory not found at: {dataDir}");
                    SetStatus("Factorio data directory not found. DLC detection skipped.", LogLevel.Warning);
                    return;
                }

                bool hasSpaceAgeDlc =
                    Directory.Exists(Path.Combine(dataDir, "space-age")) ||
                    Directory.Exists(Path.Combine(dataDir, "quality")) ||
                    Directory.Exists(Path.Combine(dataDir, "elevated-rails"));

                _settingsService.SetHasSpaceAgeDlc(hasSpaceAgeDlc);
                _logService.Log($"Detected Space Age DLC bundle: {hasSpaceAgeDlc}");

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
                }
            }
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

        public string ModCountSummary
        {
            get
            {
                var enabled = _allMods.Count(m => m.IsEnabled);
                var total = _allMods.Count;
                var updates = _allMods.Count(m => m.HasUpdate);
                return $"Enabled: {enabled}/{total} | Updates: {updates}";
            }
        }

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

                foreach (var group in Groups)
                {
                    group?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}