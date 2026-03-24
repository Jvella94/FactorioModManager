using FactorioModManager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    public interface IUpdateHostUi
    {
        Task ForceRefreshAffectedModsAsync(IEnumerable<string> names);

        Task BeginSingleDownloadProgressAsync();

        Task EndSingleDownloadProgressAsync(bool minimal = false);

        void IncrementBatchCompleted();

        Task SetStatusAsync(string message, LogLevel level = LogLevel.Info);

        Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText);

        void SetDownloadProgressTotal(int total);

        void SetDownloadProgressCompleted(int completed);

        void IncrementDownloadProgressCompleted();

        void SetDownloadProgressVisible(bool visible);

        void ApplyBatchedProgress();

        IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter();
    }
}