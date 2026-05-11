using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupGroupingServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly CleanupRepository _repository;
    private readonly CleanupGroupingService _service;

    public CleanupGroupingServiceTests()
    {
        _factory = new TestDbContextFactory();
        _repository = new CleanupRepository(_factory);
        _service = new CleanupGroupingService(_factory, _repository, NullLogger<CleanupGroupingService>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GenerateGroupsAsync_PersistsExactDuplicateGroups()
    {
        await SeedIndexesAsync(
            Indexed(1, exactHash: "abc", fileSize: 100),
            Indexed(2, exactHash: "abc", fileSize: 120),
            Indexed(3, exactHash: "def", fileSize: 80));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.ExactDuplicate
        });

        result.Status.Should().Be(CleanupGroupRunStatus.Completed);
        result.TotalGroups.Should().Be(1);
        result.TotalMembers.Should().Be(2);

        var groups = await _repository.GetGroupsAsync(result.RunId, 0, 10);
        groups.Should().ContainSingle();
        groups[0].Strategy.Should().Be(CleanupGroupingStrategy.ExactDuplicate);
        groups[0].RepresentativeImageId.Should().Be(1);
        groups[0].EstimatedBytes.Should().Be(220);

        var members = await _repository.GetGroupMembersAsync(groups[0].Id);
        members.Select(member => member.ImageId).Should().Equal(1, 2);
        members[0].Role.Should().Be(CleanupGroupMemberRole.Representative);
        members[1].Role.Should().Be(CleanupGroupMemberRole.CleanupCandidate);
    }

    [Fact]
    public async Task GenerateGroupsAsync_BuildsPromptFuzzyGroupsFromTokenOverlap()
    {
        await SeedIndexesAsync(
            Indexed(1, tokenSignature: "blue hair|portrait|smile"),
            Indexed(2, tokenSignature: "blue hair|portrait|smile"),
            Indexed(3, tokenSignature: "landscape|mountain"));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.PromptFuzzy,
            PromptFuzzyMinSimilarity = 0.8
        });

        result.TotalGroups.Should().Be(1);
        var groups = await _repository.GetGroupsAsync(result.RunId, 0, 10);
        groups.Should().ContainSingle(group => group.Strategy == CleanupGroupingStrategy.PromptFuzzy);
    }

    [Fact]
    public async Task GenerateGroupsAsync_BuildsNearDuplicateGroupsFromPerceptualHashDistance()
    {
        await SeedIndexesAsync(
            Indexed(1, perceptualHash: "0000000000000000"),
            Indexed(2, perceptualHash: "0000000000000001"),
            Indexed(3, perceptualHash: "ffffffffffffffff"));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.NearDuplicate,
            PerceptualHashMaxDistance = 1
        });

        result.TotalGroups.Should().Be(1);
        var groups = await _repository.GetGroupsAsync(result.RunId, 0, 10);
        groups.Should().ContainSingle();
        groups[0].MinSimilarity.Should().BeApproximately(63.0 / 64.0, 0.0001);
    }

    private async Task SeedIndexesAsync(params CleanupImageIndex[] indexes)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.CleanupImageIndexes.AddRange(indexes);
        await context.SaveChangesAsync();
    }

    private static CleanupImageIndex Indexed(
        int imageId,
        string? exactHash = null,
        string? perceptualHash = null,
        string? promptFingerprint = null,
        string? tokenSignature = null,
        long fileSize = 10)
    {
        return new CleanupImageIndex
        {
            ImageId = imageId,
            ProjectId = 1,
            ModeId = 1,
            ImagePath = $"image-{imageId}.png",
            FileExists = true,
            FileSizeBytes = fileSize,
            ExactHash = exactHash,
            PerceptualHash = perceptualHash,
            PromptFingerprint = promptFingerprint,
            PromptTokenSignature = tokenSignature,
            Status = CleanupIndexStatus.Indexed,
            IndexedAtUtc = DateTime.UtcNow
        };
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