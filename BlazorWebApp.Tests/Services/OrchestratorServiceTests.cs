using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class OrchestratorServiceTests
{
    private readonly Mock<IDatabaseService> _mockDb;
    private readonly Mock<IIOService> _mockIo;
    private readonly Mock<IProgressService> _mockProgress;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IComfyUIService> _mockComfyApi;
    private readonly Mock<IWorkflowService> _mockWorkflow;
    private readonly Mock<IStateService> _mockState;
    private readonly Mock<IEventService> _mockEvents;
    private readonly Mock<ISettingsService> _mockSettings;
    private readonly Mock<IBackendService> _mockBackend;
    private readonly Mock<IModelService> _mockModels;
    private readonly Mock<IGalleryService> _mockGallery;
    private readonly Mock<ISessionService> _mockSession;
    private readonly OrchestratorService _service;
    private readonly GenerationParameters _generationParameters;

    public OrchestratorServiceTests()
    {
        _mockDb = new Mock<IDatabaseService>();
        _mockIo = new Mock<IIOService>();
        _mockProgress = new Mock<IProgressService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockComfyApi = new Mock<IComfyUIService>();
        _mockWorkflow = new Mock<IWorkflowService>();
        _mockState = new Mock<IStateService>();
        _mockEvents = new Mock<IEventService>();
        _mockSettings = new Mock<ISettingsService>();
        _mockBackend = new Mock<IBackendService>();
        _mockModels = new Mock<IModelService>();
        _mockGallery = new Mock<IGalleryService>();
        _mockSession = new Mock<ISessionService>();

        // Setup default state - AppState is the actual state class in IStateService.State
        var appState = new AppState
        {
            Generation = new AppStateGeneration { Workflows = new List<Workflow>() },
            Gallery = new AppStateGallery { PageSize = 20, DateRange = new MudBlazor.DateRange(DateTime.Now.AddDays(-5), DateTime.Now) }
        };
        _mockState.Setup(s => s.State).Returns(appState);
        
        // Setup GenerationParameters
        _generationParameters = new GenerationParameters();
        _mockState.Setup(s => s.GenerationParameters).Returns(_generationParameters);

        _service = new OrchestratorService(
            _mockDb.Object,
            _mockIo.Object,
            _mockProgress.Object,
            _mockConfiguration.Object,
            _mockComfyApi.Object,
            _mockWorkflow.Object,
            _mockState.Object,
            _mockEvents.Object,
            _mockSettings.Object,
            _mockBackend.Object,
            _mockModels.Object,
            _mockGallery.Object,
            _mockSession.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldSetDatabasePageSize()
    {
        // Assert
        _mockDb.VerifySet(db => db.PageSize = 20, Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeDateRange()
    {
        // Assert
        Assert.NotNull(_mockState.Object.State.Gallery.DateRange);
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public void InvokeParametersChanged_Txt2Img_ShouldPublishEvent()
    {
        // Act
        _service.InvokeParametersChanged(isImg2Img: false);

        // Assert
        _mockEvents.Verify(e => e.Publish(It.IsAny<ParametersChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public void InvokeParametersChanged_Img2Img_ShouldPublishEvent()
    {
        // Act
        _service.InvokeParametersChanged(isImg2Img: true);

        // Assert
        _mockEvents.Verify(e => e.Publish(It.IsAny<ParametersChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public void InvokeSessionVideosChanged_ShouldPublishEvent()
    {
        // Act
        _service.InvokeSessionVideosChanged();

        // Assert
        _mockEvents.Verify(e => e.Publish(It.IsAny<SessionVideosChangedEventArgs>()), Times.Once);
    }

    #endregion

    #region Model Management Tests

    [Fact]
    public async Task GetWorkflowModels_ShouldDelegateToModelService()
    {
        // Act
        await _service.GetWorkflowModels();

        // Assert
        _mockModels.Verify(m => m.GetWorkflowModels(false), Times.Once);
    }

    [Fact]
    public async Task GetWorkflowModels_WithRefresh_ShouldPassRefreshFlag()
    {
        // Act
        await _service.GetWorkflowModels(refresh: true);

        // Assert
        _mockModels.Verify(m => m.GetWorkflowModels(true), Times.Once);
    }

    [Fact]
    public async Task GetSDVAEs_ShouldDelegateToModelService()
    {
        // Act
        await _service.GetSDVAEs();

        // Assert
        _mockModels.Verify(m => m.GetVAEModels(), Times.Once);
    }

    [Fact]
    public async Task GetSDADetailerModels_ShouldDelegateToModelService()
    {
        // Act
        await _service.GetSDADetailerModels();

        // Assert
        _mockModels.Verify(m => m.GetADetailerModels(), Times.Once);
    }

    [Fact]
    public void GetModelsForAssetType_ShouldDelegateToModelService()
    {
        // Arrange
        var expectedModels = new List<SDModel> { new SDModel { Title = "Test" } };
        _mockModels.Setup(m => m.GetModelsForAssetType(AssetType.CheckpointModel)).Returns(expectedModels);

        // Act
        var result = _service.GetModelsForAssetType(AssetType.CheckpointModel);

        // Assert
        Assert.Same(expectedModels, result);
    }

    [Fact]
    public void GetCurrentModel_ShouldDelegateToModelService()
    {
        // Arrange
        _mockModels.Setup(m => m.GetCurrentModel(null)).Returns("test_model.safetensors");

        // Act
        var result = _service.GetCurrentModel();

        // Assert
        Assert.Equal("test_model.safetensors", result);
    }

    [Fact]
    public async Task SetCurrentModel_ShouldDelegateToModelServiceAndSaveState()
    {
        // Act
        await _service.SetCurrentModel("new_model.safetensors");

        // Assert
        _mockModels.Verify(m => m.SetCurrentModel("new_model.safetensors", null), Times.Once);
        _mockState.Verify(s => s.SaveState(), Times.Once);
    }

    #endregion

    #region Workflow Management Tests

    [Fact]
    public void GetCurrentWorkflow_WithNoWorkflows_ShouldReturnNull()
    {
        // Arrange
        _mockState.Object.State.Generation.Workflows = new List<Workflow>();

        // Act
        var result = _service.GetCurrentWorkflow();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetCurrentWorkflow_WithCurrentWorkflowId_ShouldReturnMatchingWorkflow()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new Workflow { Id = workflowId, Title = "Test Workflow" };
        _mockState.Object.State.Generation.Workflows = new List<Workflow> { workflow };
        _mockState.Object.State.Generation.CurrentWorkflowId = workflowId;

        // Act
        var result = _service.GetCurrentWorkflow();

        // Assert
        Assert.Same(workflow, result);
    }

    [Fact]
    public void GetCurrentWorkflow_WithWorkflowBase_ShouldReturnMatchingWorkflow()
    {
        // Arrange
        var workflow = new Workflow { Id = Guid.NewGuid(), Title = "Test Workflow", Base = ModelBase.Flux };
        _mockState.Object.State.Generation.Workflows = new List<Workflow> { workflow };
        _mockState.Object.State.Generation.WorkflowBase = ModelBase.Flux;

        // Act
        var result = _service.GetCurrentWorkflow();

        // Assert
        Assert.Same(workflow, result);
    }

    [Fact]
    public void GetWorkflowById_ShouldReturnMatchingWorkflow()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new Workflow { Id = workflowId, Title = "Test Workflow" };
        _mockState.Object.State.Generation.Workflows = new List<Workflow> { workflow };

        // Act
        var result = _service.GetWorkflowById(workflowId);

        // Assert
        Assert.Same(workflow, result);
    }

    [Fact]
    public void GetWorkflowsForMode_ShouldReturnFilteredWorkflows()
    {
        // Arrange
        var txt2imgWorkflow = new Workflow { Id = Guid.NewGuid(), Title = "Txt2Img Workflow", Mode = ModeType.Txt2Img };
        var img2imgWorkflow = new Workflow { Id = Guid.NewGuid(), Title = "Img2Img Workflow", Mode = ModeType.Img2Img };
        _mockState.Object.State.Generation.Workflows = new List<Workflow> { txt2imgWorkflow, img2imgWorkflow };

        // Act
        var result = _service.GetWorkflowsForMode(ModeType.Txt2Img);

        // Assert
        Assert.Single(result);
        Assert.Same(txt2imgWorkflow, result.First());
    }

    [Fact]
    public void GetComfyWorkflows_ShouldLoadWorkflowsFromService()
    {
        // Arrange
        var workflows = new List<Workflow> { new Workflow { Id = Guid.NewGuid(), Title = "Test" } };
        _mockWorkflow.Setup(w => w.GetWorkflows()).Returns(workflows);

        // Act
        _service.GetComfyWorkflows();

        // Assert
        Assert.Same(workflows, _mockState.Object.State.Generation.Workflows);
    }

    [Fact]
    public void SetCurrentWorkflow_ShouldUpdateStateAndPublishEvents()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new Workflow { Id = workflowId, Title = "Test Workflow", Base = ModelBase.StableDiffusion };
        _mockState.Object.State.Generation.Workflows = new List<Workflow> { workflow };

        // Act
        _service.SetCurrentWorkflow(workflowId);

        // Assert
        Assert.Equal(workflowId, _mockState.Object.State.Generation.CurrentWorkflowId);
        Assert.Equal(ModelBase.StableDiffusion, _mockState.Object.State.Generation.WorkflowBase);
        _mockEvents.Verify(e => e.Publish(It.IsAny<StateChangedEventArgs>()), Times.Once);
        _mockEvents.Verify(e => e.Publish(It.IsAny<WorkflowChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public void ResetCurrentWorkflow_ShouldClearWorkflowState()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        _mockState.Object.State.Generation.CurrentWorkflowId = workflowId;
        _mockState.Object.State.Generation.WorkflowBase = ModelBase.Flux;

        // Act
        _service.ResetCurrentWorkflow();

        // Assert
        Assert.Null(_mockState.Object.State.Generation.CurrentWorkflowId);
        Assert.Equal(default, _mockState.Object.State.Generation.WorkflowBase);
        _mockEvents.Verify(e => e.Publish(It.IsAny<WorkflowChangedEventArgs>()), Times.Once);
    }

    #endregion

    #region Workflow Assets Tests

    [Fact]
    public void GetWorkflowAsset_ShouldReturnAssetValue()
    {
        // Arrange
        _generationParameters.Assets["Model"] = "test_model.safetensors";

        // Act
        var result = _service.GetWorkflowAsset("Model");

        // Assert
        Assert.Equal("test_model.safetensors", result);
    }

    [Fact]
    public void GetWorkflowAsset_WithNonExistentParameter_ShouldReturnNull()
    {
        // Act
        var result = _service.GetWorkflowAsset("NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetWorkflowAsset_ShouldUpdateAssetValue()
    {
        // Act
        _service.SetWorkflowAsset("Model", "new_model.safetensors");

        // Assert
        Assert.Equal("new_model.safetensors", _generationParameters.Assets["Model"]);
    }

    [Fact]
    public void GetWorkflowAssetsForMode_ShouldReturnGenerationParametersAssets()
    {
        // Arrange
        _generationParameters.Assets["Model"] = "test.safetensors";

        // Act
        var result = _service.GetWorkflowAssetsForMode(ModeType.Txt2Img);

        // Assert
        Assert.Same(_generationParameters.Assets, result);
    }

    #endregion

    #region Gallery Tests

    [Fact]
    public async Task GetFolders_ShouldDelegateToGalleryService()
    {
        // Act
        await _service.GetFolders();

        // Assert
        _mockGallery.Verify(g => g.GetFolders(), Times.Once);
    }

    [Fact]
    public void ReplaceSelectedImages_ShouldDelegateToGalleryService()
    {
        // Arrange
        var ids = new List<int> { 1, 2, 3 };

        // Act
        _service.ReplaceSelectedImages(ids);

        // Assert
        _mockGallery.Verify(g => g.ReplaceSelectedImages(ids), Times.Once);
    }

    [Fact]
    public void AddSelectedImage_ShouldDelegateToGalleryService()
    {
        // Act
        _service.AddSelectedImage(42);

        // Assert
        _mockGallery.Verify(g => g.AddSelectedImage(42), Times.Once);
    }

    [Fact]
    public void RemoveSelectedImage_ShouldDelegateToGalleryService()
    {
        // Act
        _service.RemoveSelectedImage(42);

        // Assert
        _mockGallery.Verify(g => g.RemoveSelectedImage(42), Times.Once);
    }

    [Fact]
    public void ClearSelectedImages_ShouldDelegateToGalleryService()
    {
        // Act
        _service.ClearSelectedImages();

        // Assert
        _mockGallery.Verify(g => g.ClearSelectedImages(), Times.Once);
    }

    #endregion

    #region Session Tests

    [Fact]
    public void ResetImageEditorState_ShouldDelegateToSessionService()
    {
        // Act
        _service.ResetImageEditorState();

        // Assert
        _mockSession.Verify(s => s.ResetImageEditorState(), Times.Once);
    }

    [Fact]
    public void SetImg2ImgInputImage_ShouldDelegateToSessionService()
    {
        // Act
        _service.SetImg2ImgInputImage("base64data", resetEditorState: true);

        // Assert
        _mockSession.Verify(s => s.SetImg2ImgInputImage("base64data", true), Times.Once);
    }

    [Fact]
    public void AddSessionVideo_ShouldDelegateToSessionService()
    {
        // Arrange
        var video = new GeneratedVideo { FilePath = "/path/to/video.mp4" };

        // Act
        _service.AddSessionVideo(video);

        // Assert
        _mockSession.Verify(s => s.AddSessionVideo(video), Times.Once);
    }

    [Fact]
    public void ClearSessionVideos_ShouldDelegateToSessionService()
    {
        // Act
        _service.ClearSessionVideos();

        // Assert
        _mockSession.Verify(s => s.ClearSessionVideos(), Times.Once);
    }

    #endregion

    #region Settings Tests

    [Fact]
    public void LoadSettings_ShouldDelegateToSettingsService()
    {
        // Act
        _service.LoadSettings();

        // Assert
        _mockSettings.Verify(s => s.LoadSettings(), Times.Once);
    }

    [Fact]
    public void SaveSettings_ShouldDelegateToSettingsService()
    {
        // Act
        _service.SaveSettings();

        // Assert
        _mockSettings.Verify(s => s.SaveSettings(), Times.Once);
    }

    #endregion

    #region State Tests

    [Fact]
    public async Task LoadState_ShouldDelegateToStateService()
    {
        // Arrange
        _mockWorkflow.Setup(w => w.RefreshWorkflows(It.IsAny<ModelBase?>(), It.IsAny<Guid?>()))
            .Returns((new List<Workflow>(), null, null));

        // Act
        await _service.LoadState();

        // Assert
        _mockState.Verify(s => s.LoadState(), Times.Once);
    }

    [Fact]
    public async Task SaveState_ShouldDelegateToStateService()
    {
        // Act
        await _service.SaveState();

        // Assert
        _mockState.Verify(s => s.SaveState(), Times.Once);
    }

    #endregion

    #region Styles & Prompts Tests

    [Fact]
    public void SetLoras_WithNullLoras_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        _service.SetLoras(null!, isImg2Img: false);
    }

    [Fact]
    public void SetLoras_ShouldAddLorasToGenerationParameters()
    {
        // Arrange
        var loras = new List<Lora> { new Lora { Name = "test_lora" } };

        // Act
        _service.SetLoras(loras, isImg2Img: false);

        // Assert
        Assert.Contains(_generationParameters.Loras, l => l.Name == "test_lora");
    }

    [Fact]
    public void ParseAndCleanCopiedPrompt_WithEmptyPrompt_ShouldReturnEmpty()
    {
        // Act
        var result = _service.ParseAndCleanCopiedPrompt("", isNegative: false, isImg2Img: false);

        // Assert
        Assert.Empty(result);
    }

    #endregion
}
