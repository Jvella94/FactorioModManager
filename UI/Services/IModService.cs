using Avalonia.Media.Imaging;
using FactorioModManager.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public interface IModService
    {
        /// <summary>
        /// Loads all installed mods from the mods directory
        /// </summary>
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();

        /// <summary>
        /// Toggles a mod's enabled/disabled state in mod-list.json
        /// </summary>
        void ToggleMod(string modName, bool isEnabled);

        /// <summary>
        /// Removes a mod completely (deletes file and updates mod-list.json)
        /// </summary>
        void RemoveMod(string modName, string filePath);

        /// <summary>
        /// Loads a thumbnail from a mod zip file or directory
        /// </summary>
        Task<Bitmap?> LoadThumbnailAsync(string thumbnailPath);

        /// <summary>
        /// Gets all installed versions of a specific mod
        /// </summary>
        List<string> GetInstalledVersions(string modName);

        /// <summary>
        /// Deletes a specific version of a mod
        /// </summary>
        void DeleteVersion(string modName, string version);

        /// <summary>
        /// Refreshes installed versions cache for a mod
        /// </summary>
        void RefreshInstalledVersions(string modName);

        /// <summary>
        /// Gets the mods directory path
        /// </summary>
        string GetModsDirectory();

        /// <summary>
        /// Downloads a mod version asynchronously with progress reporting
        /// </summary>
        Task DownloadVersionAsync(
            string modName,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)> progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads ModInfo (info.json) from a mod file (zip or directory)
        /// </summary>
        ModInfo? ReadModInfoFromFile(string filePath);
    }
}