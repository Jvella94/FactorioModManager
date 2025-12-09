using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels
{
    public class ModViewModel : ViewModelBase
    {
        private readonly CompositeDisposable _disposables = [];

        // Static brushes - frozen for performance
        private static readonly IBrush UpdateBrush = CreateFrozenBrush(60, 120, 60);

        private static readonly IBrush UnusedBrush = CreateFrozenBrush(255, 140, 0);

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
        private static readonly Bitmap PlaceholderBitmap = Constants.LoadPlaceholderThumbnail();
        private string? _selectedVersion;
        private string? _filePath;
        private int _installedCount;
        private bool _isDownloading;
        private double _downloadProgress;
        private bool _hasDownloadProgress;
        private string _downloadStatusText = string.Empty;

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
            get => _thumbnail ?? PlaceholderBitmap;
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
                if (HasUpdate) return UpdateBrush;
                if (IsUnusedInternal) return UnusedBrush;
                return null;
            }
        }

        public ModViewModel()
        {
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

            // ✅ Notify when collection changes
            AvailableVersions.CollectionChanged += OnAvailableVersionsChanged;
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
                if (_thumbnail != null && _thumbnail != PlaceholderBitmap)
                {
                    _thumbnail?.Dispose();
                }
                _thumbnail = null;
            }

            base.Dispose(disposing);
        }
    }
}