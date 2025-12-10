using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using FactorioModManager.Services.API;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Infrastructure;
using Moq;
using Xunit;

namespace Tests.ServicesTests
{
    public class FactorioApiServiceTests
    {
        private readonly Mock<HttpClient> _httpClientMock;
        private readonly Mock<ILogService> _logServiceMock;
        private readonly FactorioApiService _service;

        public FactorioApiServiceTests()
        {
            _httpClientMock = new Mock<HttpClient>();
            _logServiceMock = new Mock<ILogService>();
            _service = new FactorioApiService(_httpClientMock.Object, _logServiceMock.Object);
        }

        [Fact]
        public async Task GetModDetailsAsync_ReturnsModDetails()
        {
            // Arrange
            var modName = "test-mod";
            var expectedUrl = $"https://mods.factorio.com/api/mods/{modName}?version=2.0&hide_deprecated=true";
            var responseContent = "{\"name\":\"test-mod\",\"title\":\"Test Mod\"}";

            _httpClientMock.Setup(client => client.GetAsync(expectedUrl))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            // Act
            var result = await _service.GetModDetailsAsync(modName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-mod", result.Name);
        }

        [Fact]
        public async Task GetModDetailsAsync_ReturnsNull_WhenApiFails()
        {
            // Arrange
            var modName = "test-mod";
            var expectedUrl = $"https://mods.factorio.com/api/mods/{modName}?version=2.0&hide_deprecated=true";

            _httpClientMock.Setup(client => client.GetAsync(expectedUrl))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                });

            // Act
            var result = await _service.GetModDetailsAsync(modName);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadModAsync_ReportsProgress()
        {
            // Arrange
            var downloadUrl = "https://example.com/mod.zip";
            var destinationPath = "mod.zip";
            var progressMock = new Mock<IProgress<(long, long?)>>();

            _httpClientMock.Setup(client => client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StreamContent(new MemoryStream(new byte[1000]))
                });

            // Act
            await _service.DownloadModAsync(downloadUrl, destinationPath, progressMock.Object);

            // Assert
            progressMock.Verify(p => p.Report(It.IsAny<(long, long?)>()), Times.AtLeastOnce);
        }
    }
}