using BlazorWebApp.Data;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Tests.Services;

public class GenerateStatePresetServiceTests
{
    [Fact]
    public async Task CreatePresetAsync_ClonesCurrentWorkflowParameters()
    {
        using var factory = new TestDbContextFactory();
        var service = new GenerateStatePresetService(factory);
        var workflowId = Guid.NewGuid();
        var parameters = CreateParameters(workflowId);

        var created = await service.CreatePresetAsync("  Portrait Base  ", parameters);

        parameters.Assets["Model"] = "mutated.safetensors";
        parameters.Styles.Clear();

        var loaded = await service.GetPresetAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Portrait Base");
        loaded.WorkflowId.Should().Be(workflowId);
        loaded.Body.Parameters.WorkflowId.Should().Be(workflowId);
        loaded.Body.Parameters.Assets["Model"].Should().Be("model.safetensors");
        loaded.Body.Parameters.Styles.Should().ContainSingle(s => s.Name == "cinematic");
    }

    [Fact]
    public async Task GetPresetsAsync_ReturnsOnlyTheRequestedWorkflow()
    {
        using var factory = new TestDbContextFactory();
        var service = new GenerateStatePresetService(factory);
        var workflowId = Guid.NewGuid();
        var otherWorkflowId = Guid.NewGuid();

        await service.CreatePresetAsync("Current", CreateParameters(workflowId));
        await service.CreatePresetAsync("Other", CreateParameters(otherWorkflowId));

        var presets = await service.GetPresetsAsync(workflowId);

        presets.Should().ContainSingle();
        presets[0].Name.Should().Be("Current");
    }

    [Fact]
    public async Task UpdatePresetAsync_ReplacesNameAndSnapshot()
    {
        using var factory = new TestDbContextFactory();
        var service = new GenerateStatePresetService(factory);
        var workflowId = Guid.NewGuid();
        var created = await service.CreatePresetAsync("Before", CreateParameters(workflowId));
        var replacement = CreateParameters(workflowId);
        replacement.Assets["Model"] = "replacement.safetensors";

        await service.UpdatePresetAsync(created.Id, "After", replacement);

        var loaded = await service.GetPresetAsync(created.Id);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("After");
        loaded.Body.Parameters.Assets["Model"].Should().Be("replacement.safetensors");
    }

    private static GenerationParameters CreateParameters(Guid workflowId)
    {
        var parameters = new GenerationParameters
        {
            WorkflowId = workflowId,
            Styles = new List<PromptStyle>
            {
                new()
                {
                    Name = "cinematic",
                    Prompt = "cinematic lighting, {prompt}",
                    NegativePrompt = "flat lighting",
                    Loras = new List<Lora>()
                }
            }
        };

        parameters.Assets["Model"] = "model.safetensors";
        parameters.Loras.Add(new Lora { Name = "detail", Strength = 0.8f });
        parameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts).SetValue(FragmentKeys.Params.Positive, "portrait");
        return parameters;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());

        public void Dispose()
        {
            using var context = CreateDbContext();
            context.Database.EnsureDeleted();
        }
    }
}