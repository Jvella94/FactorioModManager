using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;

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
        private List<string> _dependencies = new();
        private DateTime? _lastUpdated;
        private string _groupName = "N/A";
        private Bitmap? _thumbnail;
        private string _thumbnailPath = string.Empty;
        private string? _category;
        private string? _sourceUrl;
        private bool _hasUpdate;
        private bool _isUnusedInternal;

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

        public string GroupName
        {
            get => _groupName;
            set => this.RaiseAndSetIfChanged(ref _groupName, value);
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

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

        public bool HasUpdate
        {
            get => _hasUpdate;
            set => this.RaiseAndSetIfChanged(ref _hasUpdate, value);
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

        public IBrush RowBrush => IsUnusedInternal ? Brushes.DarkOrange : Brushes.Transparent;

        public string ModPortalUrl => $"https://mods.factorio.com/mod/{Name}";
    }
}
