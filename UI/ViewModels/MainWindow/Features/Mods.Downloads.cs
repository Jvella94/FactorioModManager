using FactorioModManager.Models;
using System;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Downloads a mod from the portal with progress reporting
        /// </summary>
        private async Task<Result<bool>> DownloadModFromPortalAsync(
            string modName,
            string modTitle,
            string version,
            string downloadUrl,
            ModViewModel? modForProgress = null)
        {
            try
            {
                // Setup progress reporting
                IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null;

                if (modForProgress != null)
                {
                    progress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                    {
                        _uiService.Post(() =>
                        {
                            if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                            {
                                var progressPercent = (double)p.bytesDownloaded / p.totalBytes.Value * 100;
                                modForProgress.HasDownloadProgress = true;
                                modForProgress.DownloadProgress = progressPercent;
                                modForProgress.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                            }
                            else
                            {
                                var mbDownloaded = p.bytesDownloaded / 1024.0 / 1024.0;
                                modForProgress.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                            }
                        });
                    });
                }

                // Download using service
                var result = await _downloadService.DownloadModAsync(
                    modName,
                    modTitle,
                    version,
                    downloadUrl,
                    progress);

                // Handle result
                if (!result.Success)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Download failed for {modTitle}: {result.Error}", LogLevel.Error);

                        if (modForProgress != null)
                        {
                            modForProgress.DownloadStatusText = $"Failed: {result.Error}";
                        }
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading {modTitle}: {ex.Message}", ex);

                await _uiService.InvokeAsync(() =>
                {
                    SetStatus($"Error downloading {modTitle}: {ex.Message}", LogLevel.Error);

                    if (modForProgress != null)
                    {
                        modForProgress.DownloadStatusText = $"Error: {ex.Message}";
                    }
                });

                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }
    }
}