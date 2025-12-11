using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Tests.MockBuilders;
using BlazorWebApp.Tests.TestFixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    public class BackendServiceTests
    {
        #region Health Check Tests

        [Fact]
        public async Task CheckBackendAvailability_WhenComfyUIAvailable_ReturnsTrue()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            var result = await service.CheckBackendAvailability();

            // Assert
            result.Should().BeTrue();
            service.IsBackendAvailable.Should().BeTrue();
            mockComfyUI.Verify(x => x.CheckComfyUIState(), Times.Once);
        }

        [Fact]
        public async Task CheckBackendAvailability_WhenComfyUIUnavailable_ReturnsFalse()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(false)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            var result = await service.CheckBackendAvailability();

            // Assert
            result.Should().BeFalse();
            service.IsBackendAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task CheckBackendAvailability_WhenException_ReturnsFalse()
        {
            // Arrange
            var mockComfyUI = new Mock<IComfyUIService>();
            mockComfyUI.Setup(x => x.CheckComfyUIState())
                .ThrowsAsync(new Exception("Connection failed"));

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            var result = await service.CheckBackendAvailability();

            // Assert
            result.Should().BeFalse();
            service.IsBackendAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task CheckBackendAvailability_PublishesEvent_WhenStateChanges()
        {
            // Arrange
            var mockComfyUI = new Mock<IComfyUIService>();
            mockComfyUI.SetupSequence(x => x.CheckComfyUIState())
                .ReturnsAsync(true)  // First call - changes from default false to true
                .ReturnsAsync(false); // Second call - changes from true to false

            var mockEvents = new Mock<IEventService>();
            BackendAvailabilityChangedEventArgs? capturedEvent = null;
            mockEvents.Setup(x => x.Publish(It.IsAny<BackendAvailabilityChangedEventArgs>()))
                .Callback<BackendAvailabilityChangedEventArgs>(e => capturedEvent = e);

            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            await service.CheckBackendAvailability(); // true (changed from false)
            await service.CheckBackendAvailability(); // false (changed from true)

            // Assert
            mockEvents.Verify(x => x.Publish(It.IsAny<BackendAvailabilityChangedEventArgs>()), Times.Exactly(2));
            capturedEvent.Should().NotBeNull();
            capturedEvent!.IsAvailable.Should().BeFalse(); // Last captured event
        }

        [Fact]
        public async Task CheckBackendAvailability_DoesNotPublishEvent_WhenStateUnchanged()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            await service.CheckBackendAvailability(); // true (first time, publishes)
            await service.CheckBackendAvailability(); // true (unchanged, should not publish)

            // Assert
            mockEvents.Verify(x => x.Publish(It.IsAny<BackendAvailabilityChangedEventArgs>()), Times.Once);
        }

        #endregion

        #region Resource Loading Tests

        [Fact]
        public async Task LoadBackendDependentResources_WhenBackendAvailable_LoadsSamplers()
        {
            // Arrange
            var samplers = BackendTestFixtures.GetSampleSamplers();
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .WithSamplers(samplers)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability(); // Set backend as available

            // Act
            await service.LoadBackendDependentResources();

            // Assert
            service.Samplers.Should().HaveCount(4);
            service.Samplers.Should().BeEquivalentTo(samplers);
            mockComfyUI.Verify(x => x.GetSamplers(), Times.Once);
        }

        [Fact]
        public async Task LoadBackendDependentResources_WhenBackendAvailable_LoadsSchedulers()
        {
            // Arrange
            var schedulers = BackendTestFixtures.GetSampleSchedulers();
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .WithSchedulers(schedulers)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability();

            // Act
            await service.LoadBackendDependentResources();

            // Assert
            service.Schedulers.Should().HaveCount(4);
            service.Schedulers.Should().BeEquivalentTo(schedulers);
            mockComfyUI.Verify(x => x.GetSchedulers(), Times.Once);
        }

        [Fact]
        public async Task LoadBackendDependentResources_WhenBackendAvailable_LoadsUpscalers()
        {
            // Arrange
            var upscalers = BackendTestFixtures.GetSampleUpscalers();
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .WithUpscalers(upscalers)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability();

            // Act
            await service.LoadBackendDependentResources();

            // Assert
            service.Upscalers.Should().HaveCount(4);
            service.Upscalers.Should().BeEquivalentTo(upscalers);
            mockComfyUI.Verify(x => x.GetUpscalers(), Times.Once);
        }

        [Fact]
        public async Task LoadBackendDependentResources_WhenBackendUnavailable_DoesNotLoad()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(false)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability(); // Backend unavailable

            // Act
            await service.LoadBackendDependentResources();

            // Assert
            service.Samplers.Should().BeEmpty();
            service.Schedulers.Should().BeEmpty();
            service.Upscalers.Should().BeEmpty();
            mockComfyUI.Verify(x => x.GetSamplers(), Times.Never);
            mockComfyUI.Verify(x => x.GetSchedulers(), Times.Never);
            mockComfyUI.Verify(x => x.GetUpscalers(), Times.Never);
        }

        [Fact]
        public async Task LoadBackendDependentResources_LoadsAllResourcesInOneCall()
        {
            // Arrange
            var samplers = BackendTestFixtures.GetSampleSamplers();
            var schedulers = BackendTestFixtures.GetSampleSchedulers();
            var upscalers = BackendTestFixtures.GetSampleUpscalers();

            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .WithSamplers(samplers)
                .WithSchedulers(schedulers)
                .WithUpscalers(upscalers)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability();

            // Act
            await service.LoadBackendDependentResources();

            // Assert
            service.Samplers.Should().HaveCount(4);
            service.Schedulers.Should().HaveCount(4);
            service.Upscalers.Should().HaveCount(4);
            mockComfyUI.Verify(x => x.GetSamplers(), Times.Once);
            mockComfyUI.Verify(x => x.GetSchedulers(), Times.Once);
            mockComfyUI.Verify(x => x.GetUpscalers(), Times.Once);
        }

        #endregion

        #region Options Management Tests

        [Fact]
        public async Task GetOptions_WhenBackendAvailable_ReturnsOptions()
        {
            // Arrange
            var expectedOptions = BackendTestFixtures.GetSampleOptions();
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .WithOptions(expectedOptions)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability();

            // Act
            await service.GetOptions();

            // Assert
            service.Options.Should().NotBeNull();
            service.Options.ClipSkip.Should().Be(2);
            service.Options.SaveTxt.Should().BeTrue();
            mockComfyUI.Verify(x => x.GenerateOptions(), Times.Once);
        }

        [Fact]
        public async Task GetOptions_WhenBackendUnavailable_ReturnsEmptyOptions()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(false)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability(); // Backend unavailable

            // Act
            await service.GetOptions();

            // Assert
            service.Options.Should().NotBeNull();
            service.Options.Should().BeEquivalentTo(new Options());
            mockComfyUI.Verify(x => x.GenerateOptions(), Times.Never);
        }

        [Fact]
        public async Task PostOptions_WhenBackendUnavailable_ReturnsErrorMessage()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(false)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            await service.CheckBackendAvailability(); // Backend unavailable

            var options = new Options();

            // Act
            var result = await service.PostOptions(options);

            // Assert
            result.Should().Be("Backend not available");
            mockComfyUI.Verify(x => x.GenerateOptions(), Times.Never);
        }

        #endregion

        #region Monitoring Tests

        [Fact]
        public void StartMonitoring_StartsPeriodicHealthChecks()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);

            // Act
            service.StartMonitoring(intervalSeconds: 1);

            // Assert - monitoring is started (timer is internal, just verify no exceptions)
            // In a real scenario, we'd verify health checks are called periodically
            // For now, we verify the method doesn't throw
            service.StopMonitoring();
        }

        [Fact]
        public void StopMonitoring_StopsPeriodicHealthChecks()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object);
            service.StartMonitoring(intervalSeconds: 1);

            // Act
            service.StopMonitoring();

            // Assert - monitoring is stopped (timer disposed, verify no exceptions)
            // Multiple stops should not throw
            service.StopMonitoring();
        }

        #endregion
    }
}
