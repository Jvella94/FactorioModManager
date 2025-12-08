using FactorioModManager.Models;
using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager.ViewModels
{
    public class VersionHistoryReleaseViewModel : ReactiveObject
    {
        private readonly IModService _modService;
        private readonly ILogService _logService;
        private bool _isInstalling;
        private double _downloadProgress;
        private bool _isInstalled;

        public ReleaseDTO Release { get; }

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

        public bool CanDeleteOrDownload => !IsInstalling;

        public VersionHistoryReleaseViewModel(ReleaseDTO release, IModService modService, string modName, ILogService logService)
        {
            Release = release;
            _modService = modService;
            IsInstalled = _modService.GetInstalledVersions(modName).Contains(release.Version);
            _logService = logService;
        }

        public ModInfo? ExtractInfoJsonFromZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var infoEntry = archive.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith("info.json", StringComparison.OrdinalIgnoreCase));

                if (infoEntry != null)
                {
                    using var stream = infoEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    return JsonSerializer.Deserialize<ModInfo>(json);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to extract info.json from {Path.GetFileName(zipPath)}: {ex.Message}", ex);
            }
            return null;
        }
    }
}