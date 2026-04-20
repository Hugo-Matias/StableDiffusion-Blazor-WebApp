using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class ResourceFilterServiceTests
{
    private readonly Mock<IResourceCacheService> _mockCache;
    private readonly Mock<ILogger<ResourceFilterService>> _mockLogger;

    public ResourceFilterServiceTests()
    {
        _mockCache = new Mock<IResourceCacheService>();
        _mockLogger = new Mock<ILogger<ResourceFilterService>>();
    }

    private ResourceFilterService CreateService() => new(_mockCache.Object, _mockLogger.Object);

    private static Workflow CreateWorkflow(List<string>? compatibleBaseModels = null) => new()
    {
        Title = "Test Workflow",
        CompatibleResourceBaseModels = compatibleBaseModels
    };

    private void SetupCacheFinds(params (string filename, string? baseModel)[] entries)
    {
        foreach (var (filename, baseModel) in entries)
        {
            _mockCache.Setup(c => c.FindByFilenameAsync(filename))
                .ReturnsAsync(new Resource
                {
                    Id = 1,
                    Filename = filename,
                    BaseModel = baseModel,
                    Type = new ResourceType { Id = 1, Name = "Checkpoint" }
                });
        }
    }

    #region No-op filtering (workflow has no compatibility list)

    [Fact]
    public async Task FilterAssets_NullCompatibility_ReturnsAll()
    {
        var svc = CreateService();
        var names = new List<string> { "a.safetensors", "b.safetensors" };
        var workflow = CreateWorkflow(null);

        var result = await svc.FilterAssetsByWorkflowAsync(names, workflow);

        Assert.Equal(names, result);
    }

    [Fact]
    public async Task FilterAssets_EmptyCompatibility_ReturnsAll()
    {
        var svc = CreateService();
        var names = new List<string> { "a.safetensors", "b.safetensors" };
        var workflow = CreateWorkflow([]);

        var result = await svc.FilterAssetsByWorkflowAsync(names, workflow);

        Assert.Equal(names, result);
    }

    [Fact]
    public async Task FilterAssets_EmptyInput_ReturnsEmpty()
    {
        var svc = CreateService();
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync([], workflow);

        Assert.Empty(result);
    }

    #endregion

    #region Compatible resources pass through

    [Fact]
    public async Task FilterAssets_MatchingBaseModel_Included()
    {
        var svc = CreateService();
        SetupCacheFinds(("sd15.safetensors", "SD 1.5"));
        var workflow = CreateWorkflow(["SD 1.5", "SDXL 1.0"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["sd15.safetensors"], workflow);

        Assert.Single(result);
        Assert.Equal("sd15.safetensors", result[0]);
    }

    [Fact]
    public async Task FilterAssets_CaseInsensitiveBaseModel_Included()
    {
        var svc = CreateService();
        SetupCacheFinds(("model.safetensors", "sd 1.5"));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["model.safetensors"], workflow);

        Assert.Single(result);
    }

    [Fact]
    public async Task FilterAssets_MultipleMatches_AllIncluded()
    {
        var svc = CreateService();
        SetupCacheFinds(
            ("a.safetensors", "SD 1.5"),
            ("b.safetensors", "SDXL 1.0"),
            ("c.safetensors", "Flux.1 D")
        );
        var workflow = CreateWorkflow(["SD 1.5", "SDXL 1.0"]);

        var result = await svc.FilterAssetsByWorkflowAsync(
            ["a.safetensors", "b.safetensors", "c.safetensors"], workflow);

        Assert.Equal(2, result.Count);
        Assert.Contains("a.safetensors", result);
        Assert.Contains("b.safetensors", result);
    }

    #endregion

    #region Incompatible resources excluded

    [Fact]
    public async Task FilterAssets_IncompatibleBaseModel_Excluded()
    {
        var svc = CreateService();
        SetupCacheFinds(("flux_model.safetensors", "Flux.1 D"));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["flux_model.safetensors"], workflow);

        Assert.Empty(result);
    }

    #endregion

    #region Untracked resource handling

    [Fact]
    public async Task FilterAssets_NoResourceRecord_IncludedWhenUntrackedTrue()
    {
        var svc = CreateService();
        _mockCache.Setup(c => c.FindByFilenameAsync("unknown.safetensors"))
            .ReturnsAsync((Resource?)null);
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["unknown.safetensors"], workflow, includeUntracked: true);

        Assert.Single(result);
    }

    [Fact]
    public async Task FilterAssets_NoResourceRecord_ExcludedWhenUntrackedFalse()
    {
        var svc = CreateService();
        _mockCache.Setup(c => c.FindByFilenameAsync("unknown.safetensors"))
            .ReturnsAsync((Resource?)null);
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["unknown.safetensors"], workflow, includeUntracked: false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FilterAssets_NullBaseModel_IncludedWhenUntrackedTrue()
    {
        var svc = CreateService();
        SetupCacheFinds(("nobase.safetensors", null));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["nobase.safetensors"], workflow, includeUntracked: true);

        Assert.Single(result);
    }

    [Fact]
    public async Task FilterAssets_NullBaseModel_ExcludedWhenUntrackedFalse()
    {
        var svc = CreateService();
        SetupCacheFinds(("nobase.safetensors", null));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["nobase.safetensors"], workflow, includeUntracked: false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FilterAssets_EmptyBaseModel_IncludedWhenUntrackedTrue()
    {
        var svc = CreateService();
        SetupCacheFinds(("emptybase.safetensors", ""));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(["emptybase.safetensors"], workflow, includeUntracked: true);

        Assert.Single(result);
    }

    #endregion

    #region LoRA filtering uses same logic

    [Fact]
    public async Task FilterLoras_MatchingBaseModel_Included()
    {
        var svc = CreateService();
        SetupCacheFinds(("lora_sd15.safetensors", "SD 1.5"));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterLorasByWorkflowAsync(["lora_sd15.safetensors"], workflow);

        Assert.Single(result);
    }

    [Fact]
    public async Task FilterLoras_IncompatibleBaseModel_Excluded()
    {
        var svc = CreateService();
        SetupCacheFinds(("lora_flux.safetensors", "Flux.1 D"));
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterLorasByWorkflowAsync(["lora_flux.safetensors"], workflow);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FilterLoras_NullCompatibility_ReturnsAll()
    {
        var svc = CreateService();
        var names = new List<string> { "lora_a.safetensors", "lora_b.safetensors" };
        var workflow = CreateWorkflow(null);

        var result = await svc.FilterLorasByWorkflowAsync(names, workflow);

        Assert.Equal(names, result);
    }

    #endregion

    #region Mixed scenario

    [Fact]
    public async Task FilterAssets_MixedTrackedUntrackedIncompatible_CorrectResults()
    {
        var svc = CreateService();
        SetupCacheFinds(
            ("compatible.safetensors", "SD 1.5"),
            ("incompatible.safetensors", "Flux.1 D"),
            ("nobase.safetensors", null)
        );
        _mockCache.Setup(c => c.FindByFilenameAsync("untracked.safetensors"))
            .ReturnsAsync((Resource?)null);

        var workflow = CreateWorkflow(["SD 1.5"]);
        var input = new List<string>
        {
            "compatible.safetensors",
            "incompatible.safetensors",
            "nobase.safetensors",
            "untracked.safetensors"
        };

        var result = await svc.FilterAssetsByWorkflowAsync(input, workflow, includeUntracked: true);

        Assert.Equal(3, result.Count);
        Assert.Contains("compatible.safetensors", result);
        Assert.Contains("nobase.safetensors", result);
        Assert.Contains("untracked.safetensors", result);
        Assert.DoesNotContain("incompatible.safetensors", result);
    }

    [Fact]
    public async Task FilterAssets_MixedTrackedUntrackedIncompatible_UntrackedFalse_OnlyCompatible()
    {
        var svc = CreateService();
        SetupCacheFinds(
            ("compatible.safetensors", "SD 1.5"),
            ("incompatible.safetensors", "Flux.1 D"),
            ("nobase.safetensors", null)
        );
        _mockCache.Setup(c => c.FindByFilenameAsync("untracked.safetensors"))
            .ReturnsAsync((Resource?)null);

        var workflow = CreateWorkflow(["SD 1.5"]);
        var input = new List<string>
        {
            "compatible.safetensors",
            "incompatible.safetensors",
            "nobase.safetensors",
            "untracked.safetensors"
        };

        var result = await svc.FilterAssetsByWorkflowAsync(input, workflow, includeUntracked: false);

        Assert.Single(result);
        Assert.Equal("compatible.safetensors", result[0]);
    }

    #endregion

    #region Order preservation

    [Fact]
    public async Task FilterAssets_PreservesOriginalOrder()
    {
        var svc = CreateService();
        SetupCacheFinds(
            ("c.safetensors", "SD 1.5"),
            ("a.safetensors", "SD 1.5"),
            ("b.safetensors", "SD 1.5")
        );
        var workflow = CreateWorkflow(["SD 1.5"]);

        var result = await svc.FilterAssetsByWorkflowAsync(
            ["c.safetensors", "a.safetensors", "b.safetensors"], workflow);

        Assert.Equal(["c.safetensors", "a.safetensors", "b.safetensors"], result);
    }

    #endregion
}
