using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class ResourceCacheServiceTests
{
    private readonly Mock<IDatabaseService> _mockDb;
    private readonly Mock<ILogger<ResourceCacheService>> _mockLogger;
    private readonly EventService _eventService;

    public ResourceCacheServiceTests()
    {
        _mockDb = new Mock<IDatabaseService>();
        _mockLogger = new Mock<ILogger<ResourceCacheService>>();
        _eventService = new EventService();
    }

    private ResourceCacheService CreateService()
    {
        return new ResourceCacheService(_mockDb.Object, _eventService, _mockLogger.Object);
    }

    private List<Resource> CreateTestResources() =>
    [
        new Resource
        {
            Id = 1,
            Filename = "model_sd15.safetensors",
            BaseModel = "SD 1.5",
            Type = new ResourceType { Id = 1, Name = "Checkpoint" }
        },
        new Resource
        {
            Id = 2,
            Filename = "model_sdxl.safetensors",
            BaseModel = "SDXL 1.0",
            Type = new ResourceType { Id = 1, Name = "Checkpoint" }
        },
        new Resource
        {
            Id = 3,
            Filename = "lora_flux.safetensors",
            BaseModel = "Flux.1 D",
            Type = new ResourceType { Id = 2, Name = "LORA" }
        },
        new Resource
        {
            Id = 4,
            Filename = "lora_nobase.safetensors",
            BaseModel = null,
            Type = new ResourceType { Id = 2, Name = "LORA" }
        }
    ];

    #region GetCachedResourcesAsync Tests

    [Fact]
    public async Task GetCachedResourcesAsync_ShouldLoadFromDbOnFirstCall()
    {
        var resources = CreateTestResources();
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(resources);
        var service = CreateService();

        var result = await service.GetCachedResourcesAsync();

        Assert.Equal(4, result.Count);
        _mockDb.Verify(db => db.GetResources(), Times.Once);
    }

    [Fact]
    public async Task GetCachedResourcesAsync_ShouldNotCallDbOnSubsequentCalls()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        await service.GetCachedResourcesAsync();
        await service.GetCachedResourcesAsync();

        _mockDb.Verify(db => db.GetResources(), Times.Once);
    }

    [Fact]
    public async Task GetCachedResourcesAsync_WithTypeId_ShouldFilterByType()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.GetCachedResourcesAsync(typeId: 2);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(2, r.Type.Id));
    }

    #endregion

    #region FindByFilenameAsync Tests

    [Fact]
    public async Task FindByFilenameAsync_ShouldFindByExactFilename()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByFilenameAsync("model_sd15.safetensors");

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task FindByFilenameAsync_ShouldBeCaseInsensitive()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByFilenameAsync("MODEL_SD15.SAFETENSORS");

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task FindByFilenameAsync_WithPath_ShouldMatchByFilenameOnly()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByFilenameAsync("checkpoints/model_sd15.safetensors");

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
    }

    [Fact]
    public async Task FindByFilenameAsync_NotFound_ShouldReturnNull()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByFilenameAsync("nonexistent.safetensors");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByFilenameAsync_NullInput_ShouldReturnNull()
    {
        var service = CreateService();

        var result = await service.FindByFilenameAsync(null!);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByFilenameAsync_EmptyInput_ShouldReturnNull()
    {
        var service = CreateService();

        var result = await service.FindByFilenameAsync("");

        Assert.Null(result);
    }

    #endregion

    #region FindByBaseModelsAsync Tests

    [Fact]
    public async Task FindByBaseModelsAsync_ShouldReturnMatchingResources()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByBaseModelsAsync(["SD 1.5", "SDXL 1.0"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.BaseModel == "SD 1.5");
        Assert.Contains(result, r => r.BaseModel == "SDXL 1.0");
    }

    [Fact]
    public async Task FindByBaseModelsAsync_ShouldBeCaseInsensitive()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByBaseModelsAsync(["sd 1.5"]);

        Assert.Single(result);
        Assert.Equal("SD 1.5", result[0].BaseModel);
    }

    [Fact]
    public async Task FindByBaseModelsAsync_WithTypeId_ShouldFilterByType()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByBaseModelsAsync(["Flux.1 D"], typeId: 2);

        Assert.Single(result);
        Assert.Equal("lora_flux.safetensors", result[0].Filename);
    }

    [Fact]
    public async Task FindByBaseModelsAsync_ShouldExcludeNullBaseModel()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByBaseModelsAsync(["SD 1.5", "SDXL 1.0", "Flux.1 D"]);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, r => r.BaseModel == null);
    }

    [Fact]
    public async Task FindByBaseModelsAsync_NoMatches_ShouldReturnEmptyList()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        var result = await service.FindByBaseModelsAsync(["Chroma"]);

        Assert.Empty(result);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task InvalidateCache_ShouldForceReloadOnNextAccess()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        await service.GetCachedResourcesAsync();
        service.InvalidateCache();
        await service.GetCachedResourcesAsync();

        _mockDb.Verify(db => db.GetResources(), Times.Exactly(2));
    }

    [Fact]
    public async Task ResourcesChangedEvent_ShouldInvalidateCache()
    {
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(CreateTestResources());
        var service = CreateService();

        // Load cache
        await service.GetCachedResourcesAsync();

        // Publish event
        _eventService.Publish(new ResourcesChangedEventArgs("test"));

        // Access again - should reload
        await service.GetCachedResourcesAsync();

        _mockDb.Verify(db => db.GetResources(), Times.Exactly(2));
    }

    #endregion

    #region GetDistinctBaseModelsAsync Tests

    [Fact]
    public async Task GetDistinctBaseModelsAsync_EnabledOnly_ExcludesDisabledResources()
    {
        var resources = new List<Resource>
        {
            new Resource { Id = 1, Filename = "sd15.safetensors", BaseModel = "SD 1.5", IsEnabled = true, Type = new ResourceType { Id = 1, Name = "Checkpoint" } },
            new Resource { Id = 2, Filename = "sdxl.safetensors", BaseModel = "SDXL 1.0", IsEnabled = false, Type = new ResourceType { Id = 1, Name = "Checkpoint" } },
            new Resource { Id = 3, Filename = "flux.safetensors", BaseModel = "Flux.1 D", IsEnabled = true, Type = new ResourceType { Id = 1, Name = "Checkpoint" } }
        };
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(resources);
        var service = CreateService();

        var result = await service.GetDistinctBaseModelsAsync(["SD 1.5", "SDXL 1.0", "Flux.1 D"], enabledOnly: true);

        Assert.Equal(2, result.Count);
        Assert.Contains("SD 1.5", result);
        Assert.Contains("Flux.1 D", result);
        Assert.DoesNotContain("SDXL 1.0", result);
    }

    [Fact]
    public async Task GetDistinctBaseModelsAsync_EnabledOnlyFalse_IncludesDisabledResources()
    {
        var resources = new List<Resource>
        {
            new Resource { Id = 1, Filename = "sd15.safetensors", BaseModel = "SD 1.5", IsEnabled = true, Type = new ResourceType { Id = 1, Name = "Checkpoint" } },
            new Resource { Id = 2, Filename = "sdxl.safetensors", BaseModel = "SDXL 1.0", IsEnabled = false, Type = new ResourceType { Id = 1, Name = "Checkpoint" } }
        };
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(resources);
        var service = CreateService();

        var result = await service.GetDistinctBaseModelsAsync(["SD 1.5", "SDXL 1.0"], enabledOnly: false);

        Assert.Equal(2, result.Count);
        Assert.Contains("SD 1.5", result);
        Assert.Contains("SDXL 1.0", result);
    }

    [Fact]
    public async Task GetDistinctBaseModelsAsync_DefaultEnabledOnly_ExcludesDisabled()
    {
        var resources = new List<Resource>
        {
            new Resource { Id = 1, Filename = "disabled.safetensors", BaseModel = "SD 2.1", IsEnabled = false, Type = new ResourceType { Id = 2, Name = "LORA" } }
        };
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(resources);
        var service = CreateService();

        var result = await service.GetDistinctBaseModelsAsync(["SD 2.1"]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDistinctBaseModelsAsync_MixedEnabledDisabled_OnlyCountsEnabledForModel()
    {
        var resources = new List<Resource>
        {
            new Resource { Id = 1, Filename = "sd15_a.safetensors", BaseModel = "SD 1.5", IsEnabled = true, Type = new ResourceType { Id = 1, Name = "Checkpoint" } },
            new Resource { Id = 2, Filename = "sd15_b.safetensors", BaseModel = "SD 1.5", IsEnabled = false, Type = new ResourceType { Id = 1, Name = "Checkpoint" } },
            new Resource { Id = 3, Filename = "sdxl_a.safetensors", BaseModel = "SDXL 1.0", IsEnabled = false, Type = new ResourceType { Id = 1, Name = "Checkpoint" } }
        };
        _mockDb.Setup(db => db.GetResources()).ReturnsAsync(resources);
        var service = CreateService();

        var result = await service.GetDistinctBaseModelsAsync(["SD 1.5", "SDXL 1.0"]);

        // SD 1.5 has at least one enabled resource, SDXL 1.0 only has disabled
        Assert.Single(result);
        Assert.Contains("SD 1.5", result);
        Assert.DoesNotContain("SDXL 1.0", result);
    }

    #endregion
}
