using FactorioModManager.Services;
using System;

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
        /// Creates a stateful global progress reporter for downloads that updates the UpdateAllSpeedText
        /// and UpdateAllProgressText properties. Each caller should keep and use its own reporter instance.
        /// </summary>
        public IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter()
        {
            return GetOrCreateDownloadProgressHelper().CreateGlobalDownloadProgressReporter();
        }
    }
}