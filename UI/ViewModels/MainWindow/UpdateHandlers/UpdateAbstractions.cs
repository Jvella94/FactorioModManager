using System.Collections.Generic;
using System.Threading.Tasks;
using FactorioModManager.Services.Mods;
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System;

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

        Task<Result> RunInstallWithDependenciesAsync(string modName, Func<Task<Result>> installMainAsync);

        IModService ModService { get; }
        ILogService LogService { get; }

        // Host-level update operations (authoritative implementations)
        Task UpdateSingleAsync(ModViewModel? mod);

        Task UpdateModsCoreAsync(List<ModViewModel> modsToUpdate, IDependencyHandler dependencyHandler, IProgressReporter progressReporter, int concurrency);

        Task UpdateAllAsync();

        // Progress control delegated to host so host can update VM-owned UI properties
        void SetDownloadProgressTotal(int total);

        void SetDownloadProgressCompleted(int completed);

        void IncrementDownloadProgressCompleted();

        void SetDownloadProgressVisible(bool visible);
    }
}