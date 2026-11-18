using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupGroupExplanationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CleanupRepository _repository;

    public CleanupGroupExplanationServiceTests()
    {
        _repository = new CleanupRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GenerateExplanationAsync_ReturnsCachedExplanationWithoutVLEnabled()
    {
        var cached = await _repository.UpsertGroupExplanationAsync(new CleanupGroupExplanation
        {
            GroupId = 12,
            RepresentativeImageId = 50,
            ModelName = "cached-model",
            Style = "Simple",
            Caption = "cached caption",
            Explanation = "cached explanation"
        });
        var service = CreateService(new CleanupGroupExplanationOptions { Enabled = false });

        var result = await service.GenerateExplanationAsync(12);

        result.Id.Should().Be(cached.Id);
        result.Caption.Should().Be("cached caption");
    }

    [Fact]
    public async Task GenerateExplanationAsync_ThrowsWhenDisabledAndCacheIsMissing()
    {
        var service = CreateService(new CleanupGroupExplanationOptions { Enabled = false });

        var act = () => service.GenerateExplanationAsync(99);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cleanup vision-language explanations are disabled.");
    }

    private CleanupGroupExplanationService CreateService(CleanupGroupExplanationOptions options)
        => new(_factory, _repository, null!, Options.Create(options));

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