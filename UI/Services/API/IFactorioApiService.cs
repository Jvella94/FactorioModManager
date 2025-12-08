using FactorioModManager.Models.DTO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IFactorioApiService
    {
        Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName);

        Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName);

        Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo);

        void ClearCache();

        /// <summary>
        /// Downloads a mod version from the Factorio mod portal
        /// </summary>
        /// <param name="downloadUrl">Full download URL from the mod portal API</param>
        /// <param name="destinationPath">Local file path where the mod should be saved</param>
        /// <param name="progress">Optional progress callback (bytesDownloaded, totalBytes)</param>
        /// <param name="cancellationToken">Cancellation token to abort download</param>
        Task DownloadModAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default);
    }
}