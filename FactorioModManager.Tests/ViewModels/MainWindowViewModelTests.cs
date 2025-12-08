using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using FluentAssertions;
using Moq;
using System.Reactive.Linq;
using Xunit;

namespace FactorioModManager.Tests.ViewModels
{
    public class MainWindowViewModelTests
    {
        private readonly Mock<IModService> _mockModService;
        private readonly Mock<ILogService> _mockLogService;
        private readonly Mock<IFactorioApiService> _mockApiService;      // ✅ Parameter 3
        private readonly Mock<IUIService> _mockUiService;                 // ✅ Later parameter
        private readonly Mock<IModGroupService> _mockGroupService;
        private readonly Mock<IModMetadataService> _mockMetadataService;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly MainWindowViewModel _viewModel;

        public MainWindowViewModelTests()
        {
            _mockModService = new Mock<IModService>();
            _mockLogService = new Mock<ILogService>();
            _mockApiService = new Mock<IFactorioApiService>();
            _mockUiService = new Mock<IUIService>();
            _mockGroupService = new Mock<IModGroupService>();
            _mockMetadataService = new Mock<IModMetadataService>();
            _mockSettingsService = new Mock<ISettingsService>();

            _viewModel = new MainWindowViewModel(
             _mockModService.Object,        // 0: IModService
             _mockGroupService.Object,      // 1: IModGroupService  
             _mockApiService.Object,        // 2: IFactorioApiService
             _mockMetadataService.Object,   // 3: IModMetadataService
             _mockSettingsService.Object,   // 4: ISettingsService
             _mockUiService.Object,         // 5: IUIService
             _mockLogService.Object         // 6: ILogService
         );

        }

        [Fact]
        public async Task RefreshModsCommand_CallsModService()
        {
            // Arrange - mock the exact return type your service expects
            _mockModService.Setup(s => s.LoadAllMods())
                .Returns(GetTestMods());

            // Act
            await _viewModel.RefreshModsCommand.Execute();

            // Assert
            _mockModService.Verify(s => s.LoadAllMods(), Times.Once);
        }

        [Fact]
        public void ToggleModCommand_TogglesIsEnabled()
        {
            // Arrange
            var testMod = new ModViewModel
            {
                Name = "test-mod",
                Title = "Test Mod",
                Version = "1.0.0",
                IsEnabled = false
            };

            _viewModel.SelectedMod = testMod;

            // ✅ CORRECT: Direct Execute call with ModViewModel parameter
            _viewModel.ToggleModCommand.Execute(testMod);

            // Assert
            testMod.IsEnabled.Should().BeTrue();
        }



        private static List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> GetTestMods()
        {
            return
            [
                (
                    new ModInfo
                    {
                        Name = "test-mod",
                        Title = "Test Mod",
                        Version = "1.0.0",
                        Author = "test-author"
                    },
                    true,                                    // IsEnabled
                    DateTime.Now,                            // LastUpdated
                    null,                                    // ThumbnailPath
                    @"C:\test\test-mod-1.0.0.zip"           // FilePath
                )
            ];
        }

        [Fact]
        public void Debug_ToggleModCommand_CanExecuteSource()
        {
            var testMod = new ModViewModel { Name = "test-mod", IsEnabled = false };
            _viewModel.SelectedMod = testMod;

            // Direct property access if command uses WhenAnyValue
            Console.WriteLine($"🔍 ToggleModCommand observable: {_viewModel.ToggleModCommand}");

            // Force command execution regardless
            typeof(MainWindowViewModel)
                .GetProperty("ToggleModCommand")?
                .GetValue(_viewModel)?
                .GetType()
                .GetMethod("Execute")?
                .Invoke(_viewModel.ToggleModCommand, new object?[] { testMod });

            testMod.IsEnabled.Should().BeTrue();
        }




        [Fact]
        public void ToggleModCommand_TogglesSelectedMod()
        {
            var testMod = new ModViewModel { IsEnabled = false, Name = "test-mod" };
            _viewModel.SelectedMod = testMod;

            // No parameter - toggles SelectedMod
            _viewModel.ToggleModCommand.Execute(null);

            testMod.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void Test_ToggleWithParameter()
        {
            var testMod = new ModViewModel { IsEnabled = false, Name = "test-mod" };
            _viewModel.SelectedMod = testMod;

            _viewModel.ToggleModCommand.Execute(testMod);

            Assert.True(testMod.IsEnabled);
        }

        [Fact]
        public void Test_ToggleWithoutParameter()
        {
            var testMod = new ModViewModel { IsEnabled = false, Name = "test-mod" };
            _viewModel.SelectedMod = testMod;

            _viewModel.ToggleModCommand.Execute(null);

            Assert.True(testMod.IsEnabled);
        }

    }
}
