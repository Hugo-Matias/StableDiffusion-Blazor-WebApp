using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Tests.MockBuilders;
using BlazorWebApp.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    public class BackendServiceTests
    {
        private static Mock<IConfiguration> CreateMockConfiguration(string outputDir = "C:\\Output")
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["OutputDir"]).Returns(outputDir);
            
            // Setup OutputPaths section
            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s["Txt2ImgSamples"]).Returns("Text-2-Image\\_samples");
            mockSection.Setup(s => s["Img2ImgSamples"]).Returns("Image-2-Image\\_samples");
            mockSection.Setup(s => s["Img2VidSamples"]).Returns("Image-2-Video\\_samples");
            mockSection.Setup(s => s["Extras"]).Returns("Extras");
            mockSection.Setup(s => s["DirectoryPattern"]).Returns("[model_name]/[sampler]");
            mockSection.Setup(s => s["FilenamePattern"]).Returns("[seed]_[steps]_[cfg]");
            mockSection.Setup(s => s["SamplesFormat"]).Returns("png");
            mockSection.Setup(s => s["SaveSamples"]).Returns("true");
            
            mockConfig.Setup(c => c.GetSection("OutputPaths")).Returns(mockSection.Object);
            
            return mockConfig;
        }

        #region Health Check Tests

        [Fact]
        public async Task CheckBackendAvailability_WhenComfyUIAvailable_ReturnsTrue()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder()
                .WithBackendAvailable(true)
                .Build();

            var mockEvents = new Mock<IEventService>();
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

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

            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
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

        #region Output Paths Tests

        [Fact]
        public void OutputPaths_IsInitializedWithDefaults()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder().Build();
            var mockEvents = new Mock<IEventService>();
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

            // Assert
            service.OutputPaths.Should().NotBeNull();
            service.OutputPaths.SamplesFormat.Should().Be("png");
            service.OutputPaths.SaveSamples.Should().BeTrue();
        }

        [Fact]
        public void GetOutputPath_ReturnsCorrectPath_ForTxt2ImgSamples()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder().Build();
            var mockEvents = new Mock<IEventService>();
            var mockConfig = CreateMockConfiguration("C:\\Output");
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

            // Act
            var path = service.GetOutputPath(Outdir.Txt2ImgSamples);

            // Assert
            path.Should().Contain("Output");
            path.Should().Contain("Text-2-Image");
        }

        [Fact]
        public void GetOutputPath_ReturnsCorrectPath_ForImg2ImgSamples()
        {
            // Arrange
            var mockComfyUI = new MockComfyUIServiceBuilder().Build();
            var mockEvents = new Mock<IEventService>();
            var mockConfig = CreateMockConfiguration("C:\\Output");
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

            // Act
            var path = service.GetOutputPath(Outdir.Img2ImgSamples);

            // Assert
            path.Should().Contain("Output");
            path.Should().Contain("Image-2-Image");
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);

            // Act
            service.StartMonitoring(intervalSeconds: 1);

            // Assert - monitoring is started (timer is internal, just verify no exceptions)
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
            var mockConfig = CreateMockConfiguration();
            var service = new BackendService(mockComfyUI.Object, mockEvents.Object, mockConfig.Object);
            service.StartMonitoring(intervalSeconds: 1);

            // Act
            service.StopMonitoring();

            // Assert - monitoring is stopped (timer disposed, verify no exceptions)
            service.StopMonitoring();
        }

        #endregion
    }
}
