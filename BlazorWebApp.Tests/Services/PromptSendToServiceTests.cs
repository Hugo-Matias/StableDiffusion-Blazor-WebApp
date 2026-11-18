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
            Mock.Of<IWorkflowStateService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());

        service.GetParameterWorkflows().Should().ContainSingle()
            .Which.Id.Should().Be(enabledTxt2Img.Id);
    }

    [Fact]
    public async Task SendPromptToWorkflowAsync_ShouldComposeSavedPromptAndQueuePositiveAndNegativeOverrides()
    {
        var workflow = new Workflow { Id = Guid.NewGuid(), Title = "Portrait", Base = ModelBase.Flux, Mode = ModeType.Txt2Img };
        var saved = new GenerationParameters { WorkflowId = workflow.Id };
        var prompts = saved.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        prompts.SetValue(FragmentKeys.Params.Positive, "misty ruins");
        prompts.SetValue(FragmentKeys.Params.Negative, "low quality");

        var stateService = new Mock<IStateService>();
        stateService.SetupGet(service => service.GenerationParameters).Returns(new GenerationParameters { WorkflowId = Guid.NewGuid() });
        stateService.SetupGet(service => service.State).Returns(new AppState());

        var workflowStates = new Mock<IWorkflowStateService>();
        workflowStates.Setup(service => service.LoadWorkflowStateAsync(workflow.Id)).ReturnsAsync(saved);

        var parameters = new Mock<IGenerationParameterService>();
        var queued = new List<(string FragmentId, string Key, object? Value)>();
        parameters.Setup(service => service.QueuePendingOverride(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Callback<string, string, object?>((fragmentId, key, value) => queued.Add((fragmentId, key, value)));

        var nav = new TestNavigationManager();
        nav.SetUri("http://localhost/", "http://localhost/characters");

        var service = new PromptSendToService(
            Mock.Of<IBackendService>(),
            stateService.Object,
            parameters.Object,
            workflowStates.Object,
            nav,
            Mock.Of<MudBlazor.ISnackbar>());

        await service.SendPromptToWorkflowAsync(new PromptSendToRequest
        {
            PositivePrompt = "Kira, long silver hair",
            NegativePrompt = "different face",
            Mode = PromptApplicationMode.Prepend,
            IncludeNegativePrompt = true
        }, workflow);

        queued.Should().Contain(item => item.FragmentId == FragmentKeys.Fragments.Prompts
            && item.Key == FragmentKeys.Params.Positive
            && (string)item.Value! == "Kira, long silver hair, misty ruins");
        queued.Should().Contain(item => item.FragmentId == FragmentKeys.Fragments.Prompts
            && item.Key == FragmentKeys.Params.Negative
            && (string)item.Value! == "different face, low quality");
        nav.Uri.Should().Be($"http://localhost/generate/{workflow.Id}");
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public void SetUri(string baseUri, string uri) => Initialize(baseUri, uri);

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}