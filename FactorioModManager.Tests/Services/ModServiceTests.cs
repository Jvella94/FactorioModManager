using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FluentAssertions;
using Moq;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace FactorioModManager.Tests.Services
{
    public class ModServiceTests : IDisposable
    {
        private readonly string _testModsDir;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<ILogService> _mockLogService;
        private readonly ModService _modService;

        public ModServiceTests()
        {
            _testModsDir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "test-mods");
            Directory.CreateDirectory(_testModsDir);

            // ✅ Create Mocks
            _mockSettingsService = new Mock<ISettingsService>();
            _mockSettingsService.Setup(s => s.GetModsPath()).Returns(_testModsDir);

            _mockLogService = new Mock<ILogService>();

            // ✅ EXACT ModService constructor call
            _modService = new ModService(
                _mockSettingsService.Object,    // ISettingsService
                _mockLogService.Object          // ILogService
            );
        }

        [Fact]
        public void LoadAllMods_LoadsModsFromTestDirectory()
        {
            // Arrange - create test mod in test folder
            var testZip = Path.Combine(_testModsDir, "test-mod-1.0.0.zip");
            if (File.Exists(testZip)) { File.Delete(testZip); }
            CreateTestModZip(testZip);

            // Act
            var mods = _modService.LoadAllMods().ToList();

            // Assert
            mods.Should().NotBeEmpty();
            mods.Should().HaveCount(1);
            mods[0].Info.Name.Should().Be("test-mod");
            mods[0].Info.Version.Should().Be("1.0.0");
            File.Exists(testZip).Should().BeTrue(); // Confirms it's using test folder
        }

        [Fact]
        public void LoadAllMods_IgnoresInvalidZips()
        {
            // Arrange
            var invalidZip = Path.Combine(_testModsDir, "invalid.zip");
            File.WriteAllText(invalidZip, "not a zip file");

            // Act
            var mods = _modService.LoadAllMods().ToList();

            // Assert
            mods.Should().NotContain(m => Path.GetFileName(m.FilePath) == "invalid.zip");
        }

        [Fact]
        public void LoadAllMods_UsesTestModsPathFromSettings()
        {
            // Act
            var modsPath = _modService.GetModsDirectory(); // Internal method that uses settings

            // Assert
            modsPath.Should().Be(_testModsDir);
        }

        private void CreateTestModZip(string zipPath)
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var infoJson = """
            {
                "name": "test-mod",
                "title": "Test Mod",
                "version": "1.0.0",
                "author": "test-author",
                "description": "Test description",
                "dependencies": ["base >= 1.0.0"]
            }
            """;

            var entry = archive.CreateEntry("info.json");
            using var writer = entry.Open();
            using var streamWriter = new StreamWriter(writer);
            streamWriter.Write(infoJson);
        }

        public void Dispose()
        {
            // Clean up test files
            if (Directory.Exists(_testModsDir))
            {
                Directory.Delete(_testModsDir, true);
            }
        }
    }
}
