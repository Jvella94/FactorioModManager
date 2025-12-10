using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Mods;
using ReactiveUI;
using System;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class VersionHistoryReleaseViewModel : ViewModelBase
    {
        private readonly IModVersionManager _modVersionManager;
        private bool _isInstalling;
        private double _downloadProgress;
        private bool _isInstalled;
        private bool _canDelete = true;

        public ShortReleaseDTO Release { get; }

        public string Version => Release.Version;
        public DateTime ReleasedAt => Release.ReleasedAt;

        public string? FactorioVersion => Release.FactorioVersion ?? string.Empty;
        public string? DownloadUrl => Release.DownloadUrl;

        public bool IsInstalling
        {
            get => _isInstalling;
            set => this.RaiseAndSetIfChanged(ref _isInstalling, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
        }

        public bool IsInstalled
        {
            get => _isInstalled;
            set => this.RaiseAndSetIfChanged(ref _isInstalled, value);
        }

        public bool CanDelete
        {
            get => _canDelete;
            set
            {
                this.RaiseAndSetIfChanged(ref _canDelete, value);
                this.RaisePropertyChanged(nameof(CanDeleteOrDownload));
            }
        }

        // Button is enabled if: downloading OR (deleting AND canDelete)
        public bool CanDeleteOrDownload => !IsInstalled || CanDelete;

        public VersionHistoryReleaseViewModel(ShortReleaseDTO release, IModVersionManager modVersionManager, string modName)
        {
            Release = release;
            _modVersionManager = modVersionManager;
            IsInstalled = _modVersionManager.GetInstalledVersions(modName).Contains(release.Version);
        }
    }
}