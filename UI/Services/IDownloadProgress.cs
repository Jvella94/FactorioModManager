using System;

namespace FactorioModManager.Services
{
    public interface IDownloadProgress
    {
        void Initialize(
            Func<bool> isActive,
            Func<int> getTotal,
            Func<int> getCompleted,
            Action<string?> setSpeedText,
            Action<string?> setProgressText,
            Action<double> setProgressPercent);

        IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter();

        // New helpers to control animated percent from outside the reporter
        void SetTargetPercent(double percent);
        void StartAnimation();
        void StopAndSetPercent(double percent);
    }
}
