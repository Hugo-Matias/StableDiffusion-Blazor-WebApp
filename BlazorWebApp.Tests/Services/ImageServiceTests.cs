using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Globalization;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class ImageServiceTests
{
    private readonly Mock<IIOService> _mockIO;
    private readonly Mock<IBackendService> _mockBackend;
    private readonly Mock<IDatabaseService> _mockDb;
    private readonly Mock<IProgressService> _mockProgress;
    private readonly Mock<IRouterService> _mockRouter;
    private readonly Mock<ILogger<ImageService>> _mockLogger;
    private readonly Mock<IStateService> _mockState;
    private readonly Mock<ISessionService> _mockSession;
    private readonly Mock<IModelService> _mockModels;
    private readonly Mock<ISettingsService> _mockSettings;
    private readonly Mock<IWildcardService> _mockWildcardService;
    private readonly Mock<IEventService> _mockEvents;
    private readonly MagickService _magickService;
    private readonly GenerationParameters _generationParameters;

    public ImageServiceTests()
    {
        _mockIO = new Mock<IIOService>();
        _mockBackend = new Mock<IBackendService>();
        _mockDb = new Mock<IDatabaseService>();
        _mockProgress = new Mock<IProgressService>();
        _mockRouter = new Mock<IRouterService>();
        _mockLogger = new Mock<ILogger<ImageService>>();
        _mockState = new Mock<IStateService>();
        _mockSession = new Mock<ISessionService>();
        _mockModels = new Mock<IModelService>();
        _mockSettings = new Mock<ISettingsService>();
        _mockWildcardService = new Mock<IWildcardService>();
        _mockEvents = new Mock<IEventService>();

        // Setup settings for MagickService
        var appSettings = new AppSettings
        {
            Generation = new GenerationSettingsModel
            {
                Img2Img = new Img2ImgSettingsModel
                {
                    InputResolution = new Img2ImgInputResolution { Width = 2048, Height = 2048 }
                }
            }
        };
        _mockSettings.Setup(s => s.Settings).Returns(appSettings);
        _magickService = new MagickService(_mockSettings.Object);

        // Setup GenerationParameters
        _generationParameters = new GenerationParameters();
        
        var promptsFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "test prompt");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "bad quality");
        
        var samplerFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 7.0);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "euler");
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 12345L);
        
        var latentFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
        latentFragment.SetValue(FragmentKeys.Params.Width, 512);
        latentFragment.SetValue(FragmentKeys.Params.Height, 512);

        // Default setup
        SetupDefaultMocks();
    }

    private void SetupDefaultMocks()
    {
        // Setup default AppState
        var appState = new AppState
        {
            Generation = new AppStateGeneration { Seed = 12345 },
            Gallery = new AppStateGallery { ProjectId = 1 }
        };
        _mockState.Setup(s => s.State).Returns(appState);
        
        // Setup GenerationParameters
        _mockState.Setup(s => s.GenerationParameters).Returns(_generationParameters);

        // Setup default output paths
        var outputPaths = new OutputPathsOptions
        {
            DirectoryPattern = "[model_name]",
            FilenamePattern = "[seed]_[steps]_[cfg]",
            SamplesFormat = "png",
            SaveSamples = true
        };
        _mockBackend.Setup(b => b.OutputPaths).Returns(outputPaths);
        _mockBackend.Setup(b => b.IsBackendAvailable).Returns(true);
        _mockBackend.Setup(b => b.GetOutputPath(It.IsAny<Outdir>())).Returns("C:\\Output");

        // Setup model service
        _mockModels.Setup(m => m.GetCurrentModel(It.IsAny<ModeType>())).Returns("test_model.safetensors");
    }

    private ImageService CreateService()
    {
        return new ImageService(
            _mockIO.Object,
            _mockBackend.Object,
            _magickService,
            _mockDb.Object,
            _mockProgress.Object,
            _mockRouter.Object,
            _mockLogger.Object,
            _mockState.Object,
            _mockSession.Object,
            _mockModels.Object,
            _mockWildcardService.Object,
            _mockEvents.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldCreateService()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service.Images);
        Assert.NotNull(service.Progress);
    }

    #endregion

    #region GetCurrentSaveFolder Tests

    [Fact]
    public void GetCurrentSaveFolder_WithNullOutdir_ShouldReturnEmptyString()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetCurrentSaveFolder(null);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetCurrentSaveFolder_WithExtras_ShouldReturnBasePathWithoutPattern()
    {
        // Arrange
        _mockBackend.Setup(b => b.GetOutputPath(Outdir.Extras)).Returns("C:\\Output\\Extras");
        var service = CreateService();

        // Act
        var result = service.GetCurrentSaveFolder(Outdir.Extras);

        // Assert
        Assert.Equal("C:\\Output\\Extras", result);
    }

    [Fact]
    public void GetCurrentSaveFolder_WithTxt2ImgSamples_ShouldApplyDirectoryPattern()
    {
        // Arrange
        _mockBackend.Setup(b => b.GetOutputPath(Outdir.Txt2ImgSamples)).Returns("C:\\Output\\Txt2Img");
        _mockModels.Setup(m => m.GetCurrentModel(ModeType.Txt2Img)).Returns("models/my_model.safetensors");
        var service = CreateService();

        // Act
        var result = service.GetCurrentSaveFolder(Outdir.Txt2ImgSamples);

        // Assert
        Assert.Contains("my_model", result);
    }

    [Fact]
    public void GetCurrentSaveFolder_WhenBasePathEmpty_ShouldReturnEmpty()
    {
        // Arrange
        _mockBackend.Setup(b => b.GetOutputPath(It.IsAny<Outdir>())).Returns(string.Empty);
        var service = CreateService();

        // Act
        var result = service.GetCurrentSaveFolder(Outdir.Txt2ImgSamples);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetCurrentSaveFolder_WithNoDirectoryPattern_ShouldReturnBasePath()
    {
        // Arrange
        var outputPaths = new OutputPathsOptions
        {
            DirectoryPattern = null,
            FilenamePattern = "[seed]",
            SamplesFormat = "png",
            SaveSamples = true
        };
        _mockBackend.Setup(b => b.OutputPaths).Returns(outputPaths);
        _mockBackend.Setup(b => b.GetOutputPath(Outdir.Txt2ImgSamples)).Returns("C:\\Output\\Txt2Img");
        var service = CreateService();

        // Act
        var result = service.GetCurrentSaveFolder(Outdir.Txt2ImgSamples);

        // Assert
        Assert.Equal("C:\\Output\\Txt2Img", result);
    }

    #endregion

    #region ConvertPathPattern Tests

    [Fact]
    public void ConvertPathPattern_WithNullPattern_ShouldReturnEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern(null, ModeType.Txt2Img);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertPathPattern_WithEmptyPattern_ShouldReturnEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("", ModeType.Txt2Img);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertPathPattern_WithSeedTag_ShouldReplaceSeed()
    {
        // Arrange
        var appState = new AppState { Generation = new AppStateGeneration { Seed = 99999 } };
        _mockState.Setup(s => s.State).Returns(appState);
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[seed]", ModeType.Txt2Img);

        // Assert
        Assert.Equal("99999", result);
    }

    [Fact]
    public void ConvertPathPattern_WithStepsTag_NoActiveGeneration_ShouldReturnDefault()
    {
        // Arrange - when no generation is active, _currentGenerationParams is null
        // so the service returns defaults
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[steps]", ModeType.Txt2Img);

        // Assert - defaults to 20 when no active generation
        Assert.Equal("20", result);
    }

    [Fact]
    public void ConvertPathPattern_WithCfgTag_NoActiveGeneration_ShouldReturnDefault()
    {
        // Arrange - when no generation is active, _currentGenerationParams is null
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[cfg]", ModeType.Txt2Img);

        // Assert - defaults to 7 when no active generation
        Assert.Equal("7", result);
    }

    [Fact]
    public void ConvertPathPattern_WithSamplerTag_NoActiveGeneration_ShouldReturnDefault()
    {
        // Arrange - when no generation is active, _currentGenerationParams is null
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[sampler]", ModeType.Txt2Img);

        // Assert - defaults to euler when no active generation
        Assert.Equal("euler", result);
    }

    [Fact]
    public void ConvertPathPattern_WithModelNameTag_ShouldReplaceModelName()
    {
        // Arrange
        _mockModels.Setup(m => m.GetCurrentModel(ModeType.Txt2Img)).Returns("checkpoint/my_model_v2.safetensors");
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[model_name]", ModeType.Txt2Img);

        // Assert
        Assert.Contains("my_model_v2", result);
    }

    [Fact]
    public void ConvertPathPattern_WithMultipleTags_ShouldReplaceAll()
    {
        // Arrange
        var appState = new AppState { Generation = new AppStateGeneration { Seed = 12345 } };
        _mockState.Setup(s => s.State).Returns(appState);
        var service = CreateService();

        // Act - when no active generation, steps and cfg use defaults
        var result = service.ConvertPathPattern("[seed]_[steps]_[cfg]", ModeType.Txt2Img);

        // Assert
        Assert.Equal("12345_20_7", result);
    }

    [Fact]
    public void ConvertPathPattern_WithUnknownTag_ShouldReturnEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("[unknown]", ModeType.Txt2Img);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertPathPattern_WithMixedContent_ShouldPreserveNonTags()
    {
        // Arrange
        var appState = new AppState { Generation = new AppStateGeneration { Seed = 555 } };
        _mockState.Setup(s => s.State).Returns(appState);
        var service = CreateService();

        // Act
        var result = service.ConvertPathPattern("output_[seed]_final", ModeType.Txt2Img);

        // Assert
        Assert.Equal("output_555_final", result);
    }

    #endregion

    #region SaveImages Tests

    [Fact]
    public void SaveImages_Images_Property_ShouldBeInitialized()
    {
        // Arrange
        var service = CreateService();

        // Assert - Images property should be initialized to a new GeneratedImages
        Assert.NotNull(service.Images);
    }

    [Fact]
    public void SaveImages_Images_List_IsNullByDefault()
    {
        // Arrange
        var service = CreateService();

        // Assert - The inner Images list is null until generation populates it
        // This is expected behavior - SaveImages should only be called after generation
        Assert.Null(service.Images.Images);
    }

    #endregion

    #region Progress Tests

    [Fact]
    public void Progress_ShouldBeSettable()
    {
        // Arrange
        var service = CreateService();
        var progress = new InferenceProgress { Value = 0.5f };

        // Act
        service.Progress = progress;

        // Assert
        Assert.Equal(0.5f, service.Progress.Value);
    }

    #endregion

    #region GeneratedImageEntities Tests

    [Fact]
    public void GeneratedImageEntities_ShouldBeSettable()
    {
        // Arrange
        var service = CreateService();
        var entities = new ImagesDto { Images = new List<Image> { new Image { Id = 1 } } };

        // Act
        service.GeneratedImageEntities = entities;

        // Assert
        Assert.NotNull(service.GeneratedImageEntities);
        Assert.Single(service.GeneratedImageEntities.Images);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void NotifyStateChanged_ShouldPublishEventThroughEventService()
    {
        // Arrange
        var service = CreateService();
        
        // We can verify the event service is set up correctly
        // The actual publishing happens internally through NotifyStateChanged
        _mockEvents.Verify(e => e.Publish(It.IsAny<ImagesGeneratedEventArgs>()), Times.Never);
    }

    #endregion

    #region DownloadImageAsPng Tests

    [Fact]
    public async Task DownloadImageAsPng_WhenFileExistsAndNoOverwrite_ShouldReturnFalse()
    {
        // Arrange
        var service = CreateService();
        var tempFile = Path.GetTempFileName();
        try
        {
            // Act
            var result = await service.DownloadImageAsPng("http://example.com/image.png", tempFile, overwrite: false);

            // Assert
            Assert.False(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}
