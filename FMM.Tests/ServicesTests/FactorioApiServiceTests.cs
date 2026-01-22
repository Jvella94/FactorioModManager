using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;

namespace FMM.Tests.ServicesTests
{
    public class FactorioApiServiceTests
    {
        private readonly Mock<HttpMessageHandler> _httpHandlerMock;
        private readonly Mock<ILogService> _logServiceMock;
        private readonly FactorioApiService _service;

        public FactorioApiServiceTests()
        {
            _httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _logServiceMock = new Mock<ILogService>();
            var client = new HttpClient(_httpHandlerMock.Object);
            _service = new FactorioApiService(client, _logServiceMock.Object);
        }

        [Fact]
        public async Task GetModDetailsAsync_ReturnsModDetails()
        {
            // Arrange
            var modName = "test-mod";
            var expectedUrl = $"https://mods.factorio.com/api/mods/{modName}?version=2.0&hide_deprecated=true";
            var responseContent = "{\"name\":\"test-mod\",\"title\":\"Test Mod\"}";

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response)
                .Verifiable();

            // Act
            var result = await _service.GetModDetailsAsync(modName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-mod", result.Name);
            _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == expectedUrl), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task GetModDetailsAsync_ReturnsNull_WhenApiFails()
        {
            // Arrange
            var modName = "test-mod";
            var expectedUrl = $"https://mods.factorio.com/api/mods/{modName}?version=2.0&hide_deprecated=true";

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            };

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == expectedUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response)
                .Verifiable();

            // Act
            var result = await _service.GetModDetailsAsync(modName);

            // Assert
            Assert.Null(result);
            _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == expectedUrl), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task DownloadModAsync_ReportsProgress()
        {
            // Arrange
            var downloadUrl = "https://example.com/mod.zip";
            var destinationPath = Path.Combine(Path.GetTempPath(), "mod_test.zip");
            var progressMock = new Mock<IProgress<(long, long?)>>();

            // Create a response with a stream of 1000 bytes
            var ms = new MemoryStream(new byte[1000]);
            ms.Position = 0;
            var content = new StreamContent(ms);
            content.Headers.ContentLength = 1000;
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = content
            };

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri!.ToString() == downloadUrl),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response)
                .Verifiable();

            // Act
            await _service.DownloadModAsync(downloadUrl, destinationPath, progress: progressMock.Object, cancellationToken: CancellationToken.None);

            // Assert
            progressMock.Verify(p => p.Report(It.IsAny<(long, long?)>()), Times.AtLeastOnce);
            _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == downloadUrl), ItExpr.IsAny<CancellationToken>());

            // Cleanup
            try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
        }
    }
}