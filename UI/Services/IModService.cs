using FactorioModManager.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public interface IModService
    {
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();

        void ToggleMod(string modName, bool enabled);

        void RemoveMod(string modName);

        string GetModsDirectory();

        /// <summary>
        /// Downloads a specific version of a mod
        /// </summary>
        Task DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default);

        void DeleteVersion(string modName, string version);

        HashSet<string> GetInstalledVersions(string modName);

        void RefreshInstalledVersions(string modName);
    }
}