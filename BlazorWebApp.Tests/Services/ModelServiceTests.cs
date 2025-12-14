using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Tests.MockBuilders;
using BlazorWebApp.Tests.TestFixtures;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services
{
    public class ModelServiceTests
    {
        private readonly Mock<IComfyUIService> _mockComfyUI;
        private readonly Mock<IBackendService> _mockBackend;
        private readonly Mock<IStateService> _mockState;
        private readonly Mock<IEventService> _mockEvents;

        public ModelServiceTests()
        {
            _mockComfyUI = new Mock<IComfyUIService>();
            _mockBackend = new Mock<IBackendService>();
            _mockState = new Mock<IStateService>();
            _mockEvents = new Mock<IEventService>();

            // Default setup
            _mockBackend.Setup(x => x.IsBackendAvailable).Returns(true);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow>(),
                    CurrentWorkflowId = null
                }
            });
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters 
            { 
                WorkflowAssets = new Dictionary<string, string>() 
            });
            _mockState.Setup(x => x.ParametersImg2Img).Returns(new Img2ImgParameters 
            { 
                WorkflowAssets = new Dictionary<string, string>() 
            });
            _mockState.Setup(x => x.ParametersImg2Vid).Returns(new Img2VidParameters 
            { 
                WorkflowAssets = new Dictionary<string, string>() 
            });
            _mockState.Setup(x => x.ParametersUpscale).Returns(new UpscaleParameters 
            { 
                WorkflowAssets = new Dictionary<string, string>() 
            });
        }

        private ModelService CreateService()
        {
            return new ModelService(
                _mockComfyUI.Object,
                _mockBackend.Object,
                _mockState.Object,
                _mockEvents.Object,
                null!,  // ProgressService - not needed for our tests
                null!,  // IOService - not needed for our tests
                null!   // IConfiguration - not needed for our tests
            );
        }

        #region GetWorkflowModels Tests

        [Fact]
        public async Task GetWorkflowModels_WhenBackendUnavailable_ReturnsEarly()
        {
            // Arrange
            _mockBackend.Setup(x => x.IsBackendAvailable).Returns(false);
            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            _mockComfyUI.Verify(x => x.GetCheckpoints(), Times.Never);
            _mockComfyUI.Verify(x => x.GetDiffusionModels(), Times.Never);
        }

        [Fact]
        public async Task GetWorkflowModels_WhenNoWorkflowDefined_LoadsCheckpoints()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow>()
                }
            });

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            service.CheckpointModels.Should().HaveCount(3);
            service.CheckpointModels.Should().BeEquivalentTo(checkpoints);
            _mockComfyUI.Verify(x => x.GetCheckpoints(), Times.Once);
            _mockEvents.Verify(x => x.Publish(It.IsAny<ModelChangedEventArgs>()), Times.Once);
        }

        [Fact]
        public async Task GetWorkflowModels_WhenNoAssetsInWorkflow_LoadsCheckpoints()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            var workflow = ModelTestFixtures.GetWorkflowWithNoAssets();
            
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow> { workflow },
                    CurrentWorkflowId = workflow.Id
                }
            });

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            service.CheckpointModels.Should().HaveCount(3);
            _mockComfyUI.Verify(x => x.GetCheckpoints(), Times.Once);
        }

        [Fact]
        public async Task GetWorkflowModels_WithCheckpointAsset_LoadsCheckpoints()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            var workflow = ModelTestFixtures.GetSampleCheckpointWorkflow();
            
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow> { workflow },
                    CurrentWorkflowId = workflow.Id
                }
            });

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            service.CheckpointModels.Should().HaveCount(3);
            service.CheckpointModels.Should().BeEquivalentTo(checkpoints);
            _mockComfyUI.Verify(x => x.GetCheckpoints(), Times.Once);
        }

        [Fact]
        public async Task GetWorkflowModels_WithDiffusionAsset_LoadsDiffusionModels()
        {
            // Arrange
            var diffusionModels = ModelTestFixtures.GetSampleDiffusionModels();
            var workflow = ModelTestFixtures.GetSampleDiffusionWorkflow();
            
            _mockComfyUI.Setup(x => x.GetDiffusionModels()).ReturnsAsync(diffusionModels);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow> { workflow },
                    CurrentWorkflowId = workflow.Id
                }
            });

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            service.DiffusionModels.Should().HaveCount(2);
            service.DiffusionModels.Should().BeEquivalentTo(diffusionModels);
            _mockComfyUI.Verify(x => x.GetDiffusionModels(), Times.Once);
        }

        [Fact]
        public async Task GetWorkflowModels_WithMultipleAssetTypes_LoadsAll()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            var vaeModels = ModelTestFixtures.GetSampleVAEModels();
            var clipModels = ModelTestFixtures.GetSampleClipModels();
            
            var workflow = new Workflow
            {
                Id = Guid.NewGuid(),
                Assets = new List<WorkflowAsset>
                {
                    new WorkflowAsset { Type = AssetType.CheckpointModel },
                    new WorkflowAsset { Type = AssetType.Vae },
                    new WorkflowAsset { Type = AssetType.Clip }
                }
            };

            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            _mockComfyUI.Setup(x => x.GetVAEModels()).ReturnsAsync(vaeModels);
            _mockComfyUI.Setup(x => x.GetClipModels()).ReturnsAsync(clipModels);
            
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow> { workflow },
                    CurrentWorkflowId = workflow.Id
                }
            });

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            service.CheckpointModels.Should().HaveCount(3);
            service.VAEModels.Should().HaveCount(3);
            service.ClipModels.Should().HaveCount(2);
            _mockComfyUI.Verify(x => x.GetCheckpoints(), Times.Once);
            _mockComfyUI.Verify(x => x.GetVAEModels(), Times.Once);
            _mockComfyUI.Verify(x => x.GetClipModels(), Times.Once);
        }

        [Fact]
        public async Task GetWorkflowModels_PublishesModelChangedEvent()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            
            ModelChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ModelChangedEventArgs>()))
                .Callback<ModelChangedEventArgs>(e => capturedEvent = e);

            var service = CreateService();

            // Act
            await service.GetWorkflowModels();

            // Assert
            _mockEvents.Verify(x => x.Publish(It.IsAny<ModelChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
        }

        #endregion

        #region Current Model Tests

        [Fact]
        public void GetCurrentModel_WhenModelSet_ReturnsModelName()
        {
            // Arrange
            var assets = new Dictionary<string, string> { { "Model", "sd_xl_base_1.0.safetensors" } };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters { WorkflowAssets = assets });
            var service = CreateService();

            // Act
            var result = service.GetCurrentModel();

            // Assert
            result.Should().Be("sd_xl_base_1.0.safetensors");
        }

        [Fact]
        public void GetCurrentModel_WhenModelNotSet_ReturnsLoadingMessage()
        {
            // Arrange
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters { WorkflowAssets = new Dictionary<string, string>() });
            var service = CreateService();

            // Act
            var result = service.GetCurrentModel();

            // Assert
            result.Should().Be("Loading...");
        }

        [Fact]
        public void GetCurrentModel_ForImg2Vid_ReturnsHighModel()
        {
            // Arrange
            var assets = new Dictionary<string, string> { { "HighModel", "wan22_high.safetensors" } };
            _mockState.Setup(x => x.ParametersImg2Vid).Returns(new Img2VidParameters { WorkflowAssets = assets });
            var service = CreateService();

            // Act
            var result = service.GetCurrentModel(ModeType.Img2Vid);

            // Assert
            result.Should().Be("wan22_high.safetensors");
        }

        [Fact]
        public async Task SetCurrentModel_WhenBackendUnavailable_DoesNotSet()
        {
            // Arrange
            _mockBackend.Setup(x => x.IsBackendAvailable).Returns(false);
            var service = CreateService();
            var initialAssets = _mockState.Object.ParametersTxt2Img.WorkflowAssets;

            // Act
            await service.SetCurrentModel("test-model.safetensors");

            // Assert
            initialAssets.Should().NotContainKey("Model");
            _mockState.Verify(x => x.SaveState(), Times.Never);
        }

        [Fact]
        public async Task SetCurrentModel_MatchesModelTitle_SetsFullModelName()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            var workflow = ModelTestFixtures.GetSampleCheckpointWorkflow();
            
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow> { workflow },
                    CurrentWorkflowId = workflow.Id
                }
            });

            var service = CreateService();
            await service.GetWorkflowModels(); // Load models first

            // Act
            await service.SetCurrentModel("realisticVision");

            // Assert
            _mockState.Object.ParametersTxt2Img.WorkflowAssets.Should().ContainKey("Model");
            _mockState.Object.ParametersTxt2Img.WorkflowAssets["Model"].Should().Be("realisticVisionV60B1_v51VAE.safetensors");
        }

        [Fact]
        public async Task SetCurrentModel_PublishesEventAndSavesState()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.SetCurrentModel("test-model.safetensors");

            // Assert
            _mockEvents.Verify(x => x.Publish(It.IsAny<ModelChangedEventArgs>()), Times.Once);
            _mockState.Verify(x => x.SaveState(), Times.Once);
        }

        #endregion

        #region Current VAE Tests

        [Fact]
        public void GetCurrentVae_WhenVaeSet_ReturnsVaeName()
        {
            // Arrange
            var assets = new Dictionary<string, string> { { "Vae", "sdxl_vae.safetensors" } };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters { WorkflowAssets = assets });
            var service = CreateService();

            // Act
            var result = service.GetCurrentVae();

            // Assert
            result.Should().Be("sdxl_vae.safetensors");
        }

        [Fact]
        public void GetCurrentVae_WhenVaeNotSet_ReturnsNull()
        {
            // Arrange
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters { WorkflowAssets = new Dictionary<string, string>() });
            var service = CreateService();

            // Act
            var result = service.GetCurrentVae();

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetCurrentVae_SetsVaeInWorkflowAssets()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.SetCurrentVae("test-vae.safetensors");

            // Assert
            _mockState.Object.ParametersTxt2Img.WorkflowAssets.Should().ContainKey("Vae");
            _mockState.Object.ParametersTxt2Img.WorkflowAssets["Vae"].Should().Be("test-vae.safetensors");
        }

        [Fact]
        public async Task SetCurrentVae_SavesState()
        {
            // Arrange
            var service = CreateService();

            // Act
            await service.SetCurrentVae("test-vae.safetensors");

            // Assert
            _mockState.Verify(x => x.SaveState(), Times.Once);
        }

        #endregion

        #region Asset Loading Tests

        [Fact]
        public async Task GetVAEModels_WhenBackendAvailable_LoadsVAEs()
        {
            // Arrange
            var vaeModels = ModelTestFixtures.GetSampleVAEModels();
            _mockComfyUI.Setup(x => x.GetVAEModels()).ReturnsAsync(vaeModels);
            var service = CreateService();

            // Act
            await service.GetVAEModels();

            // Assert
            service.VAEModels.Should().HaveCount(3);
            service.VAEModels.Should().BeEquivalentTo(vaeModels);
            _mockComfyUI.Verify(x => x.GetVAEModels(), Times.Once);
        }

        [Fact]
        public async Task GetVAEModels_WhenBackendUnavailable_DoesNotLoad()
        {
            // Arrange
            _mockBackend.Setup(x => x.IsBackendAvailable).Returns(false);
            var service = CreateService();

            // Act
            await service.GetVAEModels();

            // Assert
            service.VAEModels.Should().BeEmpty();
            _mockComfyUI.Verify(x => x.GetVAEModels(), Times.Never);
        }

        [Fact]
        public async Task GetADetailerModels_WhenBackendAvailable_LoadsModels()
        {
            // Arrange
            var aDetailerModels = ModelTestFixtures.GetSampleADetailerModels();
            _mockComfyUI.Setup(x => x.GetBBoxDetailers()).ReturnsAsync(aDetailerModels);
            var service = CreateService();

            // Act
            await service.GetADetailerModels();

            // Assert
            service.ADetailerModels.Should().HaveCount(3);
            service.ADetailerModels.Should().BeEquivalentTo(aDetailerModels);
            _mockComfyUI.Verify(x => x.GetBBoxDetailers(), Times.Once);
        }

        [Fact]
        public async Task GetADetailerModels_WhenBackendUnavailable_DoesNotLoad()
        {
            // Arrange
            _mockBackend.Setup(x => x.IsBackendAvailable).Returns(false);
            var service = CreateService();

            // Act
            await service.GetADetailerModels();

            // Assert
            service.ADetailerModels.Should().BeEmpty();
            _mockComfyUI.Verify(x => x.GetBBoxDetailers(), Times.Never);
        }

        [Fact]
        public async Task GetAssetOptions_ReturnsCorrectOptionsForAssetType()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            var service = CreateService();
            await service.GetWorkflowModels(); // Pre-load models

            // Act
            var result = await service.GetAssetOptions(AssetType.CheckpointModel);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain("sd_xl_base_1.0.safetensors");
            result.Should().Contain("v1-5-pruned-emaonly.safetensors");
        }

        #endregion

        #region Workflow Asset Management Tests

        [Fact]
        public void GetModelsForAssetType_ReturnsCorrectModelList()
        {
            // Arrange
            var checkpoints = ModelTestFixtures.GetSampleCheckpointModels();
            var diffusion = ModelTestFixtures.GetSampleDiffusionModels();
            
            _mockComfyUI.Setup(x => x.GetCheckpoints()).ReturnsAsync(checkpoints);
            _mockComfyUI.Setup(x => x.GetDiffusionModels()).ReturnsAsync(diffusion);
            
            var service = CreateService();

            // Act
            service.GetWorkflowModels().Wait();
            var checkpointResult = service.GetModelsForAssetType(AssetType.CheckpointModel);
            var diffusionResult = service.GetModelsForAssetType(AssetType.DiffusionModel);

            // Assert
            checkpointResult.Should().HaveCount(3);
            diffusionResult.Should().BeEmpty(); // Not loaded yet
        }

        #endregion
    }
}
