using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    public interface IProgressReporter
    {
        Task BeginAsync(int total);

        void Increment();

        Task EndAsync(bool minimal);
    }

    public interface IDependencyHandler
    {
        bool SuppressDependencyPrompts { get; }

        Task<bool> PrepareAsync(List<ModViewModel> mods, Dictionary<string, string> plannedUpdates, IProgressReporter progress);
    }

    // Minimal host interface exposing operations handlers need. Implemented by MainWindowViewModel.
    public interface IUpdateHost
    {
        IDependencyFlow DependencyFlow { get; }
        IEnumerable<ModViewModel> AllMods { get; }

        Task BeginSingleDownloadProgressAsync();

        Task EndSingleDownloadProgressAsync(bool minimal = false);

        void IncrementBatchCompleted();

        void ScheduleBatchedProgressUiUpdate();

        Task SetStatusAsync(string message, LogLevel level = LogLevel.Info);

        Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText);

        Task<Result> InstallModAsync(string modName);

        Task ForceRefreshAffectedModsAsync(IEnumerable<string> names);

        void ToggleMod(string modName, bool enabled);

        IModService ModService { get; }
        ILogService LogService { get; }

        // Progress control delegated to host so host can update VM-owned UI properties
        void SetDownloadProgressTotal(int total);

        void SetDownloadProgressCompleted(int completed);

        void IncrementDownloadProgressCompleted();

        void SetDownloadProgressVisible(bool visible);

        void SetBatchDependencyInstallInProgress(bool inProgress);

        // High-level operations exposed on the host (support optional cancellation tokens)
        Task UpdateAllAsync(CancellationToken cancellationToken = default);

        Task UpdateSingleAsync(ModViewModel? mod, CancellationToken cancellationToken = default);

        Task<Result> RunInstallWithDependenciesAsync(string modName, Func<Task<Result>> installMainAsync, ModInfo? localModInfo = null, CancellationToken cancellationToken = default);
    }
}