using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using BlazorWebApp.Workflows.Templates.Flux;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class CharacterReferenceRunServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "character-run-service-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<IComfyUIService> _comfyUI = new();
    private readonly Mock<IBackendService> _backend = new();
    private readonly Mock<IDatabaseService> _database = new();
    private readonly Mock<IStateService> _state = new();
    private readonly Mock<IIOService> _io = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly string _saveDirectory;
    private int _nextImageId = 10;

    public CharacterReferenceRunServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _saveDirectory = Path.Combine(_tempDirectory, "saved");
        _backend.SetupGet(backend => backend.IsBackendAvailable).Returns(true);
        _backend.SetupGet(backend => backend.ComfyWSClientId).Returns("test-client");
        _backend.SetupGet(backend => backend.OutputPaths).Returns(new OutputPathsOptions
        {
            DirectoryPattern = string.Empty,
            FilenamePattern = "[seed]_[steps]_[cfg]",
            SamplesFormat = "png"
        });
        _backend.Setup(backend => backend.GetOutputPath(Outdir.Img2ImgSamples)).Returns(_saveDirectory);
        _database.Setup(database => database.GetSamplerIdByName(It.IsAny<string>())).ReturnsAsync(1);
        _database.Setup(database => database.GetMode(ModeType.Img2Img)).ReturnsAsync(2);
        _database.Setup(database => database.GetResourceByFilename(It.IsAny<string>())).ReturnsAsync(new Resource());
        _database.Setup(database => database.AddImage(It.IsAny<Image>()))
            .ReturnsAsync((Image image) =>
            {
                image.Id = _nextImageId++;
                return image;
            });
        _io.Setup(io => io.CreateDirectory(It.IsAny<string>()))
            .Returns((string path) => Directory.CreateDirectory(path));
        _io.Setup(io => io.SaveFileToDisk(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns((string path, byte[] data) =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, data);
                return Task.CompletedTask;
            });
        _settings.Setup(settings => settings.Settings).Returns(new AppSettings
        {
            Generation = new GenerationSettingsModel
            {
                Img2Img = new Img2ImgSettingsModel
                {
                    InputResolution = new Img2ImgInputResolution { Width = 2048, Height = 2048 }
                }
            }
        });
    }

    [Fact]
    public async Task RunAsync_ShouldRunRequestedSlotWithDependencyAndPersistOutputs()
    {
        var sourceState = CreateState();
        _state.SetupGet(state => state.State).Returns(sourceState);
        var outputPath = CreatePng("front.png");
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                "test-client",
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow _, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
                new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["left-profile"]));

        result.Success.Should().BeTrue();
        result.Slots.Select(slot => slot.SlotId).Should().Equal("front-view", "left-profile");
        sourceState.Character.Slots.First(slot => slot.Id == "front-view").Status.Should().Be(CharacterReferenceSlotStatus.Succeeded);
        sourceState.Character.Slots.First(slot => slot.Id == "left-profile").LastOutputImageId.Should().Be(11);
        _database.Verify(database => database.AddImage(It.Is<Image>(image => image.ProjectId == 7 && IsSavedImagePath(image.Path))), Times.Exactly(2));
        _state.Verify(state => state.SaveState(), Times.AtLeast(2));
    }

    [Fact]
    public async Task RunAsync_WhenDependencyOutputExists_ShouldUploadAndUseItWithoutRegeneratingDependency()
    {
        var sourceState = CreateState();
        var frontOutputPath = CreatePng("existing-front.png");
        sourceState.Character.Slots.First(slot => slot.Id == "front-view").LastOutputPath = frontOutputPath;
        _state.SetupGet(state => state.State).Returns(sourceState);
        _io.Setup(io => io.GetBase64FromFileAsync(frontOutputPath))
            .ReturnsAsync(Convert.ToBase64String(await File.ReadAllBytesAsync(frontOutputPath)));
        _comfyUI.Setup(comfy => comfy.UploadImageAsync(It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync("uploaded-front.png");

        var outputPath = CreatePng("left.png");
        string? workflowJson = null;
        IReadOnlyCollection<string>? expectedNodeIds = null;
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                "test-client",
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow workflow, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
            {
                workflowJson = workflow.Json;
                expectedNodeIds = nodeIds;
                return new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                };
            });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["left-profile"]));

        result.Success.Should().BeTrue();
        result.Slots.Should().ContainSingle().Which.SlotId.Should().Be("left-profile");
        expectedNodeIds.Should().ContainSingle().Which.Should().Contain("left_profile_save");
        workflowJson.Should().Contain("uploaded-front.png");
        workflowJson.Should().Contain("character_dependency_front_view_image");
        workflowJson.Should().NotContain("character_slot_front_view_save");
        sourceState.Character.Slots.First(slot => slot.Id == "front-view").LastOutputImageId.Should().BeNull();
        sourceState.Character.Slots.First(slot => slot.Id == "left-profile").LastOutputImageId.Should().Be(10);
        _database.Verify(database => database.AddImage(It.Is<Image>(image => IsSavedImagePath(image.Path))), Times.Once);
        _comfyUI.Verify(comfy => comfy.UploadImageAsync(It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenOutputIsMissing_ShouldMarkSlotFailed()
    {
        var sourceState = CreateState();
        _state.SetupGet(state => state.State).Returns(sourceState);
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow _, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
                new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(nodeId => nodeId, _ => new List<ComfyImageOutputFile>())
                });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["neutral"]));

        result.Success.Should().BeFalse();
        result.Slots.Should().ContainSingle()
            .Which.Status.Should().Be(CharacterReferenceSlotStatus.Failed);
        sourceState.Character.Slots.First(slot => slot.Id == "neutral").Status.Should().Be(CharacterReferenceSlotStatus.Failed);
        _database.Verify(database => database.AddImage(It.IsAny<Image>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldPersistCustomSlotOutputsWithSameCollector()
    {
        var sourceState = CreateState();
        var customSlot = sourceState.Character.AddSlot(CharacterReferenceSlotPresetKey.Blank, "Extra Pose");
        _state.SetupGet(state => state.State).Returns(sourceState);
        var outputPath = CreatePng("custom.png");
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow _, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
                new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest([customSlot.Id]));

        result.Success.Should().BeTrue();
        result.Slots.Should().ContainSingle()
            .Which.SlotId.Should().Be(customSlot.Id);
        customSlot.LastOutputImageId.Should().Be(10);
        customSlot.LastOutputPath.Should().NotBe(outputPath);
        customSlot.LastOutputPath.Should().NotBeNull();
        customSlot.LastOutputPath.Should().StartWith(_saveDirectory);
        File.Exists(customSlot.LastOutputPath!).Should().BeTrue();
        _database.Verify(database => database.AddImage(It.Is<Image>(image => IsSavedImagePath(image.Path))), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenSourceDataIsAvailable_ShouldUploadFreshSourceForWorkflow()
    {
        var sourceState = CreateState();
        sourceState.Character.SourceImage.ImagePath = "stale-comfy-input.png";
        sourceState.Character.SourceImage.ImageDataUri = "data:image/png;base64,iVBORw0KGgo=";
        _state.SetupGet(state => state.State).Returns(sourceState);
        _comfyUI.Setup(comfy => comfy.UploadImageAsync(sourceState.Character.SourceImage.ImageDataUri, It.IsAny<Guid?>()))
            .ReturnsAsync("fresh-comfy-input.png");

        var outputPath = CreatePng("fresh-source.png");
        string? workflowJson = null;
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow workflow, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
            {
                workflowJson = workflow.Json;
                return new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                };
            });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["front-view"]));

        result.Success.Should().BeTrue();
        workflowJson.Should().Contain("fresh-comfy-input.png");
        workflowJson.Should().NotContain("stale-comfy-input.png");
        sourceState.Character.SourceImage.ImagePath.Should().BeNull();
        _comfyUI.Verify(comfy => comfy.UploadImageAsync(sourceState.Character.SourceImage.ImageDataUri, It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithQwenFaceReplacementEnabled_ShouldSubmitFaceReplacementNodes()
    {
        var sourceState = CreateState();
        sourceState.Character.FaceReplacement.Enabled = true;
        sourceState.Character.FaceReplacement.CropResolution = 640;
        sourceState.Character.FaceReplacement.Denoise = 0.45;
        _state.SetupGet(state => state.State).Returns(sourceState);

        var outputPath = CreatePng("face-replacement.png");
        string? workflowJson = null;
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow workflow, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
            {
                workflowJson = workflow.Json;
                return new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                };
            });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["front-view"]));

        result.Success.Should().BeTrue();
        workflowJson.Should().Contain("character_slot_front_view_face_target_mask");
        workflowJson.Should().Contain("character_slot_front_view_face_source_crop");
        workflowJson.Should().Contain("character_slot_front_view_face_sampler");
        workflowJson.Should().Contain("character_slot_front_view_face_uncrop");
        workflowJson.Should().Contain("BatchCLIPSeg");
        workflowJson.Should().Contain("BatchUncropAdvanced");
        workflowJson.Should().Contain("\"denoise\":0.45");
    }

    [Fact]
    public async Task RunAsync_WhenFluxEngineSelected_ShouldBuildFluxWorkflowAndPersistFluxModel()
    {
        var sourceState = CreateState();
        sourceState.Character.Engine = CharacterReferenceEngine.Flux2Klein;
        _state.SetupGet(state => state.State).Returns(sourceState);

        var outputPath = CreatePng("flux-front.png");
        string? workflowJson = null;
        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync((BlazorWebApp.Workflows.Models.ComfyWorkflow workflow, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
            {
                workflowJson = workflow.Json;
                return new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                };
            });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["front-view"]));

        result.Success.Should().BeTrue();
        workflowJson.Should().Contain("EmptyFlux2LatentImage");
        workflowJson.Should().Contain("Flux2Scheduler");
        workflowJson.Should().Contain("ReferenceLatent");
        workflowJson.Should().NotContain("TextEncodeQwenImageEditPlusPro_lrzjason");
        _database.Verify(database => database.GetResourceByFilename(CharacterReferenceDefaults.Flux2KleinDiffusionModel), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenRtxUpscaleFails_ShouldRetryWithoutRtxNode()
    {
        var sourceState = CreateState();
        sourceState.Character.UseRtxUpscale = true;
        _state.SetupGet(state => state.State).Returns(sourceState);
        var outputPath = CreatePng("rtx-fallback.png");
        var workflowJsons = new List<string>();
        var callCount = 0;

        _comfyUI.Setup(comfy => comfy.PostWorkflowForImageOutputsAsync(
                It.IsAny<BlazorWebApp.Workflows.Models.ComfyWorkflow>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<Guid?>()))
            .Returns((BlazorWebApp.Workflows.Models.ComfyWorkflow workflow, string _, IReadOnlyCollection<string> nodeIds, Guid? _) =>
            {
                workflowJsons.Add(workflow.Json);
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("NvVFX_Load failed: The effect has not been properly initialized (code -12)");
                }

                return Task.FromResult(new ComfyImageOutputResponse
                {
                    PromptId = Guid.NewGuid().ToString(),
                    ImagesByNodeId = nodeIds.ToDictionary(
                        nodeId => nodeId,
                        _ => new List<ComfyImageOutputFile> { new() { FullPath = outputPath } })
                });
            });

        var result = await CreateService().RunAsync(new CharacterReferenceRunRequest(["front-view"]));

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().Contain("RTX upscale failed");
        sourceState.Character.UseRtxUpscale.Should().BeFalse();
        workflowJsons.Should().HaveCount(2);
        workflowJsons[0].Should().Contain("RTXVideoSuperResolution");
        workflowJsons[1].Should().NotContain("RTXVideoSuperResolution");
        sourceState.Character.Slots.First(slot => slot.Id == "front-view").Status.Should().Be(CharacterReferenceSlotStatus.Succeeded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private CharacterReferenceRunService CreateService()
    {
        return new CharacterReferenceRunService(
            _comfyUI.Object,
            _backend.Object,
            _database.Object,
            _state.Object,
            _io.Object,
            new MagickService(_settings.Object),
            [
                new QwenCharacterReferenceWorkflowComposer(),
                new Flux2KleinCharacterReferenceWorkflowComposer()
            ],
            Mock.Of<ILogger<CharacterReferenceRunService>>());
    }

    private static AppState CreateState()
    {
        return new AppState
        {
            Gallery = new AppStateGallery { ProjectId = 7 },
            Character = new AppStateCharacter
            {
                SourceImage = new CharacterSourceImageState { ImagePath = "source.png" }
            }
        };
    }

    private string CreatePng(string filename)
    {
        var path = Path.Combine(_tempDirectory, filename);
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private bool IsSavedImagePath(string path)
    {
        return path.StartsWith(_saveDirectory, StringComparison.OrdinalIgnoreCase)
            && Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
            && File.Exists(path);
    }
}