using BlazorWebApp.Services;
using BlazorWebApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Moq;
using static BlazorWebApp.Data.Enums;
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Tests.Services;

/// <summary>
/// Unit tests for ImageSendToService, focusing on the IsLocal helper.
/// </summary>
public class ImageSendToServiceTests
{
    private readonly ImageSendToService _service;

    public ImageSendToServiceTests()
    {
        _service = new ImageSendToService(
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
    public void GetImageWorkflows_ExcludesDisabledWorkflowsForCurrentBase()
    {
        var enabledImg2Img = new Workflow { Id = Guid.NewGuid(), Title = "Enabled Img2Img", Base = ModelBase.Flux, Mode = ModeType.Img2Img };
        var disabledImg2Vid = new Workflow { Id = Guid.NewGuid(), Title = "Disabled Img2Vid", Base = ModelBase.Flux, Mode = ModeType.Img2Vid };
        var otherBase = new Workflow { Id = Guid.NewGuid(), Title = "Other Base", Base = ModelBase.Wan, Mode = ModeType.Img2Img };

        var service = CreateServiceWithState(enabledImg2Img, disabledImg2Vid, otherBase, disabledImg2Vid.Id);

        service.GetImageWorkflows().Should().ContainSingle()
            .Which.Id.Should().Be(enabledImg2Img.Id);
    }

    [Fact]
    public void GetParameterWorkflows_ExcludesDisabledWorkflowsForCurrentBase()
    {
        var enabledTxt2Img = new Workflow { Id = Guid.NewGuid(), Title = "Enabled Txt2Img", Base = ModelBase.Flux, Mode = ModeType.Txt2Img };
        var disabledImg2Img = new Workflow { Id = Guid.NewGuid(), Title = "Disabled Img2Img", Base = ModelBase.Flux, Mode = ModeType.Img2Img };
        var imageOnly = new Workflow { Id = Guid.NewGuid(), Title = "Image To Video", Base = ModelBase.Flux, Mode = ModeType.Img2Vid };

        var service = CreateServiceWithState(enabledTxt2Img, disabledImg2Img, imageOnly, disabledImg2Img.Id);

        service.GetParameterWorkflows().Should().ContainSingle()
            .Which.Id.Should().Be(enabledTxt2Img.Id);
    }

    private static ImageSendToService CreateServiceWithState(Workflow first, Workflow second, Workflow third, Guid disabledId)
    {
        var state = new AppState
        {
            Generation = new AppStateGeneration
            {
                WorkflowBase = ModelBase.Flux,
                Workflows = new List<Workflow> { first, second, third },
                DisabledWorkflowIds = new List<Guid> { disabledId }
            }
        };

        var stateService = new Mock<IStateService>();
        stateService.SetupGet(s => s.State).Returns(state);

        var backend = new Mock<IBackendService>();
        backend.SetupGet(b => b.IsBackendAvailable).Returns(true);

        return new ImageSendToService(
            backend.Object,
            stateService.Object,
            Mock.Of<ISessionService>(),
            Mock.Of<IIOService>(),
            Mock.Of<IOrchestratorService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());
    }
}
