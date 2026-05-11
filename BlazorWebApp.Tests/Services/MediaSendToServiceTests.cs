using BlazorWebApp.Models;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

/// <summary>
/// Unit tests for MediaSendToService source and parameter routing helpers.
/// </summary>
public class MediaSendToServiceTests
{
    private readonly MediaSendToService _service;

    public MediaSendToServiceTests()
    {
        _service = new MediaSendToService(
            Mock.Of<IBackendService>(),
            Mock.Of<IStateService>(),
            Mock.Of<ISessionService>(),
            Mock.Of<IIOService>(),
            Mock.Of<IOrchestratorService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());
    }

    [Theory]
    [InlineData("/image/foo.png", true)]
    [InlineData("/files/danbooru/general/score_0/123.jpg", true)]
    [InlineData("https://cdn.donmai.us/sample.jpg", false)]
    [InlineData("http://example.com/image.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("C:\\local\\image.png", true)]
    public void IsLocal_ReturnsExpectedValue(string? path, bool expected)
    {
        _service.IsLocal(path).Should().Be(expected);
    }

    [Fact]
    public void GetSourceTargets_ForImages_ExcludesDisabledWorkflowsForCurrentBase()
    {
        var enabledImg2Img = CreateWorkflow("Enabled Img2Img", ModelBase.Flux, ModeType.Img2Img, "source_image", "Source Image", "image");
        var disabledImg2Vid = CreateWorkflow("Disabled Img2Vid", ModelBase.Flux, ModeType.Img2Vid, "start_image", "Start Image", "image");
        var otherBase = CreateWorkflow("Other Base", ModelBase.Wan, ModeType.Img2Img, "source_image", "Source Image", "image");
        var videoOnly = CreateWorkflow("Video Only", ModelBase.Flux, ModeType.Vid2Vid, "source_video", "Source Video", "video");

        var service = CreateServiceWithState(new[] { enabledImg2Img, disabledImg2Vid, otherBase, videoOnly }, disabledImg2Vid.Id);

        var target = service.GetSourceTargets(SendToMediaType.Image).Should().ContainSingle().Subject;
        target.Workflow.Id.Should().Be(enabledImg2Img.Id);
        target.SourceKey.Should().Be("source_image");
        target.SourceLabel.Should().Be("Source Image");
    }

    [Fact]
    public void GetSourceTargets_ForVideos_ReturnsMatchingVideoSourcesOnly()
    {
        var imageWorkflow = CreateWorkflow("Image Workflow", ModelBase.Flux, ModeType.Img2Img, "source_image", "Source Image", "image");
        var videoWorkflow = CreateWorkflow("Video Workflow", ModelBase.Flux, ModeType.Vid2Vid, "source_video", "Source Video", "video");

        var service = CreateServiceWithState(new[] { imageWorkflow, videoWorkflow }, disabledId: Guid.NewGuid());

        var target = service.GetSourceTargets(SendToMediaType.Video).Should().ContainSingle().Subject;
        target.Workflow.Id.Should().Be(videoWorkflow.Id);
        target.SourceKey.Should().Be("source_video");
        target.SourceType.Should().Be("video");
    }

    [Fact]
    public void GetParameterWorkflows_ExcludesDisabledWorkflowsForCurrentBase()
    {
        var enabledTxt2Img = new Workflow { Id = Guid.NewGuid(), Title = "Enabled Txt2Img", Base = ModelBase.Flux, Mode = ModeType.Txt2Img };
        var disabledImg2Img = new Workflow { Id = Guid.NewGuid(), Title = "Disabled Img2Img", Base = ModelBase.Flux, Mode = ModeType.Img2Img };
        var imageOnly = new Workflow { Id = Guid.NewGuid(), Title = "Image To Video", Base = ModelBase.Flux, Mode = ModeType.Img2Vid };

        var service = CreateServiceWithState(new[] { enabledTxt2Img, disabledImg2Img, imageOnly }, disabledImg2Img.Id);

        service.GetParameterWorkflows().Should().ContainSingle()
            .Which.Id.Should().Be(enabledTxt2Img.Id);
    }

    private static Workflow CreateWorkflow(string title, ModelBase modelBase, ModeType mode, string sourceId, string sourceLabel, string sourceType)
    {
        return new Workflow
        {
            Id = Guid.NewGuid(),
            Title = title,
            Base = modelBase,
            Mode = mode,
            Sources = new List<WorkflowSource>
            {
                new()
                {
                    Id = sourceId,
                    Label = sourceLabel,
                    Type = sourceType
                }
            }
        };
    }

    private static MediaSendToService CreateServiceWithState(IEnumerable<Workflow> workflows, Guid disabledId)
    {
        var state = new AppState
        {
            Generation = new AppStateGeneration
            {
                WorkflowBase = ModelBase.Flux,
                Workflows = workflows.ToList(),
                DisabledWorkflowIds = new List<Guid> { disabledId }
            }
        };

        var stateService = new Mock<IStateService>();
        stateService.SetupGet(s => s.State).Returns(state);
        stateService.SetupGet(s => s.GenerationParameters).Returns(new GenerationParameters());

        var backend = new Mock<IBackendService>();
        backend.SetupGet(b => b.IsBackendAvailable).Returns(true);

        return new MediaSendToService(
            backend.Object,
            stateService.Object,
            Mock.Of<ISessionService>(),
            Mock.Of<IIOService>(),
            Mock.Of<IOrchestratorService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());
    }
}