using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services;

public class ResourceFilterStateServiceTests
{
    private readonly Mock<IResourceCacheService> _mockCache;
    private readonly Mock<IEventService> _mockEvents;
    private readonly ResourceFilterStateService _service;

    public ResourceFilterStateServiceTests()
    {
        _mockCache = new Mock<IResourceCacheService>();
        _mockEvents = new Mock<IEventService>();
        _service = new ResourceFilterStateService(_mockCache.Object, _mockEvents.Object);
    }

    private Workflow CreateWorkflow(Guid id, List<string>? compatibleModels = null)
    {
        return new Workflow
        {
            Id = id,
            Title = "Test Workflow",
            CompatibleResourceBaseModels = compatibleModels
        };
    }

    [Fact]
    public async Task EnsureInitialized_SetsAvailableModelsFromCache()
    {
        var workflowId = Guid.NewGuid();
        var workflow = CreateWorkflow(workflowId, ["SD 1.5", "SDXL 1.0", "Pony", "Illustrious"]);

        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0", "Illustrious"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.Equal(3, _service.AvailableBaseModels.Count);
        Assert.Contains("SD 1.5", _service.AvailableBaseModels);
        Assert.Contains("SDXL 1.0", _service.AvailableBaseModels);
        Assert.Contains("Illustrious", _service.AvailableBaseModels);
        Assert.DoesNotContain("Pony", _service.AvailableBaseModels);
    }

    [Fact]
    public async Task EnsureInitialized_AllModelsEnabledByDefault()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0"]);

        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.Equal(2, _service.EnabledBaseModels.Count);
        Assert.Contains("SD 1.5", _service.EnabledBaseModels);
        Assert.Contains("SDXL 1.0", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task EnsureInitialized_DefaultsIncludeUntrackedTrue()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.True(_service.IncludeUntracked);
    }

    [Fact]
    public async Task EnsureInitialized_DefaultsAllowAllFalse()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.False(_service.AllowAll);
    }

    [Fact]
    public async Task EnsureInitialized_NoCompatibleModels_SetsAllowAll()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), null);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.True(_service.AllowAll);
        Assert.Empty(_service.AvailableBaseModels);
    }

    [Fact]
    public async Task EnsureInitialized_EmptyCompatibleModels_SetsAllowAll()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), []);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.True(_service.AllowAll);
        Assert.Empty(_service.AvailableBaseModels);
    }

    [Fact]
    public async Task EnsureInitialized_SameWorkflow_DoesNotReset()
    {
        var workflowId = Guid.NewGuid();
        var workflow = CreateWorkflow(workflowId, ["SD 1.5", "SDXL 1.0"]);

        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        // Disable one model
        _service.ToggleBaseModel("SD 1.5");
        Assert.DoesNotContain("SD 1.5", _service.EnabledBaseModels);

        // Re-initialize with same workflow
        await _service.EnsureInitializedForWorkflowAsync(workflow);

        // State should be preserved
        Assert.DoesNotContain("SD 1.5", _service.EnabledBaseModels);
        Assert.Contains("SDXL 1.0", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task EnsureInitialized_DifferentWorkflow_Resets()
    {
        var workflow1 = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0"]);
        var workflow2 = CreateWorkflow(Guid.NewGuid(), ["Flux.1 D"]);

        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.Is<IEnumerable<string>>(m => m.Contains("SD 1.5")), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.Is<IEnumerable<string>>(m => m.Contains("Flux.1 D")), It.IsAny<bool>()))
            .ReturnsAsync(["Flux.1 D"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow1);
        _service.ToggleBaseModel("SD 1.5");

        await _service.EnsureInitializedForWorkflowAsync(workflow2);

        Assert.Single(_service.AvailableBaseModels);
        Assert.Contains("Flux.1 D", _service.EnabledBaseModels);
        Assert.Equal(workflow2.Id, _service.CurrentWorkflowId);
    }

    [Fact]
    public async Task ToggleBaseModel_DisablesEnabled()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.ToggleBaseModel("SD 1.5");

        Assert.DoesNotContain("SD 1.5", _service.EnabledBaseModels);
        Assert.Contains("SDXL 1.0", _service.EnabledBaseModels);
        _mockEvents.Verify(e => e.Publish(It.IsAny<ResourceFilterChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task ToggleBaseModel_EnablesDisabled()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.ToggleBaseModel("SD 1.5");
        _service.ToggleBaseModel("SD 1.5");

        Assert.Contains("SD 1.5", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task SetIncludeUntracked_PublishesEvent()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SetIncludeUntracked(false);

        Assert.False(_service.IncludeUntracked);
        _mockEvents.Verify(e => e.Publish(It.IsAny<ResourceFilterChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task SetIncludeUntracked_SameValue_NoEvent()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SetIncludeUntracked(true); // already true

        _mockEvents.Verify(e => e.Publish(It.IsAny<ResourceFilterChangedEventArgs>()), Times.Never);
    }

    [Fact]
    public async Task SetAllowAll_PublishesEvent()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SetAllowAll(true);

        Assert.True(_service.AllowAll);
        _mockEvents.Verify(e => e.Publish(It.IsAny<ResourceFilterChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task GetEffectiveBaseModels_AllowAll_ReturnsNull()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SetAllowAll(true);

        Assert.Null(_service.GetEffectiveBaseModels());
    }

    [Fact]
    public async Task GetEffectiveBaseModels_ReturnsOnlyEnabled()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0", "Illustrious"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0", "Illustrious"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.ToggleBaseModel("SD 1.5");

        var effective = _service.GetEffectiveBaseModels();

        Assert.NotNull(effective);
        Assert.Equal(2, effective.Count);
        Assert.DoesNotContain("SD 1.5", effective);
        Assert.Contains("SDXL 1.0", effective);
        Assert.Contains("Illustrious", effective);
    }

    [Fact]
    public void GetEffectiveBaseModels_NotInitialized_ReturnsNull()
    {
        Assert.Null(_service.GetEffectiveBaseModels());
    }

    [Fact]
    public async Task EnsureInitialized_PreservesOrderFromCandidates()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SDXL 1.0", "Pony", "Illustrious", "NoobAI", "SD 1.5"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SDXL 1.0", "Illustrious", "SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.Equal("SDXL 1.0", _service.AvailableBaseModels[0]);
        Assert.Equal("Illustrious", _service.AvailableBaseModels[1]);
        Assert.Equal("SD 1.5", _service.AvailableBaseModels[2]);
    }

    [Fact]
    public async Task SoloBaseModel_DisablesAllButSelected()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0", "Illustrious"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0", "Illustrious"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SoloBaseModel("Illustrious");

        Assert.Single(_service.EnabledBaseModels);
        Assert.Contains("Illustrious", _service.EnabledBaseModels);
        _mockEvents.Verify(e => e.Publish(It.IsAny<ResourceFilterChangedEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task SoloBaseModel_AlreadySolo_ReEnablesAll()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0", "Illustrious"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0", "Illustrious"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SoloBaseModel("Illustrious");
        _service.SoloBaseModel("Illustrious");

        Assert.Equal(3, _service.EnabledBaseModels.Count);
        Assert.Contains("SD 1.5", _service.EnabledBaseModels);
        Assert.Contains("SDXL 1.0", _service.EnabledBaseModels);
        Assert.Contains("Illustrious", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task SoloBaseModel_ThenSoloDifferent_SwitchesSolo()
    {
        var workflow = CreateWorkflow(Guid.NewGuid(), ["SD 1.5", "SDXL 1.0", "Illustrious"]);
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0", "Illustrious"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.SoloBaseModel("Illustrious");
        _service.SoloBaseModel("SD 1.5");

        Assert.Single(_service.EnabledBaseModels);
        Assert.Contains("SD 1.5", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task InvalidateCurrentWorkflow_AllowsReinitialization()
    {
        var workflowId = Guid.NewGuid();
        var workflow = CreateWorkflow(workflowId, ["SD 1.5", "SDXL 1.0"]);

        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        _service.ToggleBaseModel("SD 1.5");
        Assert.DoesNotContain("SD 1.5", _service.EnabledBaseModels);

        // Invalidate and re-initialize should reset state
        _service.InvalidateCurrentWorkflow();
        Assert.Null(_service.CurrentWorkflowId);

        await _service.EnsureInitializedForWorkflowAsync(workflow);

        // State should be reset - all enabled again
        Assert.Equal(2, _service.EnabledBaseModels.Count);
        Assert.Contains("SD 1.5", _service.EnabledBaseModels);
        Assert.Contains("SDXL 1.0", _service.EnabledBaseModels);
    }

    [Fact]
    public async Task InvalidateCurrentWorkflow_RefreshesAvailableModels()
    {
        var workflowId = Guid.NewGuid();
        var workflow = CreateWorkflow(workflowId, ["SD 1.5", "SDXL 1.0"]);

        // First init: only SD 1.5 has resources
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5"]);

        await _service.EnsureInitializedForWorkflowAsync(workflow);
        Assert.Single(_service.AvailableBaseModels);

        // Now SDXL 1.0 also has resources (e.g. resource was enabled)
        _mockCache.Setup(c => c.GetDistinctBaseModelsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
            .ReturnsAsync(["SD 1.5", "SDXL 1.0"]);

        _service.InvalidateCurrentWorkflow();
        await _service.EnsureInitializedForWorkflowAsync(workflow);

        Assert.Equal(2, _service.AvailableBaseModels.Count);
        Assert.Contains("SDXL 1.0", _service.AvailableBaseModels);
    }
}
