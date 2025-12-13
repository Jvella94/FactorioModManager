using FactorioModManager;
using System;
using Xunit;

namespace Tests.ServicesTests
{
    public class ServiceContainerTests
    {
        [Fact]
        public void Resolve_ReturnsRegisteredSingleton()
        {
            // Arrange
            var container = ServiceContainer.Instance;
            var testService = new TestService();
            container.RegisterSingleton(testService);

            // Act
            var resolvedService = container.Resolve<TestService>();

            // Assert
            Assert.Equal(testService, resolvedService);
        }

        [Fact]
        public void Resolve_ThrowsExceptionForUnregisteredService()
        {
            // Arrange
            var container = ServiceContainer.Instance;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.Resolve<UnregisteredService>());
        }

        [Fact]
        public void IsRegistered_ReturnsTrueForRegisteredService()
        {
            // Arrange
            var container = ServiceContainer.Instance;
            var testService = new TestService();
            container.RegisterSingleton(testService);

            // Act
            var isRegistered = container.IsRegistered<TestService>();

            // Assert
            Assert.True(isRegistered);
        }

        [Fact]
        public void IsRegistered_ReturnsFalseForUnregisteredService()
        {
            // Arrange
            var container = ServiceContainer.Instance;

            // Act
            var isRegistered = container.IsRegistered<UnregisteredService>();

            // Assert
            Assert.False(isRegistered);
        }

        private class TestService
        {
        }

        private class UnregisteredService
        {
        }
    }
}