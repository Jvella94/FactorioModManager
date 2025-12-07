using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;

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
        private List<string> _dependencies = [];
        private DateTime? _lastUpdated;
        private string _thumbnailPath = string.Empty;
        private string? _category;
        private string? _sourceUrl;
        private string _groupName = "N/A";
        private bool _hasUpdate;
        private bool _isUnusedInternal;
        private Bitmap? _thumbnail;
        private string? _latestVersion;

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

        public List<string> Dependencies
        {
            get => _dependencies;
            set => this.RaiseAndSetIfChanged(ref _dependencies, value);
        }

        public DateTime? LastUpdated
        {
            get => _lastUpdated;
            set => this.RaiseAndSetIfChanged(ref _lastUpdated, value);
        }

        public string LastUpdatedText => LastUpdated?.ToString("yyyy-MM-dd") ?? "Unknown";

        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set => this.RaiseAndSetIfChanged(ref _thumbnailPath, value);
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

        public string GroupName
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

        public string? LatestVersion
        {
            get => _latestVersion;
            set
            {
                this.RaiseAndSetIfChanged(ref _latestVersion, value);
                this.RaisePropertyChanged(nameof(UpdateText));
            }
        }

        public string UpdateText
        {
            get
            {
                if (HasUpdate && !string.IsNullOrEmpty(LatestVersion))
                {
                    return $"Update: {LatestVersion}";
                }
                return "";
            }
        }

        public IBrush? RowBrush
        {
            get
            {
                // UPDATED: Softer green for updates, orange for unused internal
                if (HasUpdate)
                    return new SolidColorBrush(Color.FromRgb(60, 120, 60)); // Softer, darker green
                if (IsUnusedInternal)
                    return new SolidColorBrush(Color.FromRgb(255, 140, 0)); // Orange
                return null;
            }
        }

        public bool HasUpdate
        {
            get => _hasUpdate;
            set
            {
                this.RaiseAndSetIfChanged(ref _hasUpdate, value);
                this.RaisePropertyChanged(nameof(RowBrush));
                this.RaisePropertyChanged(nameof(UpdateText));
            }
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }
    }

    public class ModGroupViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string? _description;
        private List<string> _modNames = [];
        private int _enabledCount;
        private int _totalCount;
        private bool _isEditing;
        private string _editName = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string? Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public List<string> ModNames
        {
            get => _modNames;
            set => this.RaiseAndSetIfChanged(ref _modNames, value);
        }

        public int EnabledCount
        {
            get => _enabledCount;
            set
            {
                this.RaiseAndSetIfChanged(ref _enabledCount, value);
                this.RaisePropertyChanged(nameof(StatusText));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                this.RaiseAndSetIfChanged(ref _totalCount, value);
                this.RaisePropertyChanged(nameof(StatusText));
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => this.RaiseAndSetIfChanged(ref _isEditing, value);
        }

        public string EditName
        {
            get => _editName;
            set => this.RaiseAndSetIfChanged(ref _editName, value);
        }

        public string StatusText => $"{EnabledCount}/{TotalCount} enabled";
    }
}
