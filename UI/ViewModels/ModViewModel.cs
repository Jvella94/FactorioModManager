using Avalonia.Media.Imaging;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FactorioModManager.ViewModels
{
    public class ModViewModel : ViewModelBase
    {
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
        private string? _selectedVersion;
        private string? _filePath;
        private int _installedCount;

        public int InstalledCount
        {
            get => _installedCount;
            set => this.RaiseAndSetIfChanged(ref _installedCount, value);
        }

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
            set
            {
                this.RaiseAndSetIfChanged(ref _hasUpdate, value);
                this.RaisePropertyChanged(nameof(RowBrush));
            }
        }

        public string? LatestVersion
        {
            get => _latestVersion;
            set
            {
                this.RaiseAndSetIfChanged(ref _latestVersion, value);
                this.RaisePropertyChanged(nameof(UpdateText));
            }
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
            set
            {
                this.RaiseAndSetIfChanged(ref _isUnusedInternal, value);
                this.RaisePropertyChanged(nameof(RowBrush));
            }
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        public List<string> Dependencies { get; set; } = [];
        public DateTime? LastUpdated { get; set; }
        public string? ThumbnailPath { get; set; }

        // ADDED: Version management
        public ObservableCollection<string> AvailableVersions { get; set; } = [];
        public List<string> VersionFilePaths { get; set; } = []; // Track file paths for each version

        public string? SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedVersion, value);
                this.RaisePropertyChanged(nameof(IsOldVersionSelected));
            }
        }

        public bool HasMultipleVersions => AvailableVersions.Count > 1;

        public bool IsOldVersionSelected => SelectedVersion != null && SelectedVersion != Version;

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
                if (HasUpdate)
                    return new SolidColorBrush(Color.FromRgb(60, 120, 60));
                if (IsUnusedInternal)
                    return new SolidColorBrush(Color.FromRgb(255, 140, 0));
                return null;
            }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
        }

        private double _downloadProgress;
        public double DownloadProgress
        {
            get => _downloadProgress;
            set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
        }

        private bool _hasDownloadProgress;
        public bool HasDownloadProgress
        {
            get => _hasDownloadProgress;
            set => this.RaiseAndSetIfChanged(ref _hasDownloadProgress, value);
        }

        private string _downloadStatusText = "";
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set => this.RaiseAndSetIfChanged(ref _downloadStatusText, value);
        }
        public string? FilePath
        {
            get => _filePath;
            set => this.RaiseAndSetIfChanged(ref _filePath, value);
        }

    }
}
