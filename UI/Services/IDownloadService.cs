using FactorioModManager.Models;
using System;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public interface IDownloadService
    {
        /// <summary>
        /// Downloads a mod from the Factorio mod portal
        /// </summary>
        Task<Result<bool>> DownloadModAsync(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null);

        /// <summary>
        /// Verifies a downloaded mod file is valid
        /// </summary>
        Task<Result<bool>> VerifyModFileAsync(string filePath, string displayName);

        /// <summary>
        /// Installs a mod from a local file
        /// </summary>
        Task<Result<bool>> InstallFromLocalFileAsync(string sourceFilePath);

        /// <summary>
        /// Deletes old versions of a mod (if setting enabled)
        /// </summary>
        void DeleteOldVersions(string modName, string currentVersionFile);
    }
}