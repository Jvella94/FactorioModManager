using FactorioModManager.Models;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow.UpdateHandlers;
using Moq;

namespace FMM.Tests.ViewModelTests
{
    public class UpdatesHostTests
    {
        [Fact]
        public async Task DownloadUpdate_ResolvesAgain_WhenPromptsSuppressed_And_ProceedsToDownload()
        {
            // Arrange
            var settingsMockForVm = new Mock<ISettingsService>();

            var mod = new ModViewModel(settingsMockForVm.Object)
            {
                Name = "mainmod",
                Title = "Main Mod",
                Version = "1.0",
                LatestVersion = "2.0",
                HasUpdate = true
            };

            var allMods = new List<ModViewModel> { mod };

            var depFlowMock = new Mock<IDependencyFlow>();

            var firstRes = new DependencyResolution { Proceed = true };
            firstRes.MissingDependenciesToInstall.Add("dep1");

            var secondRes = new DependencyResolution { Proceed = true };

            // Setup sequence: first call returns missing deps, second returns none
            depFlowMock
                .SetupSequence(d => d.ResolveForUpdateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<ModViewModel>>(), It.IsAny<IDictionary<string, string>?>()))
                .ReturnsAsync(firstRes)
                .ReturnsAsync(secondRes);

            var apiMock = new Mock<IFactorioApiService>();
            var details = new ModDetailsShortDTO(mod.Name, mod.Title, null, 0, [
                new(mod.LatestVersion!, DateTime.UtcNow, "http://download", "2.0")
            ]);
            apiMock.Setup(a => a.GetModDetailsAsync(It.IsAny<string>())).ReturnsAsync(details);

            var downloadMock = new Mock<IDownloadService>();
            downloadMock.Setup(d => d.DownloadModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<(long, long?)>>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Result<bool>.Ok(true));

            var modServiceMock = new Mock<IModService>();
            var logMock = new Mock<ILogService>();
            var uiMock = new Mock<IUIService>();
            uiMock.Setup(u => u.InvokeAsync(It.IsAny<Action>())).Returns<Action>(a => { a(); return Task.CompletedTask; });
            uiMock.Setup(u => u.Post(It.IsAny<Action>())).Callback<Action>(a => a());
            var settingsMock = new Mock<ISettingsService>();
            var metadataMock = new Mock<IModMetadataService>();

            // Create a small test UI adapter implementing IUpdateHostUi
            var testUi = new TestUpdateHostUi(
                forceRefresh: _ => Task.CompletedTask,
                beginSingle: () => Task.CompletedTask,
                endSingle: _ => Task.CompletedTask,
                incrementBatchCompleted: () => { },
                setStatus: (_, __) => Task.CompletedTask,
                confirm: (_, __, ___, ____, _____) => Task.FromResult(true),
                setTotal: _ => { },
                setCompleted: _ => { },
                incrementCompleted: () => { },
                setVisible: _ => { },
                onApplyBatched: () => { }
            );

            var host = new UpdatesHost(
                allMods,
                depFlowMock.Object,
                modServiceMock.Object,
                logMock.Object,
                uiMock.Object,
                downloadMock.Object,
                apiMock.Object,
                settingsMock.Object,
                metadataMock.Object,
                testUi
            );

            // Create a dependency handler stub that signals prompts are suppressed and PrepareAsync succeeds
            var depHandler = new StubDependencyHandler();
            var progress = new TestProgressReporter();

            // Act
            await host.UpdateModsCoreAsync([mod], depHandler, progress, concurrency: 1, cancellationToken: CancellationToken.None);

            // Assert - ensure download was attempted
            downloadMock.Verify(d => d.DownloadModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<(long, long?)>>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private class StubDependencyHandler : IDependencyHandler
        {
            public bool SuppressDependencyPrompts => true;

            public Task<bool> PrepareAsync(List<ModViewModel> mods, Dictionary<string, string> plannedUpdates, IProgressReporter progress)
            {
                return Task.FromResult(true);
            }
        }

        private class TestProgressReporter : IProgressReporter
        {
            public Task BeginAsync(int total) => Task.CompletedTask;

            public void Increment()
            { }

            public Task EndAsync(bool minimal) => Task.CompletedTask;
        }

        private sealed class TestUpdateHostUi(
            Func<IEnumerable<string>, Task> forceRefresh,
            Func<Task> beginSingle,
            Func<bool, Task> endSingle,
            Action incrementBatchCompleted,
            Func<string, LogLevel, Task> setStatus,
            Func<bool, string, string, string, string, Task<bool>> confirm,
            Action<int> setTotal,
            Action<int> setCompleted,
            Action incrementCompleted,
            Action<bool> setVisible,
            Action onApplyBatched) : IUpdateHostUi
        {
            private readonly Func<IEnumerable<string>, Task> _forceRefresh = forceRefresh;
            private readonly Func<Task> _beginSingle = beginSingle;
            private readonly Func<bool, Task> _endSingle = endSingle;
            private readonly Action _incrementBatchCompleted = incrementBatchCompleted;
            private readonly Func<string, LogLevel, Task> _setStatus = setStatus;
            private readonly Func<bool, string, string, string, string, Task<bool>> _confirm = confirm;
            private readonly Action<int> _setTotal = setTotal;
            private readonly Action<int> _setCompleted = setCompleted;
            private readonly Action _incrementCompleted = incrementCompleted;
            private readonly Action<bool> _setVisible = setVisible;
            private readonly Action _onApplyBatched = onApplyBatched;

            public Task ForceRefreshAffectedModsAsync(IEnumerable<string> names) => _forceRefresh(names);

            public Task BeginSingleDownloadProgressAsync() => _beginSingle();

            public Task EndSingleDownloadProgressAsync(bool minimal = false) => _endSingle(minimal);

            public void IncrementBatchCompleted() => _incrementBatchCompleted();

            public Task SetStatusAsync(string message, LogLevel level = LogLevel.Info) => _setStatus(message, level);

            public Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText) => _confirm(suppress, title, message, confirmText, cancelText);

            public void SetDownloadProgressTotal(int total) => _setTotal(total);

            public void SetDownloadProgressCompleted(int completed) => _setCompleted(completed);

            public void IncrementDownloadProgressCompleted() => _incrementCompleted();

            public void SetDownloadProgressVisible(bool visible) => _setVisible(visible);

            public void ApplyBatchedProgress() => _onApplyBatched();

            public IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter()
            {
                return CreateGlobalDownloadProgressReporter();
            }
        }
    }
}