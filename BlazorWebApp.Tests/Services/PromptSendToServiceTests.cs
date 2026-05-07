using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class PromptSendToServiceTests
{
    [Fact]
    public void GetParameterWorkflows_ExcludesDisabledWorkflowsForCurrentBase()
    {
        var enabledTxt2Img = new Workflow { Id = Guid.NewGuid(), Title = "Enabled Txt2Img", Base = ModelBase.Flux, Mode = ModeType.Txt2Img };
        var disabledImg2Vid = new Workflow { Id = Guid.NewGuid(), Title = "Disabled Img2Vid", Base = ModelBase.Flux, Mode = ModeType.Img2Vid };
        var otherBase = new Workflow { Id = Guid.NewGuid(), Title = "Other Base", Base = ModelBase.Wan, Mode = ModeType.Txt2Img };

        var state = new AppState
        {
            Generation = new AppStateGeneration
            {
                WorkflowBase = ModelBase.Flux,
                Workflows = new List<Workflow> { enabledTxt2Img, disabledImg2Vid, otherBase },
                DisabledWorkflowIds = new List<Guid> { disabledImg2Vid.Id }
            }
        };

        var stateService = new Mock<IStateService>();
        stateService.SetupGet(s => s.State).Returns(state);

        var backend = new Mock<IBackendService>();
        backend.SetupGet(b => b.IsBackendAvailable).Returns(true);

        var service = new PromptSendToService(
            backend.Object,
            stateService.Object,
            Mock.Of<IGenerationParameterService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());

        service.GetParameterWorkflows().Should().ContainSingle()
            .Which.Id.Should().Be(enabledTxt2Img.Id);
    }
}