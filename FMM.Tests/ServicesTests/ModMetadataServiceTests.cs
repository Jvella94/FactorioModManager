using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using Moq;

namespace FMM.Tests.ServicesTests
{
    public class ModMetadataServiceTests
    {
        private readonly Mock<ILogService> _logServiceMock;
        private readonly ModMetadataService _service;

        public ModMetadataServiceTests()
        {
            _logServiceMock = new Mock<ILogService>();
            _service = new ModMetadataService(_logServiceMock.Object);
        }

        [Fact]
        public void EnsureModsExist_AddsMissingMods()
        {
            // Arrange
            var modNames = new List<string> { "mod1", "mod2" };

            // Act
            _service.EnsureModsExist(modNames);

            // Assert
            Assert.NotNull(_service.GetCategory("mod1"));
            Assert.NotNull(_service.GetCategory("mod2"));
        }

        [Fact]
        public void UpdateCategory_SavesCategory()
        {
            // Arrange
            var modName = "test-mod";
            var category = "Test Category";

            // Act
            _service.UpdateCategory(modName, category);

            // Assert
            Assert.Equal(category, _service.GetCategory(modName));
        }

        [Fact]
        public void ClearAllUpdates_ClearsUpdateFlags()
        {
            // Arrange
            var modName = "test-mod";
            _service.UpdateLatestVersion(modName, "1.0.0", true);

            // Act
            _service.ClearAllModUpdates();

            // Assert
            Assert.False(_service.GetHasUpdate(modName));
        }

        [Fact]
        public void UpdateSourceUrl_SavesSourceUrl()
        {
            // Arrange
            var modName = "test-mod";
            var sourceUrl = "https://example.com/mod";
            // Act
            _service.UpdateSourceUrl(modName, sourceUrl);
            // Assert
            Assert.Equal(sourceUrl, _service.GetSourceUrl(modName));
        }
    }
}