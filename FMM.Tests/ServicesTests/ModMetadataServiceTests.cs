using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using Moq;

namespace Tests.ServicesTests
{
    public class ModMetadataServiceTests
    {
        private readonly Mock<ILogService> _logServiceMock;
        private readonly IFactorioApiService _apiService = new FactorioApiService(new HttpClient(), new Mock<ILogService>().Object);
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
        public void NeedsCategoryCheck_ReturnsTrueForNewMod()
        {
            // Arrange
            var modName = "new-mod";

            // Act
            var result = _service.NeedsCategoryCheck(modName);

            // Assert
            Assert.True(result);
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

        [Fact]
        public void NeedsSourceUrlCheck_ReturnsFalseIfRecentlyChecked()
        {
            // Arrange
            var modName = "recently-checked-mod";
            _service.UpdateSourceUrl(modName, "https://example.com/mod", wasChecked: true);
            // Act
            var result = _service.NeedsSourceUrlCheck(modName);
            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NeedsSourceUrlCheck_ReturnsTrueIfNotChecked()
        {
            // Arrange
            var modName = "not-checked-mod";
            _service.UpdateSourceUrl(modName, "https://example.com/mod", wasChecked: false);
            // Act
            var result = _service.NeedsSourceUrlCheck(modName);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void NeedsSourceUrlCheck_ReturnsTrueIfCheckedLongAgo()
        {
            // Arrange
            var modName = "old-checked-mod";
            _service.UpdateSourceUrl(modName, "https://example.com/mod", wasChecked: true);
            // Manually set LastChecked to more than 7 days ago
            var metadataField = typeof(ModMetadataService).GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = (Dictionary<string, ModMetadata>)metadataField.GetValue(_service)!;
            cache[modName].CreatedOn = DateTime.UtcNow.AddDays(-8);
            // Act
            var result = _service.NeedsSourceUrlCheck(modName);
            // Assert
            Assert.True(result);
        }

        [Fact]
        public async void CheckSourceUrlLoadSuccessfully()
        {
            var modName = "single-furnace-stack";
            _service.ClearMetadataForMod(modName);
            var details = await _apiService.GetModDetailsFullAsync(modName);
            // Manually clear cache
            if (details != null)
            {
            }
            else
            {
                _service.CreateMetadata(modName);
            }
        }
    }
}