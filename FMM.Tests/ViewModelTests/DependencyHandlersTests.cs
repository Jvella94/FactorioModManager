using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow.UpdateHandlers;
using Moq;

namespace FMM.Tests.ViewModelTests
{
    public class DependencyHandlersTests
    {
        private class TestProgressReporter : IProgressReporter
        {
            public int? Total { get; private set; }
            public int IncrementCount { get; private set; }

            public Task BeginAsync(int total)
            {
                Total = total;
                return Task.CompletedTask;
            }

            public void Increment()
            { IncrementCount++; }

            public Task EndAsync(bool minimal) => Task.CompletedTask;
        }

        private class TestUpdateHost(IDependencyFlow flow, IEnumerable<ModViewModel> allMods, Func<string, Task<Result>>? installFunc = null) : IUpdateHost
        {
            public IDependencyFlow DependencyFlow { get; } = flow;
            public IEnumerable<ModViewModel> AllMods { get; set; } = allMods;
            public IModService ModService { get; } = Mock.Of<IModService>();
            public ILogService LogService { get; } = Mock.Of<ILogService>();
            private readonly Func<string, Task<Result>> _installFunc = installFunc ?? (name => Task.FromResult(Result.Ok()));

            public List<(string Name, bool Enabled)> ToggleCalls { get; } = [];

            public Task BeginSingleDownloadProgressAsync() => Task.CompletedTask;

            public Task EndSingleDownloadProgressAsync(bool minimal = false) => Task.CompletedTask;

            public void IncrementBatchCompleted()
            { }

            public void ScheduleBatchedProgressUiUpdate()
            { }

            public Task SetStatusAsync(string message, LogLevel level = LogLevel.Info) => Task.CompletedTask;

            public Task<bool> ConfirmDependencyInstallAsync(bool suppress, string title, string message, string confirmText, string cancelText) => Task.FromResult(true);

            public Task<Result> InstallModAsync(string modName) => _installFunc(modName);

            public Task ForceRefreshAffectedModsAsync(IEnumerable<string> names) => Task.CompletedTask;

            public void ToggleMod(string modName, bool enabled)
            {
                ToggleCalls.Add((modName, enabled));

                var list = AllMods as IList<ModViewModel> ?? [.. AllMods];
                var vm = list.FirstOrDefault(m => m.Name == modName);
                vm?.IsEnabled = enabled;
            }

            public Task<Result> RunInstallWithDependenciesAsync(string modName, Func<Task<Result>> installMainAsync) => Task.FromResult(Result.Ok());

            public Task UpdateSingleAsync(ModViewModel? mod) => Task.CompletedTask;

            public Task UpdateModsCoreAsync(List<ModViewModel> modsToUpdate, IDependencyHandler dependencyHandler, IProgressReporter progressReporter, int concurrency) => Task.CompletedTask;

            public Task UpdateAllAsync() => Task.CompletedTask;

            public void SetDownloadProgressTotal(int total)
            { }

            public void SetDownloadProgressCompleted(int completed)
            { }

            public void IncrementDownloadProgressCompleted()
            { }

            public void SetDownloadProgressVisible(bool visible)
            { }
        }

        private class FakeDependencyFlow : IDependencyFlow
        {
            private readonly HashSet<string> _missing;
            private readonly HashSet<string> _toEnable;
            private readonly HashSet<string> _toDisable;

            public FakeDependencyFlow(IEnumerable<string> missing)
            {
                _missing = new HashSet<string>(missing, StringComparer.OrdinalIgnoreCase);
                _toEnable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _toDisable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public FakeDependencyFlow(IEnumerable<string> missing, IEnumerable<string>? toEnable, IEnumerable<string>? toDisable)
            {
                _missing = new HashSet<string>(missing, StringComparer.OrdinalIgnoreCase);
                _toEnable = toEnable != null ? new HashSet<string>(toEnable, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _toDisable = toDisable != null ? new HashSet<string>(toDisable, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public Task<(CombinedDependencyResolution Combined, string Message)> BuildCombinedUpdatePreviewAsync(IEnumerable<(string Name, string? TargetVersion, bool WillBeEnabled)> plannedTargets, IEnumerable<ModViewModel> installedMods)
                => Task.FromResult((new CombinedDependencyResolution([.. _missing], [], [], []), string.Empty));

            public Task<(DependencyResolution Resolution, string Message)> BuildInstallPreviewAsync(string modName, IEnumerable<ModViewModel> installedMods)
                => Task.FromResult((new DependencyResolution { Proceed = true }, string.Empty));

            public Task<(DependencyResolution Resolution, string Message)> BuildUpdatePreviewAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null)
            {
                var res = new DependencyResolution { Proceed = true };
                foreach (var m in _missing) res.MissingDependenciesToInstall.Add(m);

                foreach (var e in _toEnable)
                {
                    var vm = installedMods.FirstOrDefault(m => m.Name.Equals(e, StringComparison.OrdinalIgnoreCase));
                    if (vm != null) res.ModsToEnable.Add(vm);
                }

                foreach (var d in _toDisable)
                {
                    var vm = installedMods.FirstOrDefault(m => m.Name.Equals(d, StringComparison.OrdinalIgnoreCase));
                    if (vm != null) res.ModsToDisable.Add(vm);
                }

                return Task.FromResult((res, string.Empty));
            }

            public Task<DependencyResolution> ResolveForInstallAsync(string modName, IEnumerable<ModViewModel> installedMods)
                => Task.FromResult(new DependencyResolution { Proceed = true });

            public Task<DependencyResolution> ResolveForUpdateAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null)
            {
                var r = new DependencyResolution { Proceed = true };
                foreach (var m in _missing) r.MissingDependenciesToInstall.Add(m);

                foreach (var e in _toEnable)
                {
                    var vm = installedMods.FirstOrDefault(m => m.Name.Equals(e, StringComparison.OrdinalIgnoreCase));
                    if (vm != null) r.ModsToEnable.Add(vm);
                }

                foreach (var d in _toDisable)
                {
                    var vm = installedMods.FirstOrDefault(m => m.Name.Equals(d, StringComparison.OrdinalIgnoreCase));
                    if (vm != null) r.ModsToDisable.Add(vm);
                }

                return Task.FromResult(r);
            }

            public List<string> GetDisabledDependenciesForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods, Dictionary<string, bool> enabledStates) => [];

            public List<string> GetMissingBuiltInDependenciesForModInfo(ModInfo mod) => [];

            public List<string> GetMissingMandatoryDepsForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods) => [];

            public bool ValidateMandatoryDependencies(List<string> dependencies, IEnumerable<ModViewModel> installedMods) => true;
        }

        [Fact]
        public async Task BatchDependencyHandler_BeginAsync_TotalIsModsPlusMissingDependencies()
        {
            // Arrange
            var settingsServiceMock = new Mock<FactorioModManager.Services.Settings.ISettingsService>();
            var mod1 = new ModViewModel(settingsServiceMock.Object) { Name = "modA", LatestVersion = "1.2", IsEnabled = true };
            var mod2 = new ModViewModel(settingsServiceMock.Object) { Name = "modB", LatestVersion = "2.0", IsEnabled = false };

            var mods = new List<ModViewModel> { mod1, mod2 };

            var missing = new HashSet<string> { "dep1", "dep2" };
            var fakeFlow = new FakeDependencyFlow(missing);

            var host = new TestUpdateHost(fakeFlow, mods);

            // Instantiate the real BatchDependencyHandler from the UI project
            var handler = new BatchDependencyHandler(host);

            var progress = new TestProgressReporter();

            // Act
            var prepared = await handler.PrepareAsync(mods, [], progress);

            // Assert
            Assert.True(prepared);
            // Expect total = number of mods + number of missing dependencies
            Assert.Equal(mods.Count + missing.Count, progress.Total);
        }

        [Fact]
        public async Task PrepareAsync_ReturnsFalse_WhenDependencyInstallFails_PartialInstallObserved()
        {
            // Arrange
            var settingsServiceMock = new Mock<FactorioModManager.Services.Settings.ISettingsService>();
            var mod1 = new ModViewModel(settingsServiceMock.Object) { Name = "modA", LatestVersion = "1.2", IsEnabled = true };
            var mods = new List<ModViewModel> { mod1 };

            // Two missing dependencies where first succeeds and second fails
            var missing = new List<string> { "dep-good", "dep-bad" };
            var fakeFlow = new FakeDependencyFlow(missing);

            // installFunc: succeed for dep-good, fail for dep-bad
            static Task<Result> InstallFunc(string name) => name == "dep-good"
                ? Task.FromResult(Result.Ok())
                : Task.FromResult(Result.Fail($"Failed to install {name}"));

            var host = new TestUpdateHost(fakeFlow, mods, InstallFunc);
            var handler = new BatchDependencyHandler(host);
            var progress = new TestProgressReporter();

            // Act
            var prepared = await handler.PrepareAsync(mods, [], progress);

            // Assert: should abort and return false
            Assert.False(prepared);
            Assert.Equal(mods.Count + missing.Count, progress.Total);
            // Only the first (successful) install should have incremented progress
            Assert.Equal(1, progress.IncrementCount);
        }

        [Fact]
        public async Task PrepareAsync_TogglesEnableDisable_AsSuggestedByDependencyResolution()
        {
            // Arrange
            var settingsServiceMock = new Mock<FactorioModManager.Services.Settings.ISettingsService>();
            var modA = new ModViewModel(settingsServiceMock.Object) { Name = "modA", LatestVersion = "1.2", IsEnabled = true };
            var depX = new ModViewModel(settingsServiceMock.Object) { Name = "depX", LatestVersion = "1.0", IsEnabled = false };

            var mods = new List<ModViewModel> { modA, depX };

            // Fake flow will suggest enabling depX and also report a missing dependency depY so the handler
            // applies the enable/disable decisions before attempting installs
            var fakeFlow = new FakeDependencyFlow(["depY"], ["depX"], null);

            var host = new TestUpdateHost(fakeFlow, mods);
            var handler = new BatchDependencyHandler(host);
            var progress = new TestProgressReporter();

            // Act
            var prepared = await handler.PrepareAsync([modA], [], progress);

            // Assert
            Assert.True(prepared);
            // Toggle should have been called to enable depX
            Assert.Contains(host.ToggleCalls, t => t.Name == "depX" && t.Enabled == true);
            // And the actual ModViewModel instance should now be enabled
            Assert.True(depX.IsEnabled);
        }
    }
}