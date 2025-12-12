using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for StateService state management and persistence
    /// </summary>
    public class StateServiceTests
    {
        private readonly Mock<IStateDatabaseService> _mockDb;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IEventService> _mockEvents;
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly StateService _sut;
        private readonly List<State> _stateStore;

        public StateServiceTests()
        {
            _mockDb = new Mock<IStateDatabaseService>();
            _mockConfig = new Mock<IConfiguration>();
            _mockEvents = new Mock<IEventService>();
            _mockSettings = new Mock<ISettingsService>();

            // Setup default settings
            _mockSettings.Setup(s => s.Settings).Returns(new AppSettings());

            // Setup configuration
            _mockConfig.Setup(c => c["StateVersion"]).Returns("1");

            // Setup database mocks
            _mockDb.Setup(db => db.GetState(It.IsAny<int>())).ReturnsAsync((State)null);

            _sut = new StateService(_mockDb.Object, _mockConfig.Object, _mockEvents.Object, _mockSettings.Object);
        }

        #region Constructor and Initialization Tests

        [Fact]
        public void Constructor_ShouldInitializeStateWithDefaults()
        {
            // Assert
            _sut.State.Should().NotBeNull();
            _sut.ParametersTxt2Img.Should().NotBeNull();
            _sut.ParametersImg2Img.Should().NotBeNull();
            _sut.ParametersUpscale.Should().NotBeNull();
            _sut.ParametersImg2Vid.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_ShouldInitializeParametersWithDefaults()
        {
            // Assert - These should match AppSettings defaults
            _sut.ParametersTxt2Img.Steps.Should().Be(30); // AppSettings default
            _sut.ParametersTxt2Img.CfgScale.Should().Be(7.5f); // AppSettings default
            _sut.ParametersTxt2Img.Width.Should().Be(512);
            _sut.ParametersTxt2Img.Height.Should().Be(768); // AppSettings default height
        }

        [Fact]
        public void InitializeParameters_ShouldReinitializeAllModes()
        {
            // Arrange
            var modes = new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Extras, ModeType.Img2Vid };
            var oldTxt2ImgParams = _sut.ParametersTxt2Img;

            // Act
            _sut.InitializeParameters(modes);

            // Assert
            _sut.ParametersTxt2Img.Should().NotBeSameAs(oldTxt2ImgParams);
            _sut.ParametersImg2Img.Should().NotBeNull();
            _sut.ParametersUpscale.Should().NotBeNull();
            _sut.ParametersImg2Vid.Should().NotBeNull();
        }

        [Fact]
        public void InitializeParameters_WithTxt2ImgOnly_ShouldOnlyInitializeTxt2Img()
        {
            // Arrange
            var modes = new[] { ModeType.Txt2Img };
            var oldImg2ImgParams = _sut.ParametersImg2Img;

            // Act
            _sut.InitializeParameters(modes);

            // Assert
            _sut.ParametersTxt2Img.Should().NotBeNull();
            // Other parameters should remain unchanged from constructor
            _sut.ParametersImg2Img.Should().BeSameAs(oldImg2ImgParams);
        }

        #endregion

        #region LoadState Tests

        [Fact]
        public async Task LoadState_WhenStateExists_ShouldLoadFromDatabase()
        {
            // Arrange
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                Txt2ImgParameters = new Txt2ImgParameters(new SharedParameters { Steps = 30 }),
                Img2ImgParameters = new Img2ImgParameters(new SharedParameters { Steps = 25 }),
                UpscaleParameters = new UpscaleParameters(new SharedParameters { Steps = 15 }),
                Img2VidParameters = new Img2VidParameters { Steps = 10 }
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _sut.State.Should().BeSameAs(savedState.AppState);
            _sut.ParametersTxt2Img.Should().BeSameAs(savedState.Txt2ImgParameters);
            _sut.ParametersTxt2Img.Steps.Should().Be(30);
            _sut.ParametersImg2Img.Steps.Should().Be(25);
            _sut.ParametersUpscale.Steps.Should().Be(15);
            _sut.ParametersImg2Vid.Steps.Should().Be(10);
        }

        [Fact]
        public async Task LoadState_WhenStateExists_ShouldPublishEvents()
        {
            // Arrange
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                Txt2ImgParameters = new Txt2ImgParameters(new SharedParameters()),
                Img2ImgParameters = new Img2ImgParameters(new SharedParameters()),
                UpscaleParameters = new UpscaleParameters(new SharedParameters()),
                Img2VidParameters = new Img2VidParameters()
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.AppState)), Times.Once);
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.Txt2ImgParameters)), Times.Once);
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.Img2ImgParameters)), Times.Once);
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.UpscaleParameters)), Times.Once);
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.Img2VidParameters)), Times.Once);
        }

        [Fact]
        public async Task LoadState_WhenStateDoesNotExist_ShouldSaveDefaultState()
        {
            // Arrange
            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync((State)null);
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>())).ReturnsAsync((State s) => s);

            // Act
            await _sut.LoadState();

            // Assert
            _mockDb.Verify(db => db.UpdateState(It.IsAny<State>()), Times.Once);
        }

        [Fact]
        public async Task LoadState_ShouldResetIsInterruptedFlag()
        {
            // Arrange
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()) { Generation = new AppStateGeneration { IsInterrupted = true } },
                Txt2ImgParameters = new Txt2ImgParameters(new SharedParameters()),
                Img2ImgParameters = new Img2ImgParameters(new SharedParameters()),
                UpscaleParameters = new UpscaleParameters(new SharedParameters()),
                Img2VidParameters = new Img2VidParameters()
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _sut.State.Generation.IsInterrupted.Should().BeFalse();
        }

        #endregion

        #region SaveState Tests

        [Fact]
        public async Task SaveState_WhenStateExists_ShouldUpdateExistingState()
        {
            // Arrange
            var existingState = new State
            {
                Id = 1,
                Title = "AutoSave",
                CreationDate = DateTime.Now.AddDays(-1),
                Version = 1
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(existingState);
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>())).ReturnsAsync((State s) => s);

            // Act
            await _sut.SaveState();

            // Assert
            _mockDb.Verify(db => db.UpdateState(It.Is<State>(s =>
                s.Id == 1 &&
                s.AppState == _sut.State &&
                s.Txt2ImgParameters == _sut.ParametersTxt2Img &&
                s.Img2ImgParameters == _sut.ParametersImg2Img &&
                s.UpscaleParameters == _sut.ParametersUpscale &&
                s.Img2VidParameters == _sut.ParametersImg2Vid
            )), Times.Once);
        }

        [Fact]
        public async Task SaveState_WhenStateDoesNotExist_ShouldCreateNewState()
        {
            // Arrange
            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync((State)null);
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>())).ReturnsAsync((State s) => s);

            // Act
            await _sut.SaveState();

            // Assert
            _mockDb.Verify(db => db.UpdateState(It.Is<State>(s =>
                s.Title == "AutoSave" &&
                s.Version == 1 &&
                s.AppState == _sut.State
            )), Times.Once);
        }

        #endregion

        #region State Property Tests

        [Fact]
        public void State_ShouldBeAccessible()
        {
            // Assert
            _sut.State.Should().NotBeNull();
            _sut.State.Should().BeOfType<AppState>();
        }

        [Fact]
        public void ParametersImg2Vid_ShouldHaveWorkflowAssetsInitialized()
        {
            // Assert
            _sut.ParametersImg2Vid.WorkflowAssets.Should().NotBeNull();
            _sut.ParametersImg2Vid.WorkflowAssets.Should().ContainKey("HighModel");
            _sut.ParametersImg2Vid.WorkflowAssets.Should().ContainKey("LowModel");
            _sut.ParametersImg2Vid.WorkflowAssets.Should().ContainKey("Clip");
            _sut.ParametersImg2Vid.WorkflowAssets.Should().ContainKey("ClipVision");
            _sut.ParametersImg2Vid.WorkflowAssets.Should().ContainKey("Vae");
        }

        [Fact]
        public void ParametersImg2Vid_ShouldHaveFrameInterpolationInitialized()
        {
            // Assert
            _sut.ParametersImg2Vid.FrameInterpolation.Should().NotBeNull();
            _sut.ParametersImg2Vid.FrameInterpolation.IsActive.Should().BeTrue();
            _sut.ParametersImg2Vid.FrameInterpolation.ScaleBy.Should().Be(2.0);
            _sut.ParametersImg2Vid.FrameInterpolation.Multiplier.Should().Be(2);
        }

        #endregion

        #region Parameter Default Value Tests

        [Theory]
        [InlineData(ModeType.Txt2Img)]
        [InlineData(ModeType.Img2Img)]
        public void Parameters_ShouldHaveCorrectDefaultSharedValues(ModeType mode)
        {
            // Arrange
            var parameters = mode == ModeType.Txt2Img ?
                (SharedParameters)_sut.ParametersTxt2Img :
                (SharedParameters)_sut.ParametersImg2Img;

            // Assert - These should match AppSettings.Generation.Shared defaults
            parameters.Steps.Should().Be(30); // AppSettings default
            parameters.Seed.Should().Be(-1);
            parameters.CfgScale.Should().Be(7.5f); // AppSettings default
            parameters.Width.Should().Be(512);
            parameters.Height.Should().Be(768); // AppSettings default
            parameters.NIter.Should().Be(1); // Batch.Count.Value
            parameters.BatchSize.Should().Be(4); // Batch.Size.Value
            parameters.DenoisingStrength.Should().Be(0.52); // AppSettings Denoising.Value
            parameters.RestoreFaces.Should().BeFalse(); // AppSettings FaceRestoration
            parameters.Tiling.Should().BeFalse(); // AppSettings Tilling
        }

        [Fact]
        public void ParametersTxt2Img_ShouldHaveCorrectHighResDefaults()
        {
            // Assert
            _sut.ParametersTxt2Img.EnableHR.Should().BeFalse();
            _sut.ParametersTxt2Img.HRUpscaler.Should().Be("Latent");
            _sut.ParametersTxt2Img.HRScale.Should().Be(2.0);
            _sut.ParametersTxt2Img.HRSecondPassSteps.Should().Be(0);
        }

        [Fact]
        public void ParametersImg2Img_ShouldHaveCorrectInpaintDefaults()
        {
            // Assert
            _sut.ParametersImg2Img.MaskBlur.Should().Be(4);
            _sut.ParametersImg2Img.ResizeMode.Should().Be(0);
            _sut.ParametersImg2Img.InpaintingFill.Should().Be(1);
            _sut.ParametersImg2Img.InpaintFullRes.Should().BeTrue();
            _sut.ParametersImg2Img.InpaintFullResPadding.Should().Be(32);
        }

        [Fact]
        public void ParametersUpscale_ShouldHaveCorrectDefaults()
        {
            // Assert
            _sut.ParametersUpscale.UpscalingMultiplier.Should().Be(2);
            _sut.ParametersUpscale.ShowResults.Should().BeTrue();
            _sut.ParametersUpscale.UpscalerPrimary.Should().Be("None");
            _sut.ParametersUpscale.UpscalingCrop.Should().BeTrue();
        }

        [Fact]
        public void ParametersImg2Vid_ShouldHaveCorrectDefaults()
        {
            // Assert
            _sut.ParametersImg2Vid.Length.Should().Be(81);
            _sut.ParametersImg2Vid.FrameRate.Should().Be(16);
            _sut.ParametersImg2Vid.Steps.Should().Be(8);
            _sut.ParametersImg2Vid.CfgScale.Should().Be(1.0f);
            _sut.ParametersImg2Vid.SamplerName.Should().Be("euler");
            _sut.ParametersImg2Vid.Scheduler.Should().Be("simple");
        }

        #endregion
    }
}
