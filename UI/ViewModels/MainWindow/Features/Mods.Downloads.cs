using FactorioModManager.Models;
using FactorioModManager.Services;
using System;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        // Shared download progress helper (resolved via interface)
        private IDownloadProgress? _downloadProgressHelper;

        // Helper to create or return the shared download progress helper
        private IDownloadProgress GetOrCreateDownloadProgressHelper()
        {
            if (_downloadProgressHelper != null)
                return _downloadProgressHelper;

            // Resolve the shared singleton (via interface) and configure it for this view model's callbacks
            var singleton = ServiceContainer.Instance.Resolve<IDownloadProgress>();
            // Initialize helper so it updates its own properties rather than main VM properties
            singleton.Initialize(
                () => IsDownloadProgressVisible,
                () => DownloadProgressTotal,
                () => DownloadProgressCompleted,
                s => ((DownloadProgressViewModel)singleton).UpdateSpeedText(s),
                s => ((DownloadProgressViewModel)singleton).UpdateProgressText(s),
                d => ((DownloadProgressViewModel)singleton).UpdateProgressPercent(d));

            _downloadProgressHelper = singleton;
            return _downloadProgressHelper;
        }

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
                // Setup progress reporting using shared helper
                var helper = GetOrCreateDownloadProgressHelper();
                IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null;

                if (modForProgress != null)
                {
                    // Per-mod progress: update mod UI and forward to global helper
                    progress = new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
                    {
                        _uiService.Post(() =>
                        {
                            modForProgress.HasDownloadProgress = p.totalBytes.HasValue && p.totalBytes.Value > 0;
                            if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                            {
                                var progressPercent = (double)p.bytesDownloaded / p.totalBytes.Value * 100;
                                modForProgress.DownloadProgress = progressPercent;
                                // show simple per-mod status; global helper shows speed/aggregate
                                if (p.totalBytes.HasValue && p.totalBytes.Value > 0)
                                    modForProgress.DownloadStatusText = $"Downloading... {progressPercent:F0}%";
                            }
                            else
                            {
                                var mbDownloaded = p.bytesDownloaded / 1024.0 / 1024.0;
                                modForProgress.DownloadStatusText = $"Downloading... {mbDownloaded:F2} MB";
                            }
                        });

                        // Always report to global helper
                        helper.CreateGlobalDownloadProgressReporter().Report(p);
                    });
                }
                else
                {
                    // No per-mod UI; use global helper directly
                    progress = helper.CreateGlobalDownloadProgressReporter();
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

                        modForProgress?.DownloadStatusText = $"Failed: {result.Error}";
                    });
                }
                else
                {
                    // If running batch progress, increment completed count when this download finishes
                    if (IsDownloadProgressVisible)
                    {
                        var newVal = System.Threading.Interlocked.Increment(ref _downloadProgressCompleted);
                        // Schedule a batched UI update instead of immediate per-increment updates
                        ScheduleUpdateAllProgressUiUpdate();
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error downloading {modTitle}: {ex.Message}");

                await _uiService.InvokeAsync(() =>
                {
                    SetStatus($"Error downloading {modTitle}: {ex.Message}", LogLevel.Error);

                    modForProgress?.DownloadStatusText = $"Error: {ex.Message}";
                });

                return Result<bool>.Fail(ex.Message, ErrorCode.DownloadFailed);
            }
        }

        /// <summary>
        /// Creates a stateful global progress reporter for downloads that updates the UpdateAllSpeedText
        /// and UpdateAllProgressText properties. Each caller should keep and use its own reporter instance.
        /// </summary>
        public IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter()
        {
            return GetOrCreateDownloadProgressHelper().CreateGlobalDownloadProgressReporter();
        }
    }
}