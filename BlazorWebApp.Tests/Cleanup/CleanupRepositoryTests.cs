using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly CleanupRepository _repository;

    public CleanupRepositoryTests()
    {
        _factory = new TestDbContextFactory();
        _repository = new CleanupRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task UpsertImageIndexAsync_CreatesAndUpdates_ByImageId()
    {
        var created = await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 10,
            ProjectId = 2,
            ModeId = 1,
            ImagePath = "first.png",
            FileExists = true,
            FileSizeBytes = 1024,
            PromptFingerprint = "portrait"
        });

        var updated = await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 10,
            ProjectId = 2,
            ModeId = 1,
            ImagePath = "first-renamed.png",
            FileExists = false,
            Status = CleanupIndexStatus.MissingFile,
            PromptFingerprint = "portrait"
        });

        updated.Id.Should().Be(created.Id);

        var loaded = await _repository.GetImageIndexAsync(10);
        loaded.Should().NotBeNull();
        loaded!.ImagePath.Should().Be("first-renamed.png");
        loaded.FileExists.Should().BeFalse();
        loaded.Status.Should().Be(CleanupIndexStatus.MissingFile);
        loaded.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
        loaded.UpdatedAtUtc.Should().BeOnOrAfter(created.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetStaleAndMissingQueries_ReturnExpectedRows()
    {
        var oldIndexedAt = DateTime.UtcNow.AddDays(-4);

        await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 1,
            ProjectId = 1,
            ModeId = 1,
            ImagePath = "old.png",
            FileExists = true,
            Status = CleanupIndexStatus.Indexed,
            IndexedAtUtc = oldIndexedAt,
            UpdatedAtUtc = oldIndexedAt
        });

        await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 2,
            ProjectId = 1,
            ModeId = 1,
            ImagePath = "missing.png",
            FileExists = false,
            Status = CleanupIndexStatus.MissingFile
        });

        var stale = await _repository.GetStaleImageIndexesAsync(DateTime.UtcNow.AddDays(-1), 10);
        var missing = await _repository.GetMissingFileIndexesAsync(0, 10);

        stale.Select(row => row.ImageId).Should().Contain(new[] { 1, 2 });
        missing.Should().ContainSingle(row => row.ImageId == 2);
    }

    [Fact]
    public async Task UpsertImageEmbeddingAsync_CreatesAndUpdates_ByImageAndModelIdentity()
    {
        var created = await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 42,
            ModelKey = "clip-vit-b32",
            ModelHash = "hash-a",
            RuntimeProvider = "CPU",
            Dimensions = 3,
            Vector = new byte[] { 1, 2, 3, 4 },
            Status = CleanupEmbeddingStatus.Indexed
        });

        var updated = await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 42,
            ModelKey = "clip-vit-b32",
            ModelHash = "hash-a",
            RuntimeProvider = "CUDA",
            Dimensions = 3,
            Vector = new byte[] { 4, 3, 2, 1 },
            Status = CleanupEmbeddingStatus.Stale
        });

        updated.Id.Should().Be(created.Id);

        var loaded = await _repository.GetImageEmbeddingAsync(42, "clip-vit-b32", "hash-a");
        loaded.Should().NotBeNull();
        loaded!.RuntimeProvider.Should().Be("CUDA");
        loaded.Vector.Should().Equal(4, 3, 2, 1);
        loaded.Status.Should().Be(CleanupEmbeddingStatus.Stale);
        loaded.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
        loaded.UpdatedAtUtc.Should().BeOnOrAfter(created.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetStaleImageEmbeddingsAsync_ReturnsRowsForChangedModelOrDimensions()
    {
        await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 1,
            ModelKey = "clip-vit-b32",
            ModelHash = "old-hash",
            Dimensions = 512,
            Vector = new byte[] { 1, 2, 3, 4 },
            Status = CleanupEmbeddingStatus.Indexed
        });
        await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 2,
            ModelKey = "clip-vit-b32",
            ModelHash = "new-hash",
            Dimensions = 768,
            Vector = new byte[] { 1, 2, 3, 4 },
            Status = CleanupEmbeddingStatus.Indexed
        });
        await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 3,
            ModelKey = "clip-vit-b32",
            ModelHash = "new-hash",
            Dimensions = 512,
            Vector = new byte[] { 1, 2, 3, 4 },
            Status = CleanupEmbeddingStatus.Indexed
        });
        await _repository.UpsertImageEmbeddingAsync(new CleanupImageEmbedding
        {
            ImageId = 4,
            ModelKey = "other-model",
            ModelHash = "old-hash",
            Dimensions = 512,
            Vector = new byte[] { 1, 2, 3, 4 },
            Status = CleanupEmbeddingStatus.Stale
        });

        var stale = await _repository.GetStaleImageEmbeddingsAsync("clip-vit-b32", "new-hash", 512, 10);

        stale.Select(row => row.ImageId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task UpsertImageScoreAsync_CreatesAndUpdates_ByImageModelAndScoreName()
    {
        var created = await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 50,
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "aesthetic",
            RuntimeProvider = "CPU",
            Score = 0.25,
            MinScore = 0,
            MaxScore = 1,
            Status = CleanupScoreStatus.Indexed
        });

        var updated = await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 50,
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "aesthetic",
            RuntimeProvider = "CUDA",
            Score = 0.75,
            MinScore = 0,
            MaxScore = 1,
            Status = CleanupScoreStatus.Stale
        });

        updated.Id.Should().Be(created.Id);

        var loaded = await _repository.GetImageScoreAsync(50, "aesthetic-v1", "hash-a", "aesthetic");
        loaded.Should().NotBeNull();
        loaded!.RuntimeProvider.Should().Be("CUDA");
        loaded.Score.Should().Be(0.75);
        loaded.Status.Should().Be(CleanupScoreStatus.Stale);
        loaded.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
        loaded.UpdatedAtUtc.Should().BeOnOrAfter(created.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetImageScoresAsync_FiltersLowIndexedScores()
    {
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 1,
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "aesthetic",
            Score = 0.2,
            Status = CleanupScoreStatus.Indexed
        });
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 2,
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "aesthetic",
            Score = 0.8,
            Status = CleanupScoreStatus.Indexed
        });
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 3,
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "quality",
            Score = 0.1,
            Status = CleanupScoreStatus.Indexed
        });

        var scores = await _repository.GetImageScoresAsync(new CleanupScoreFilter
        {
            ModelKey = "aesthetic-v1",
            ModelHash = "hash-a",
            ScoreName = "aesthetic",
            Status = CleanupScoreStatus.Indexed,
            MaxScore = 0.5
        });

        scores.Should().ContainSingle(score => score.ImageId == 1);
    }

    [Fact]
    public async Task GetStaleImageScoresAsync_ReturnsRowsForChangedModel()
    {
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 1,
            ModelKey = "aesthetic-v1",
            ModelHash = "old-hash",
            ScoreName = "aesthetic",
            Score = 0.2,
            Status = CleanupScoreStatus.Indexed
        });
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 2,
            ModelKey = "aesthetic-v1",
            ModelHash = "new-hash",
            ScoreName = "aesthetic",
            Score = 0.8,
            Status = CleanupScoreStatus.Stale
        });
        await _repository.UpsertImageScoreAsync(new CleanupImageScore
        {
            ImageId = 3,
            ModelKey = "aesthetic-v1",
            ModelHash = "new-hash",
            ScoreName = "aesthetic",
            Score = 0.6,
            Status = CleanupScoreStatus.Indexed
        });

        var stale = await _repository.GetStaleImageScoresAsync("aesthetic-v1", "new-hash", "aesthetic", 10);

        stale.Select(row => row.ImageId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task AddGroupsAsync_PersistsGroupsAndMembers_ForRun()
    {
        var run = await _repository.CreateGroupRunAsync(new CleanupGroupRun
        {
            Name = "Prompt duplicates",
            Strategy = CleanupGroupingStrategy.PromptFingerprint,
            Status = CleanupGroupRunStatus.Completed,
            ProjectId = 3
        });

        await _repository.AddGroupsAsync(run.Id, new[]
        {
            new CleanupGroup
            {
                GroupKey = "prompt:abc",
                Strategy = CleanupGroupingStrategy.PromptFingerprint,
                Reason = "same prompt fingerprint",
                RepresentativeImageId = 101,
                MemberCount = 2,
                Members =
                {
                    new CleanupGroupMember
                    {
                        ImageId = 101,
                        Role = CleanupGroupMemberRole.Representative,
                        SuggestedAction = CleanupSuggestedAction.Keep,
                        SortOrder = 0
                    },
                    new CleanupGroupMember
                    {
                        ImageId = 102,
                        Role = CleanupGroupMemberRole.CleanupCandidate,
                        SuggestedAction = CleanupSuggestedAction.Review,
                        SortOrder = 1
                    }
                }
            }
        });

        var groups = await _repository.GetGroupsAsync(run.Id, 0, 10);
        groups.Should().ContainSingle();
        groups[0].RunId.Should().Be(run.Id);
        groups[0].GroupKey.Should().Be("prompt:abc");

        var members = await _repository.GetGroupMembersAsync(groups[0].Id);
        members.Should().HaveCount(2);
        members.Select(member => member.ImageId).Should().Equal(101, 102);
        members[0].Role.Should().Be(CleanupGroupMemberRole.Representative);

        var previewMembers = await _repository.GetGroupMembersAsync(groups[0].Id, 1);
        previewMembers.Should().ContainSingle();
        previewMembers[0].ImageId.Should().Be(101);
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