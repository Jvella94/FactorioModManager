using Avalonia.Media;
using Avalonia.Media.Imaging;
using FactorioModManager.Views.Converters;
using FactorioModManager.Services.Mods;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels
{
    public class ModViewModel : ViewModelBase
    {
        private readonly CompositeDisposable _disposables = [];
        private readonly Services.Settings.ISettingsService? _settingsService;

        // Static brushes - frozen for performance
        private static readonly IBrush _updateBrush = CreateFrozenBrush(60, 120, 60);

        private static readonly IBrush _unusedBrush = CreateFrozenBrush(255, 140, 0);

        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            // Freeze the brush for better performance
            return brush;
        }

        // Backing fields
        private string _name = string.Empty;

        private string _title = string.Empty;
        private string _version = string.Empty;
        private string _author = string.Empty;
        private string _description = string.Empty;
        private bool _isEnabled;
        private bool _hasUpdate;
        private string? _latestVersion;
        private string? _category;
        private string? _sourceUrl;
        private string? _groupName;
        private bool _isUnusedInternal;
        private Bitmap? _thumbnail;
        private static readonly Bitmap _placeholderBitmap = LoadPlaceholderThumbnail();
        private string? _selectedVersion;
        private string? _filePath;
        private int _installedCount;
        private bool _isDownloading;
        private double _downloadProgress;
        private bool _hasDownloadProgress;
        private string _downloadStatusText = string.Empty;
        private long? _sizeOnDiskBytes;

        // Properties
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public string Version
        {
            get => _version;
            set => this.RaiseAndSetIfChanged(ref _version, value);
        }

        public string Author
        {
            get => _author;
            set => this.RaiseAndSetIfChanged(ref _author, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set => this.RaiseAndSetIfChanged(ref _hasUpdate, value);
        }

        public string? LatestVersion
        {
            get => _latestVersion;
            set => this.RaiseAndSetIfChanged(ref _latestVersion, value);
        }

        public string? Category
        {
            get => _category;
            set => this.RaiseAndSetIfChanged(ref _category, value);
        }

        public string? SourceUrl
        {
            get => _sourceUrl;
            set => this.RaiseAndSetIfChanged(ref _sourceUrl, value);
        }

        public string? GroupName
        {
            get => _groupName;
            set => this.RaiseAndSetIfChanged(ref _groupName, value);
        }

        public bool IsUnusedInternal
        {
            get => _isUnusedInternal;
            set => this.RaiseAndSetIfChanged(ref _isUnusedInternal, value);
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail ?? _placeholderBitmap;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        public int InstalledCount
        {
            get => _installedCount;
            set => this.RaiseAndSetIfChanged(ref _installedCount, value);
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
        }

        public bool HasDownloadProgress
        {
            get => _hasDownloadProgress;
            set => this.RaiseAndSetIfChanged(ref _hasDownloadProgress, value);
        }

        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set => this.RaiseAndSetIfChanged(ref _downloadStatusText, value);
        }

        public string? SelectedVersion
        {
            get => _selectedVersion;
            set => this.RaiseAndSetIfChanged(ref _selectedVersion, value);
        }

        public string? FilePath
        {
            get => _filePath;
            set => this.RaiseAndSetIfChanged(ref _filePath, value);
        }

        // New: size on disk in bytes
        public long? SizeOnDiskBytes
        {
            get => _sizeOnDiskBytes;
            set
            {
                this.RaiseAndSetIfChanged(ref _sizeOnDiskBytes, value);
                this.RaisePropertyChanged(nameof(SizeOnDiskText));
            }
        }

        // Human-readable size text
        public string SizeOnDiskText
        {
            get
            {
                if (!SizeOnDiskBytes.HasValue || SizeOnDiskBytes.Value == 0)
                    return "Unknown";

                var bytes = SizeOnDiskBytes.Value;
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                if (bytes >= GB)
                    return $"{(bytes / (double)GB):F2} GB";
                if (bytes >= MB)
                    return $"{(bytes / (double)MB):F2} MB";
                if (bytes >= KB)
                    return $"{(bytes / (double)KB):F2} KB";
                return $"{bytes} B";
            }
        }

        // Collections
        public List<string> Dependencies { get; set; } = [];

        public ObservableCollection<string> AvailableVersions { get; set; } = [];
        public List<string> VersionFilePaths { get; set; } = [];

        // Non-observable properties
        public DateTime? LastUpdated { get; set; }

        public string? ThumbnailPath { get; set; }

        // ✅ Computed properties with proper change notifications
        public bool HasMultipleVersions => AvailableVersions.Count > 1;

        public bool IsOldVersionSelected =>
            SelectedVersion != null && SelectedVersion != Version;

        public string LastUpdatedText => LastUpdated.HasValue
            ? LastUpdated.Value.ToString("yyyy-MM-dd")
            : "Unknown";

        public string UpdateText => HasUpdate && !string.IsNullOrEmpty(LatestVersion)
            ? $"Update available: {LatestVersion}"
            : string.Empty;

        public string ModPortalUrl => $"https://mods.factorio.com/mod/{Name}";

        public IBrush? RowBrush
        {
            get
            {
                if (HasUpdate) return _updateBrush;
                if (IsUnusedInternal) return _unusedBrush;
                return null;
            }
        }

        // Add new properties for dependency visibility and organization
        private bool _showHiddenDependencies = false;

        private bool _isDependencyListExpanded = false;

        public bool ShowHiddenDependencies
        {
            get => _showHiddenDependencies;
            set => this.RaiseAndSetIfChanged(ref _showHiddenDependencies, value);
        }

        public bool IsDependencyListExpanded
        {
            get => _isDependencyListExpanded;
            set => this.RaiseAndSetIfChanged(ref _isDependencyListExpanded, value);
        }

        // Installed dependencies with resolved installed version (Name, InstalledVersion)
        private List<(string Name, string? InstalledVersion)> _installedDependencies = [];

        // Property to expose installed dependencies as tuples for efficient lookups
        public List<(string Name, string? InstalledVersion)> InstalledDependencies
        {
            get => _installedDependencies;
            set => this.RaiseAndSetIfChanged(ref _installedDependencies, value);
        }

        // Computed properties for organized dependencies
        public IReadOnlyList<string> MandatoryDependencies =>
            DependencyHelper.GetMandatoryDependencies(Dependencies);

        public IReadOnlyList<string> OptionalDependencies =>
            [.. Dependencies.Where(DependencyHelper.IsOptionalDependency)];

        public IReadOnlyList<string> IncompatibleDependencies =>
            DependencyHelper.GetIncompatibleDependencies(Dependencies);

        // Add new computed property for sorted dependencies
        public IReadOnlyList<DependencyViewModel> SortedDependencies
        {
            get
            {
                var list = new List<DependencyViewModel>();

                // Mandatory with constraints (preserves version info)
                var mandatoryParsed = DependencyHelper.GetMandatoryDependenciesWithConstraints(Dependencies);
                foreach (var p in mandatoryParsed)
                {
                    var tuple = InstalledDependencies.FirstOrDefault(t => t.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                    var installed = !string.IsNullOrEmpty(tuple.Name);
                    var installedVersion = installed ? tuple.InstalledVersion : null;
                    var versionSatisfied = DependencyHelper.SatisfiesVersionConstraint(installedVersion, p.VersionOperator, p.Version);
                    list.Add(new DependencyViewModel(p.Name, DependencyStatus.Mandatory, p.VersionOperator, p.Version, installed, versionSatisfied, p.Prefix));
                }

                // Optional dependencies (preserve version info from raw strings)
                var optionalRaw = OptionalDependencies;
                foreach (var raw in optionalRaw.Where(IsInstalled))
                {
                    var parsed = DependencyHelper.ParseDependency(raw);
                    var depName = parsed?.Name ?? raw;
                    var tuple = InstalledDependencies.FirstOrDefault(t => t.Name.Equals(depName, StringComparison.OrdinalIgnoreCase));
                    var inst = !string.IsNullOrEmpty(tuple.Name);
                    var installedVersion = inst ? tuple.InstalledVersion : null;
                    var satisfied = DependencyHelper.SatisfiesVersionConstraint(installedVersion, parsed?.VersionOperator, parsed?.Version);
                    list.Add(new DependencyViewModel(depName, DependencyStatus.OptionalInstalled, parsed?.VersionOperator, parsed?.Version, inst, satisfied, parsed?.Prefix));
                }

                foreach (var raw in optionalRaw.Where(d => !IsInstalled(DependencyHelper.ExtractDependencyName(d))))
                {
                    var parsed = DependencyHelper.ParseDependency(raw);
                    var depName = parsed?.Name ?? raw;
                    var tuple = InstalledDependencies.FirstOrDefault(t => t.Name.Equals(depName, StringComparison.OrdinalIgnoreCase));
                    var inst = !string.IsNullOrEmpty(tuple.Name);
                    var installedVersion = inst ? tuple.InstalledVersion : null;
                    var satisfied = DependencyHelper.SatisfiesVersionConstraint(installedVersion, parsed?.VersionOperator, parsed?.Version);
                    list.Add(new DependencyViewModel(depName, DependencyStatus.OptionalNotInstalled, parsed?.VersionOperator, parsed?.Version, inst, satisfied, parsed?.Prefix));
                }

                // Incompatible dependencies: parse raw dependencies to preserve version info
                foreach (var raw in Dependencies)
                {
                    var parsed = DependencyHelper.ParseDependency(raw);
                    if (parsed != null && (parsed.Value.Prefix == "!" || parsed.Value.Prefix == "(!)"))
                    {
                        var tuple = InstalledDependencies.FirstOrDefault(t => t.Name.Equals(parsed.Value.Name, StringComparison.OrdinalIgnoreCase));
                        var inst = !string.IsNullOrEmpty(tuple.Name);
                        var installedVersion = inst ? tuple.InstalledVersion : null;
                        var satisfied = DependencyHelper.SatisfiesVersionConstraint(installedVersion, parsed.Value.VersionOperator, parsed.Value.Version);
                        list.Add(new DependencyViewModel(parsed.Value.Name, DependencyStatus.Incompatible, parsed.Value.VersionOperator, parsed.Value.Version, inst, satisfied, parsed.Value.Prefix));
                    }
                }

                return list;
            }
        }

        public IReadOnlyList<DependencyViewModel> OnlyModSortedDepedencies =>
            [.. SortedDependencies.Where(sd => DependencyHelper.IsGameDependency(sd.Name) == false)
                // Respect user's ShowHiddenDependencies setting: when false, hide only the hidden form '(?)' optional dependencies
                .Where(sd => ShowHiddenDependencies || !( (sd.Status == DependencyStatus.OptionalInstalled || sd.Status == DependencyStatus.OptionalNotInstalled) && sd.IsHiddenOptional))];

        public IReadOnlyList<DependencyViewModel> VisibleDependencies =>
            IsDependencyListExpanded ? OnlyModSortedDepedencies : [.. OnlyModSortedDepedencies.Take(5)];

        // Helper method to check if a dependency is installed. Accepts raw dependency strings (may include prefix/operator/version)
        // and normalizes them before lookup.
        private bool IsInstalled(string dependency)
        {
            if (string.IsNullOrWhiteSpace(dependency))
                return false;

            var name = DependencyHelper.ExtractDependencyName(dependency);
            if (string.IsNullOrEmpty(name))
                return false;

            return InstalledDependencies.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public ReactiveCommand<Unit, Unit> ToggleDependencyListCommand { get; }

        public ModViewModel(Services.Settings.ISettingsService settingsService)
        {
            _settingsService = settingsService;

            // Initialize ShowHiddenDependencies from settings if available
            if (_settingsService != null)
            {
                try
                {
                    ShowHiddenDependencies = _settingsService.GetShowHiddenDependencies();
                    // subscribe to changes so VMs update themselves
                    void handler() => ShowHiddenDependencies = _settingsService.GetShowHiddenDependencies();
                    _settingsService.ShowHiddenDependenciesChanged += handler;
                    // store removal on dispose
                    _disposables.Add(Disposable.Create(() => _settingsService.ShowHiddenDependenciesChanged -= handler));
                }
                catch { }
            }

            // ✅ Properly managed subscriptions
            this.WhenAnyValue(x => x.HasUpdate, x => x.IsUnusedInternal)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(RowBrush)))
                .DisposeWith(_disposables);

            this.WhenAnyValue(x => x.HasUpdate, x => x.LatestVersion)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(UpdateText)))
                .DisposeWith(_disposables);

            this.WhenAnyValue(x => x.SelectedVersion, x => x.Version)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(IsOldVersionSelected)))
                .DisposeWith(_disposables);

            // Notify when ShowHiddenDependencies changes so UI updates
            this.WhenAnyValue(x => x.ShowHiddenDependencies)
                .Subscribe(_ =>
                {
                    this.RaisePropertyChanged(nameof(OnlyModSortedDepedencies));
                    this.RaisePropertyChanged(nameof(VisibleDependencies));
                })
                .DisposeWith(_disposables);

            // ✅ Notify when collection changes
            AvailableVersions.CollectionChanged += OnAvailableVersionsChanged;

            ToggleDependencyListCommand = ReactiveCommand.Create(() =>
            {
                IsDependencyListExpanded = !IsDependencyListExpanded;
                this.RaisePropertyChanged(nameof(VisibleDependencies));
            });
        }

        private void OnAvailableVersionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(HasMultipleVersions));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // ✅ Unsubscribe from event
                AvailableVersions.CollectionChanged -= OnAvailableVersionsChanged;

                _disposables?.Dispose();
                // Only dispose if it's not the shared placeholder
                if (_thumbnail != null && _thumbnail != _placeholderBitmap)
                {
                    _thumbnail?.Dispose();
                }
                _thumbnail = null;

                // Unsubscribe from settings event if we stored it
                // (we used Disposable to remove it above)
            }

            base.Dispose(disposing);
        }
    }
}