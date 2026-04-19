using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Integration
{
    /// <summary>
    /// Integration tests verifying multi-service coordination and state management.
    /// These tests use real service instances where practical, mocking only external dependencies.
    /// </summary>
    public class ServiceIntegrationTests
    {
        #region State Persistence Round-Trip Tests

        /// <summary>
        /// Verifies that state persists correctly through a save/load cycle with real StateService.
        /// </summary>
        [Fact]
        public async Task StatePersistence_SaveAndLoad_ShouldPreserveAllParameters()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockEvents = new Mock<IEventService>();
            var mockSettings = new Mock<ISettingsService>();
            
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings());
            mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            State? capturedState = null;
            mockDb.Setup(db => db.UpdateState(It.IsAny<State>()))
                .Callback<State>(s => capturedState = s)
                .ReturnsAsync((State s) => s);

            var stateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);

            // Set up specific parameter values using GenerationParameters
            var promptsFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
            promptsFragment.SetValue(FragmentKeys.Params.Positive, "test prompt for round-trip");
            promptsFragment.SetValue(FragmentKeys.Params.Negative, "test negative prompt");
            
            var samplerFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 42);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, 8.5);
            samplerFragment.SetValue(FragmentKeys.Params.Seed, 12345L);
            
            var latentFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
            latentFragment.SetValue(FragmentKeys.Params.Width, 768);
            latentFragment.SetValue(FragmentKeys.Params.Height, 1024);
            
            stateService.GenerationParameters.Assets["Model"] = "test_model.safetensors";
            stateService.GenerationParameters.Assets["Vae"] = "test_vae.safetensors";
            
            stateService.State.Generation.WorkflowBase = ModelBase.Flux;

            // Act - Save state
            await stateService.SaveState();

            // Verify capture
            capturedState.Should().NotBeNull("State should have been saved to database");

            // Set up mock to return captured state
            mockDb.Setup(db => db.GetState(1)).ReturnsAsync(capturedState);

            // Create new StateService instance to simulate fresh app startup
            var newStateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);
            await newStateService.LoadState();

            // Assert - Verify all parameters survived the round-trip
            var loadedPrompts = newStateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
            loadedPrompts.GetValue<string>(FragmentKeys.Params.Positive).Should().Be("test prompt for round-trip");
            loadedPrompts.GetValue<string>(FragmentKeys.Params.Negative).Should().Be("test negative prompt");
            
            var loadedSampler = newStateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            loadedSampler.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(42);
            loadedSampler.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(8.5);
            loadedSampler.GetValue<long>(FragmentKeys.Params.Seed).Should().Be(12345L);
            
            var loadedLatent = newStateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
            loadedLatent.GetValue<int>(FragmentKeys.Params.Width).Should().Be(768);
            loadedLatent.GetValue<int>(FragmentKeys.Params.Height).Should().Be(1024);
            
            newStateService.GenerationParameters.Assets.Should().ContainKey("Model");
            newStateService.GenerationParameters.Assets["Model"].Should().Be("test_model.safetensors");
            newStateService.GenerationParameters.Assets["Vae"].Should().Be("test_vae.safetensors");

            newStateService.State.Generation.WorkflowBase.Should().Be(ModelBase.Flux);
        }

        /// <summary>
        /// Verifies that video generation parameters persist correctly through save/load.
        /// </summary>
        [Fact]
        public async Task StatePersistence_VideoParameters_ShouldPreserveAllFields()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockEvents = new Mock<IEventService>();
            var mockSettings = new Mock<ISettingsService>();
            
            mockSettings.Setup(s => s.Settings).Returns(new AppSettings());
            mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            State? capturedState = null;
            mockDb.Setup(db => db.UpdateState(It.IsAny<State>()))
                .Callback<State>(s => capturedState = s)
                .ReturnsAsync((State s) => s);

            var stateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);

            // Set up video generation parameters using GenerationParameters
            var promptsFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
            promptsFragment.SetValue(FragmentKeys.Params.Positive, "video generation prompt");
            
            var videoFragment = stateService.GenerationParameters.GetOrCreateFragment("video_settings");
            videoFragment.SetValue("length", 97);
            videoFragment.SetValue("frame_rate", 24);
            videoFragment.SetValue("motion_amplitude", 1.5f);
            
            var samplerFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 12);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, 2.5);
            samplerFragment.SetValue("shift", 7);
            
            stateService.GenerationParameters.Assets["HighModel"] = "wan_high.safetensors";
            stateService.GenerationParameters.Assets["LowModel"] = "wan_low.safetensors";
            stateService.GenerationParameters.Assets["Clip"] = "clip_model.safetensors";

            // Act - Save and reload
            await stateService.SaveState();
            mockDb.Setup(db => db.GetState(1)).ReturnsAsync(capturedState);
            
            var newStateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);
            await newStateService.LoadState();

            // Assert
            var loadedPrompts = newStateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
            loadedPrompts.GetValue<string>(FragmentKeys.Params.Positive).Should().Be("video generation prompt");
            
            var loadedVideo = newStateService.GenerationParameters.GetOrCreateFragment("video_settings");
            loadedVideo.GetValue<int>("length").Should().Be(97);
            loadedVideo.GetValue<int>("frame_rate").Should().Be(24);
            
            var loadedSampler = newStateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            loadedSampler.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(12);
            
            newStateService.GenerationParameters.Assets["HighModel"].Should().Be("wan_high.safetensors");
            newStateService.GenerationParameters.Assets["LowModel"].Should().Be("wan_low.safetensors");
        }

        #endregion

        #region Event Propagation Tests

        /// <summary>
        /// Verifies that events flow correctly through EventService to multiple subscribers.
        /// </summary>
        [Fact]
        public void EventPropagation_MultipleSubscribers_ShouldReceiveEvents()
        {
            // Arrange
            var eventService = new EventService();
            var receivedBySubscriber1 = new List<string>();
            var receivedBySubscriber2 = new List<string>();
            var receivedBySubscriber3 = new List<string>();

            Action<ParametersChangedEventArgs> subscriber1 = e => receivedBySubscriber1.Add(e.ParametersType);
            Action<ParametersChangedEventArgs> subscriber2 = e => receivedBySubscriber2.Add(e.ParametersType);
            Action<ParametersChangedEventArgs> subscriber3 = e => receivedBySubscriber3.Add(e.ParametersType);

            eventService.Subscribe(subscriber1);
            eventService.Subscribe(subscriber2);
            eventService.Subscribe(subscriber3);

            // Act
            eventService.Publish(new ParametersChangedEventArgs("Txt2Img"));
            eventService.Publish(new ParametersChangedEventArgs("Img2Img"));

            // Assert
            receivedBySubscriber1.Should().HaveCount(2);
            receivedBySubscriber1.Should().Contain("Txt2Img");
            receivedBySubscriber1.Should().Contain("Img2Img");

            receivedBySubscriber2.Should().HaveCount(2);
            receivedBySubscriber3.Should().HaveCount(2);
        }

        /// <summary>
        /// Verifies that different event types are isolated correctly.
        /// </summary>
        [Fact]
        public void EventPropagation_DifferentEventTypes_ShouldBeIsolated()
        {
            // Arrange
            var eventService = new EventService();
            var stateEvents = new List<StateChangedEventArgs>();
            var progressEvents = new List<ProgressChangedEventArgs>();
            var workflowEvents = new List<WorkflowChangedEventArgs>();

            eventService.Subscribe<StateChangedEventArgs>(e => stateEvents.Add(e));
            eventService.Subscribe<ProgressChangedEventArgs>(e => progressEvents.Add(e));
            eventService.Subscribe<WorkflowChangedEventArgs>(e => workflowEvents.Add(e));

            // Act
            eventService.Publish(new StateChangedEventArgs());
            eventService.Publish(new StateChangedEventArgs());
            eventService.Publish(new ProgressChangedEventArgs { Value = 50f });
            eventService.Publish(new WorkflowChangedEventArgs(Guid.NewGuid(), "Test"));

            // Assert
            stateEvents.Should().HaveCount(2);
            progressEvents.Should().HaveCount(1);
            progressEvents[0].Value.Should().Be(50f);
            workflowEvents.Should().HaveCount(1);
        }

        /// <summary>
        /// Verifies that unsubscribed handlers no longer receive events.
        /// </summary>
        [Fact]
        public void EventPropagation_AfterUnsubscribe_ShouldStopReceiving()
        {
            // Arrange
            var eventService = new EventService();
            var receivedEvents = new List<string>();

            Action<ParametersChangedEventArgs> handler = e => receivedEvents.Add(e.ParametersType);
            eventService.Subscribe(handler);

            // Act
            eventService.Publish(new ParametersChangedEventArgs("First"));
            eventService.Unsubscribe(handler);
            eventService.Publish(new ParametersChangedEventArgs("Second"));

            // Assert
            receivedEvents.Should().HaveCount(1);
            receivedEvents[0].Should().Be("First");
        }

        #endregion

        #region Progress Service Integration Tests

        /// <summary>
        /// Verifies ProgressService correctly publishes events when state changes.
        /// </summary>
        [Fact]
        public void ProgressService_WhenConvergingChanges_ShouldPublishEvent()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProgressService>>();
            var eventService = new EventService();
            var progressService = new ProgressService(mockLogger.Object, eventService);

            var receivedEvents = new List<ConvergingChangedEventArgs>();
            eventService.Subscribe<ConvergingChangedEventArgs>(e => receivedEvents.Add(e));

            // Act
            progressService.IsConverging = true;
            progressService.IsConverging = false;
            progressService.IsConverging = true;

            // Assert
            receivedEvents.Should().HaveCount(3);
            receivedEvents[0].IsConverging.Should().BeTrue();
            receivedEvents[1].IsConverging.Should().BeFalse();
            receivedEvents[2].IsConverging.Should().BeTrue();
        }

        /// <summary>
        /// Verifies ProgressService correctly publishes events when progress changes.
        /// </summary>
        [Fact]
        public void ProgressService_WhenProgressChanges_ShouldPublishEvent()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ProgressService>>();
            var eventService = new EventService();
            var progressService = new ProgressService(mockLogger.Object, eventService);

            var receivedValues = new List<float>();
            eventService.Subscribe<ProgressChangedEventArgs>(e => receivedValues.Add(e.Value));

            // Act
            progressService.CurrentProgress = 25;
            progressService.CurrentProgress = 50;
            progressService.CurrentProgress = 75;
            progressService.CurrentProgress = 100;

            // Assert
            receivedValues.Should().HaveCount(4);
            receivedValues.Should().Equal(25f, 50f, 75f, 100f);
        }

        #endregion

        #region Gallery and Session Service Integration Tests

        /// <summary>
        /// Verifies GalleryService correctly publishes events when selection changes.
        /// </summary>
        [Fact]
        public void GalleryService_WhenSelectionChanges_ShouldPublishEvent()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockState = new Mock<IStateService>();
            var eventService = new EventService();
            var galleryService = new GalleryService(mockDb.Object, mockState.Object, eventService);

            var receivedEvents = new List<ImageSelectionChangedEventArgs>();
            eventService.Subscribe<ImageSelectionChangedEventArgs>(e => receivedEvents.Add(e));

            // Act
            galleryService.AddSelectedImage(1);
            galleryService.AddSelectedImage(2);
            galleryService.RemoveSelectedImage(1);
            galleryService.ClearSelectedImages();

            // Assert
            receivedEvents.Should().HaveCount(4);
        }

        /// <summary>
        /// Verifies SessionService correctly publishes events when input image changes.
        /// </summary>
        [Fact]
        public void SessionService_WhenInputImageChanges_ShouldPublishEvent()
        {
            // Arrange
            var eventService = new EventService();
            var sessionService = new SessionService(eventService);

            var receivedEvents = new List<Img2ImgInputImageChangedEventArgs>();
            eventService.Subscribe<Img2ImgInputImageChangedEventArgs>(e => receivedEvents.Add(e));

            // Act
            sessionService.SetImg2ImgInputImage("base64data1", resetEditorState: true);
            sessionService.SetImg2ImgInputImage("base64data2", resetEditorState: false);

            // Assert
            receivedEvents.Should().HaveCount(2);
            receivedEvents[0].ImageData.Should().Be("base64data1");
            receivedEvents[1].ImageData.Should().Be("base64data2");
        }

        /// <summary>
        /// Verifies SessionService correctly manages video collection.
        /// </summary>
        [Fact]
        public void SessionService_VideoManagement_ShouldTrackVideosCorrectly()
        {
            // Arrange
            var eventService = new EventService();
            var sessionService = new SessionService(eventService);

            var video1 = new GeneratedVideo { FilePath = "/path/to/video1.mp4" };
            var video2 = new GeneratedVideo { FilePath = "/path/to/video2.mp4" };
            var video3 = new GeneratedVideo { FilePath = "/path/to/video3.mp4" };

            // Act & Assert - Add videos
            sessionService.AddSessionVideo(video1);
            sessionService.SessionGeneratedVideos.Videos.Should().HaveCount(1);

            sessionService.AddSessionVideos(new[] { video2, video3 });
            sessionService.SessionGeneratedVideos.Videos.Should().HaveCount(3);

            // Act & Assert - Remove video
            sessionService.RemoveSessionVideo(video2);
            sessionService.SessionGeneratedVideos.Videos.Should().HaveCount(2);
            sessionService.SessionGeneratedVideos.Videos.Should().NotContain(video2);

            // Act & Assert - Clear all
            sessionService.ClearSessionVideos();
            sessionService.SessionGeneratedVideos.Videos.Should().BeEmpty();
        }

        #endregion

        #region Multi-Service Workflow Tests

        /// <summary>
        /// Verifies that state changes propagate correctly through event system to multiple services.
        /// </summary>
        [Fact]
        public async Task MultiService_StateChangesPropagateCorrectly()
        {
            // Arrange - Create real EventService and StateService
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockSettings = new Mock<ISettingsService>();
            var eventService = new EventService();

            mockSettings.Setup(s => s.Settings).Returns(new AppSettings());
            mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            var stateService = new StateService(mockDb.Object, mockConfig.Object, eventService, mockSettings.Object);

            // Track events received
            var stateChangedCount = 0;
            eventService.Subscribe<StateChangedEventArgs>(_ => stateChangedCount++);

            // Setup save mock
            mockDb.Setup(db => db.UpdateState(It.IsAny<State>())).ReturnsAsync((State s) => s);

            // Setup load mock with saved state
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                GenerationParameters = new GenerationParameters()
            };
            mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await stateService.LoadState();

            // Assert - StateService should publish events when state is loaded
            stateChangedCount.Should().BeGreaterThan(0, "LoadState should publish state changed events");
        }

        /// <summary>
        /// Verifies GalleryService selection state is isolated per service instance.
        /// </summary>
        [Fact]
        public void MultiService_GalleryServiceInstances_ShouldBeIndependent()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockState = new Mock<IStateService>();
            var eventService1 = new EventService();
            var eventService2 = new EventService();

            var gallery1 = new GalleryService(mockDb.Object, mockState.Object, eventService1);
            var gallery2 = new GalleryService(mockDb.Object, mockState.Object, eventService2);

            // Act
            gallery1.AddSelectedImage(1);
            gallery1.AddSelectedImage(2);
            gallery2.AddSelectedImage(100);

            // Assert
            gallery1.SelectedImageIds.Should().HaveCount(2);
            gallery1.SelectedImageIds.Should().Contain(new[] { 1, 2 });

            gallery2.SelectedImageIds.Should().HaveCount(1);
            gallery2.SelectedImageIds.Should().Contain(100);
        }

        /// <summary>
        /// Verifies SessionService instances maintain isolated state.
        /// </summary>
        [Fact]
        public void MultiService_SessionServiceInstances_ShouldBeIndependent()
        {
            // Arrange
            var eventService1 = new EventService();
            var eventService2 = new EventService();

            var session1 = new SessionService(eventService1);
            var session2 = new SessionService(eventService2);

            // Act
            session1.Img2ImgInputImage = "data:image/png;base64,abc123";
            session1.AddSessionVideo(new GeneratedVideo { FilePath = "/video1.mp4" });

            session2.Img2ImgInputImage = "data:image/png;base64,xyz789";

            // Assert
            session1.Img2ImgInputImage.Should().Be("data:image/png;base64,abc123");
            session1.SessionGeneratedVideos.Videos.Should().HaveCount(1);

            session2.Img2ImgInputImage.Should().Be("data:image/png;base64,xyz789");
            session2.SessionGeneratedVideos.Videos.Should().BeEmpty();
        }

        #endregion

        #region Settings and State Interaction Tests

        /// <summary>
        /// Verifies that custom settings affect StateService initialization.
        /// </summary>
        [Fact]
        public void SettingsToState_CustomSettings_ShouldAffectDefaults()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockEvents = new Mock<IEventService>();
            var mockSettings = new Mock<ISettingsService>();

            var customSettings = new AppSettings
            {
                Generation = new GenerationSettingsModel
                {
                    Shared = new SharedSettingsModel
                    {
                        Steps = new StepsSettingsModel { Value = 100 },
                        CfgScale = new CfgScaleSettingsModel { Value = 15.0f },
                        Resolution = new ResolutionSettingsModel { Width = 2048, Height = 2048 }
                    }
                }
            };

            mockSettings.Setup(s => s.Settings).Returns(customSettings);
            mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            // Act
            var stateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);

            // Assert - Defaults should come from custom settings
            var samplerFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(100);
            samplerFragment.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(15.0);
            
            var latentFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
            latentFragment.GetValue<int>(FragmentKeys.Params.Width).Should().Be(2048);
            latentFragment.GetValue<int>(FragmentKeys.Params.Height).Should().Be(2048);
        }

        /// <summary>
        /// Verifies InitializeGenerationParameters respects current settings.
        /// </summary>
        [Fact]
        public void SettingsToState_InitializeGenerationParameters_ShouldUseCurrentSettings()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            var mockEvents = new Mock<IEventService>();
            var mockSettings = new Mock<ISettingsService>();

            var initialSettings = new AppSettings
            {
                Generation = new GenerationSettingsModel
                {
                    Shared = new SharedSettingsModel
                    {
                        Steps = new StepsSettingsModel { Value = 30 }
                    }
                }
            };

            var updatedSettings = new AppSettings
            {
                Generation = new GenerationSettingsModel
                {
                    Shared = new SharedSettingsModel
                    {
                        Steps = new StepsSettingsModel { Value = 75 }
                    }
                }
            };

            mockSettings.Setup(s => s.Settings).Returns(initialSettings);
            mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            var stateService = new StateService(mockDb.Object, mockConfig.Object, mockEvents.Object, mockSettings.Object);
            var samplerFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(30);

            // Act - Change settings and reinitialize
            mockSettings.Setup(s => s.Settings).Returns(updatedSettings);
            stateService.InitializeGenerationParameters();

            // Assert
            samplerFragment = stateService.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(75);
        }

        #endregion
    }
}
