using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Xunit;

namespace FMM.Tests.ViewModelTests
{
    public class UpdatesHostTests
    {
        [Fact]
        public async Task DownloadUpdate_ResolvesAgain_WhenPromptsSuppressed_And_ProceedsToDownload()
        {
            // Arrange
            var settingsMockForVm = new Mock<ISettingsService>();

            var mod = new ModViewModel(settingsMockForVm.Object);
            mod.Name = "mainmod";
            mod.Title = "Main Mod";
            mod.Version = "1.0";
            mod.LatestVersion = "2.0";
            mod.HasUpdate = true;

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
            var details = new ModDetailsShortDTO(mod.Name, mod.Title, null, 0, new List<ShortReleaseDTO> {
                new ShortReleaseDTO(mod.LatestVersion!, DateTime.UtcNow, "http://download", "2.0")
            });
            apiMock.Setup(a => a.GetModDetailsAsync(It.IsAny<string>())).ReturnsAsync(details);

            var downloadMock = new Mock<IDownloadService>();
            downloadMock.Setup(d => d.DownloadModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<(long, long?)>>(), default))
                        .ReturnsAsync(Result<bool>.Ok(true));

            var modServiceMock = new Mock<IModService>();
            var logMock = new Mock<ILogService>();
            var uiMock = new Mock<IUIService>();
            uiMock.Setup(u => u.InvokeAsync(It.IsAny<Action>())).Returns<Action>(a => { a(); return Task.CompletedTask; });
            uiMock.Setup(u => u.Post(It.IsAny<Action>())).Callback<Action>(a => a());
            var settingsMock = new Mock<ISettingsService>();
            var metadataMock = new Mock<IModMetadataService>();

            // simple delegates required by UpdatesHost
            Func<IEnumerable<string>, Task> forceRefresh = _ => Task.CompletedTask;
            Func<Task> beginSingle = () => Task.CompletedTask;
            Func<bool, Task> endSingle = _ => Task.CompletedTask;
            Action incrementBatchCompleted = () => { };
            Func<string, LogLevel, Task> setStatus = (_, __) => Task.CompletedTask;
            Func<bool, string, string, string, string, Task<bool>> confirm = (_, __, ___, ____, _____) => Task.FromResult(true);
            Action<int> setTotal = _ => { };
            Action<int> setCompleted = _ => { };
            Action incrementCompleted = () => { };
            Action<bool> setVisible = _ => { };
            Action onApplyBatched = () => { };

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
                forceRefresh,
                beginSingle,
                endSingle,
                incrementBatchCompleted,
                setStatus,
                confirm,
                setTotal,
                setCompleted,
                incrementCompleted,
                setVisible,
                onApplyBatched
            );

            // Create a dependency handler stub that signals prompts are suppressed and PrepareAsync succeeds
            var depHandler = new StubDependencyHandler();
            var progress = new TestProgressReporter();

            // Act
            await host.UpdateModsCoreAsync(new List<ModViewModel> { mod }, depHandler, progress, concurrency: 1);

            // Assert - ensure download was attempted
            downloadMock.Verify(d => d.DownloadModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<(long, long?)>>(), default), Times.Once);
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
            public void Increment() { }
            public Task EndAsync(bool minimal) => Task.CompletedTask;
        }
    }
}
