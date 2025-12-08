using FactorioModManager.Services.API;
using FluentAssertions;
using Xunit;

namespace FactorioModManager.Tests.Services
{
    public class FactorioApiServiceTests
    {
        private readonly FactorioApiService _factorioApiService;

        public FactorioApiServiceTests()
        {
            _factorioApiService = new FactorioApiService();
        }

        [Fact]
        public async Task LoadModFromModPortal()
        {
            var modDetails = await _factorioApiService.GetModDetailsAsync("single-furnace-stack");
            Assert.NotNull(modDetails);
            modDetails.Name.Should().Be("single-furnace-stack");
            modDetails.Category.Should().Be("content");
        }
    }
}