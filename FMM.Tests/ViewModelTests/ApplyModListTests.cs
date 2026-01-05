using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels.MainWindow;
using Moq;

namespace FMM.Tests.ViewModelTests
{
    public class ApplyModListTests
    {
        [Fact]
        public async Task ApplyModList_ActivatesVersions_WithConfirmation()
        {
            var mockModService = new Mock<IModService>();
            var mockGroupService = new Mock<IModGroupService>();
            var mockApi = new Mock<IFactorioApiService>();
            var mockMeta = new Mock<IModMetadataService>();
            var mockSettings = new Mock<ISettingsService>();
            var mockUi = new Mock<IUIService>();
            var mockLog = new Mock<ILogService>();
            var mockDownload = new Mock<IDownloadService>();
            var mockError = new Mock<IErrorMessageService>();
            var mockAppUpdate = new Mock<IAppUpdateChecker>();
            var mockDepFlow = new Mock<IDependencyFlow>();
            var mockVersionManager = new Mock<IModVersionManager>();
            var mockLauncher = new Mock<IFactorioLauncher>();
            var mockThumb = new Mock<IThumbnailCache>();
            var mockFilter = new Mock<IModFilterService>();
            var mockListService = new Mock<IModListService>();
            var mockDownloadProgress = new Mock<IDownloadProgress>();

            // Create sample mod VMs by constructing ModViewModel via factory method in MainWindowViewModel (not public) — instead we'll create lightweight fake ModViewModel objects
            var vm1 = new FactorioModManager.ViewModels.ModViewModel(mockSettings.Object) { Name = "modA", Title = "Mod A", Version = "1.0", IsEnabled = true };
            var vm2 = new FactorioModManager.ViewModels.ModViewModel(mockSettings.Object) { Name = "modB", Title = "Mod B", Version = "1.0", IsEnabled = true };

            mockVersionManager.Setup(x => x.GetInstalledVersions("modA")).Returns(["1.0", "0.9"]);
            mockVersionManager.Setup(x => x.GetInstalledVersions("modB")).Returns(["1.0"]);

            mockUi.Setup(x => x.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Avalonia.Controls.Window?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                  .ReturnsAsync(true);

            var vm = new MainWindowViewModel(
                mockModService.Object,
                mockGroupService.Object,
                mockApi.Object,
                mockMeta.Object,
                mockSettings.Object,
                mockUi.Object,
                mockLog.Object,
                mockDownload.Object,
                mockError.Object,
                mockAppUpdate.Object,
                mockDepFlow.Object,
                mockVersionManager.Object,
                mockLauncher.Object,
                mockThumb.Object,
                mockFilter.Object,
                mockListService.Object,
                mockDownloadProgress.Object);

            // Inject our sample mods into private field via reflection
            var allModsField = typeof(MainWindowViewModel).GetField("_allMods", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) ?? throw new InvalidOperationException("Could not find backing field '_allMods' on MainWindowViewModel (reflection failed in test setup).");
            allModsField.SetValue(vm, new System.Collections.ObjectModel.ObservableCollection<FactorioModManager.ViewModels.ModViewModel> { vm1, vm2 });

            // Prepare list to apply: enable both and set specific versions
            var list = new CustomModList { Name = "list1" };
            list.Entries.Add(new ModListEntry { Name = "modA", Enabled = true, Version = "0.9" });
            list.Entries.Add(new ModListEntry { Name = "modB", Enabled = true, Version = "1.0" });

            mockListService.Setup(x => x.LoadLists()).Returns([list]);

            vm.ModLists.Add(list);

            // Execute command (ReactiveCommand returns IObservable<Unit>, subscribe to completion)
            var obs = vm.ApplyModListCommand.Execute(list.Name);
            var tcs = new TaskCompletionSource<bool>();
            obs.Subscribe(_ => tcs.SetResult(true));
            await tcs.Task;

            mockModService.Verify(x => x.SaveModState("modA", true, "0.9"), Times.AtLeastOnce);
            mockModService.Verify(x => x.SaveModState("modB", true, "1.0"), Times.AtLeastOnce);
        }
    }
}