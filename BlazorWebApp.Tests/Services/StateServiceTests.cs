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
        private readonly Mock<IDatabaseService> _mockDb;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IEventService> _mockEvents;
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly StateService _sut;

        public StateServiceTests()
        {
            _mockDb = new Mock<IDatabaseService>();
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
            _sut.GenerationParameters.Should().NotBeNull();
            _sut.GenerationParameters.Fragments.Should().NotBeEmpty();
        }

        [Fact]
        public void Constructor_ShouldInitializeGenerationParametersWithDefaults()
        {
            // Assert - These should match AppSettings defaults
            var samplerFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(30); // AppSettings default
            samplerFragment.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(7.5); // AppSettings default
            
            var latentFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
            latentFragment.GetValue<int>(FragmentKeys.Params.Width).Should().Be(512);
            latentFragment.GetValue<int>(FragmentKeys.Params.Height).Should().Be(768); // AppSettings default height
        }

        [Fact]
        public void InitializeGenerationParameters_ShouldReinitializeFragments()
        {
            // Arrange
            var oldFragmentCount = _sut.GenerationParameters.Fragments.Count;

            // Act
            _sut.InitializeGenerationParameters();

            // Assert
            _sut.GenerationParameters.Fragments.Should().NotBeEmpty();
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.Prompts);
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.MainSampler);
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.Latent);
        }

        #endregion

        #region LoadState Tests

        [Fact]
        public async Task LoadState_WhenStateExists_ShouldLoadFromDatabase()
        {
            // Arrange
            var savedGenParams = new GenerationParameters();
            var samplerFragment = savedGenParams.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Files.Sampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 30);
            
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                GenerationParameters = savedGenParams
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _sut.State.Should().BeSameAs(savedState.AppState);
            var loadedSampler = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            loadedSampler.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(30);
        }

        [Fact]
        public async Task LoadState_WhenStateExists_ShouldPublishEvents()
        {
            // Arrange
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                GenerationParameters = new GenerationParameters()
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _mockEvents.Verify(e => e.Publish(It.Is<StateChangedEventArgs>(
                args => args.ChangeType == StateChangeType.AppState)), Times.Once);
            _mockEvents.Verify(e => e.Publish(It.IsAny<GenerationParametersChangedEventArgs>()), Times.Once);
        }

        [Fact]
        public async Task LoadState_WhenStateDoesNotExist_ShouldUseDefaults()
        {
            // Arrange
            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync((State)null);

            // Act
            await _sut.LoadState();

            // Assert
            _sut.State.Should().NotBeNull();
            _sut.GenerationParameters.Should().NotBeNull();
            _sut.GenerationParameters.Fragments.Should().NotBeEmpty();
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
                s.GenerationParameters == _sut.GenerationParameters
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
                s.AppState == _sut.State &&
                s.GenerationParameters == _sut.GenerationParameters
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
        public void GenerationParameters_ShouldHaveAssetsInitialized()
        {
            // Assert
            _sut.GenerationParameters.Assets.Should().NotBeNull();
        }

        [Fact]
        public void GenerationParameters_ShouldHaveLorasInitialized()
        {
            // Assert
            _sut.GenerationParameters.Loras.Should().NotBeNull();
        }

        #endregion

        #region Parameter Default Value Tests

        [Fact]
        public void GenerationParameters_ShouldHaveCorrectDefaultSamplerValues()
        {
            // Arrange
            var samplerFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);

            // Assert - These should match AppSettings.Generation.Shared defaults
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(30); // AppSettings default
            samplerFragment.GetValue<long>(FragmentKeys.Params.Seed).Should().Be(-1);
            samplerFragment.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(7.5); // AppSettings default
            samplerFragment.GetValue<double>(FragmentKeys.Params.Denoise).Should().Be(0.52); // AppSettings Denoising.Value
        }

        [Fact]
        public void GenerationParameters_ShouldHaveCorrectDefaultLatentValues()
        {
            // Arrange
            var latentFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);

            // Assert
            latentFragment.GetValue<int>(FragmentKeys.Params.Width).Should().Be(512);
            latentFragment.GetValue<int>(FragmentKeys.Params.Height).Should().Be(768); // AppSettings default
            latentFragment.GetValue<int>(FragmentKeys.Params.BatchSize).Should().Be(4); // Batch.Size.Value
        }

        [Fact]
        public void GenerationParameters_ShouldHaveEmptyPromptsByDefault()
        {
            // Arrange
            var promptsFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);

            // Assert
            promptsFragment.GetValue<string>(FragmentKeys.Params.Positive).Should().BeEmpty();
            promptsFragment.GetValue<string>(FragmentKeys.Params.Negative).Should().BeEmpty();
        }

        #endregion

        #region Assets Persistence Tests

        [Fact]
        public async Task SaveState_PersistsAssets()
        {
            // Arrange
            _sut.GenerationParameters.Assets["Model"] = "test_model.safetensors";
            _sut.GenerationParameters.Assets["Vae"] = "test_vae.safetensors";

            State capturedState = null;
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>()))
                .Callback<State>(s => capturedState = s)
                .ReturnsAsync((State s) => s);

            // Act
            await _sut.SaveState();

            // Assert
            capturedState.Should().NotBeNull();
            capturedState.GenerationParameters.Assets.Should().NotBeNull();
            capturedState.GenerationParameters.Assets.Should().ContainKey("Model");
            capturedState.GenerationParameters.Assets["Model"].Should().Be("test_model.safetensors");
            capturedState.GenerationParameters.Assets["Vae"].Should().Be("test_vae.safetensors");
        }

        [Fact]
        public async Task LoadState_RestoresAssets()
        {
            // Arrange
            var savedGenParams = new GenerationParameters();
            savedGenParams.Assets["Model"] = "restored_model.safetensors";
            savedGenParams.Assets["Vae"] = "restored_vae.safetensors";
            
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                GenerationParameters = savedGenParams
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            _sut.GenerationParameters.Assets.Should().NotBeNull();
            _sut.GenerationParameters.Assets.Should().ContainKey("Model");
            _sut.GenerationParameters.Assets["Model"].Should().Be("restored_model.safetensors");
            _sut.GenerationParameters.Assets["Vae"].Should().Be("restored_vae.safetensors");
        }

        [Fact]
        public async Task Assets_SurvivesSaveLoadCycle()
        {
            // Arrange - Set up initial assets
            _sut.GenerationParameters.Assets["Model"] = "cycle_test_model.safetensors";
            _sut.GenerationParameters.Assets["Vae"] = "cycle_test_vae.safetensors";
            _sut.GenerationParameters.Assets["Clip"] = "cycle_test_clip.safetensors";

            State capturedState = null;
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>()))
                .Callback<State>(s => capturedState = s)
                .ReturnsAsync((State s) => s);

            // Act - Save state
            await _sut.SaveState();

            // Simulate loading by setting up mock to return captured state
            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(capturedState);

            // Create new StateService instance to simulate fresh load
            var newSut = new StateService(_mockDb.Object, _mockConfig.Object, _mockEvents.Object, _mockSettings.Object);
            await newSut.LoadState();

            // Assert - Verify assets survived the cycle
            newSut.GenerationParameters.Assets.Should().NotBeNull();
            newSut.GenerationParameters.Assets.Should().HaveCount(3);
            newSut.GenerationParameters.Assets["Model"].Should().Be("cycle_test_model.safetensors");
            newSut.GenerationParameters.Assets["Vae"].Should().Be("cycle_test_vae.safetensors");
            newSut.GenerationParameters.Assets["Clip"].Should().Be("cycle_test_clip.safetensors");
        }

        #endregion

        #region Fragments Persistence Tests

        [Fact]
        public async Task SaveState_PersistsFragments()
        {
            // Arrange
            var samplerFragment = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Files.Sampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 50);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, 10.0);

            State capturedState = null;
            _mockDb.Setup(db => db.UpdateState(It.IsAny<State>()))
                .Callback<State>(s => capturedState = s)
                .ReturnsAsync((State s) => s);

            // Act
            await _sut.SaveState();

            // Assert
            capturedState.Should().NotBeNull();
            capturedState.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.MainSampler);
            var savedSampler = capturedState.GenerationParameters.Fragments[FragmentKeys.Fragments.MainSampler];
            savedSampler.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(50);
        }

        [Fact]
        public async Task LoadState_RestoresFragments()
        {
            // Arrange
            var savedGenParams = new GenerationParameters();
            var samplerFragment = savedGenParams.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Files.Sampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 75);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, 12.0);
            
            var savedState = new State
            {
                Id = 1,
                AppState = new AppState(new AppSettings()),
                GenerationParameters = savedGenParams
            };

            _mockDb.Setup(db => db.GetState(1)).ReturnsAsync(savedState);

            // Act
            await _sut.LoadState();

            // Assert
            var loadedSampler = _sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            loadedSampler.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(75);
            loadedSampler.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(12.0);
        }

        #endregion

        #region GenerationParameters Initialization Tests

        [Fact]
        public void InitializeGenerationParameters_CreatesDefaultFragments()
        {
            // Act
            _sut.InitializeGenerationParameters();

            // Assert
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.Prompts);
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.MainSampler);
            _sut.GenerationParameters.Fragments.Should().ContainKey(FragmentKeys.Fragments.Latent);
        }

        [Fact]
        public void InitializeGenerationParameters_UsesSettingsForDefaults()
        {
            // Arrange
            var customSettings = new AppSettings
            {
                Generation = new GenerationSettingsModel
                {
                    Shared = new SharedSettingsModel
                    {
                        Steps = new StepsSettingsModel { Value = 50 },
                        CfgScale = new CfgScaleSettingsModel { Value = 10.0f },
                        Resolution = new ResolutionSettingsModel { Width = 1024, Height = 1024 },
                        Batch = new BatchSettingsModel
                        {
                            Count = new BatchCountSettingsModel { Value = 2 },
                            Size = new BatchSizeSettingsModel { Value = 8 }
                        },
                        Denoising = new DenoisingSettingsModel { Value = 0.75 }
                    }
                }
            };

            _mockSettings.Setup(s => s.Settings).Returns(customSettings);
            var sut = new StateService(_mockDb.Object, _mockConfig.Object, _mockEvents.Object, _mockSettings.Object);

            // Assert
            var samplerFragment = sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
            samplerFragment.GetValue<int>(FragmentKeys.Params.Steps).Should().Be(50);
            samplerFragment.GetValue<double>(FragmentKeys.Params.Cfg).Should().Be(10.0);
            samplerFragment.GetValue<double>(FragmentKeys.Params.Denoise).Should().Be(0.75);
            
            var latentFragment = sut.GenerationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
            latentFragment.GetValue<int>(FragmentKeys.Params.Width).Should().Be(1024);
            latentFragment.GetValue<int>(FragmentKeys.Params.Height).Should().Be(1024);
            latentFragment.GetValue<int>(FragmentKeys.Params.BatchSize).Should().Be(8);
        }

        #endregion
    }
}
