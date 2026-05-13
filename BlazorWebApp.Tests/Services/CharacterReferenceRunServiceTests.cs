using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
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
    private readonly Mock<ISettingsService> _settings = new();
    private int _nextImageId = 10;

    public CharacterReferenceRunServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _backend.SetupGet(backend => backend.IsBackendAvailable).Returns(true);
        _backend.SetupGet(backend => backend.ComfyWSClientId).Returns("test-client");
        _database.Setup(database => database.GetSamplerIdByName(It.IsAny<string>())).ReturnsAsync(1);
        _database.Setup(database => database.GetMode(ModeType.Img2Img)).ReturnsAsync(2);
        _database.Setup(database => database.GetResourceByFilename(It.IsAny<string>())).ReturnsAsync(new Resource());
        _database.Setup(database => database.AddImage(It.IsAny<Image>()))
            .ReturnsAsync((Image image) =>
            {
                image.Id = _nextImageId++;
                return image;
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
        _database.Verify(database => database.AddImage(It.Is<Image>(image => image.ProjectId == 7 && image.Path == outputPath)), Times.Exactly(2));
        _state.Verify(state => state.SaveState(), Times.AtLeast(2));
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
        customSlot.LastOutputPath.Should().Be(outputPath);
        _database.Verify(database => database.AddImage(It.Is<Image>(image => image.Path == outputPath)), Times.Once);
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
            new MagickService(_settings.Object),
            new QwenCharacterReferenceWorkflowComposer(),
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
}