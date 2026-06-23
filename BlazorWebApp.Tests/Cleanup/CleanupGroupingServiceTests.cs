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
    private readonly CleanupEmbeddingVectorCodec _vectorCodec = new();

    public CleanupGroupingServiceTests()
    {
        _factory = new TestDbContextFactory();
        _repository = new CleanupRepository(_factory);
        _service = new CleanupGroupingService(
            _factory,
            _repository,
            new StaticEmbeddingMetadataService(),
            new StaticScoringMetadataService(),
            _vectorCodec,
            NullLogger<CleanupGroupingService>.Instance);
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

    [Fact]
    public async Task GenerateGroupsAsync_BuildsVisualSimilarityGroupsFromCurrentModelEmbeddings()
    {
        await SeedImagesAsync(
            Image(1, favorite: false, score: 0),
            Image(2, favorite: true, score: 0),
            Image(3, favorite: false, score: 0));
        await SeedIndexesAsync(
            Indexed(1, fileSize: 100),
            Indexed(2, fileSize: 200),
            Indexed(3, fileSize: 300));
        await SeedEmbeddingsAsync(
            Embedding(1, new[] { 1f, 0f }),
            Embedding(2, new[] { 0.98f, 0.02f }),
            Embedding(3, new[] { -1f, 0f }));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.VisualSimilarity,
            VisualSimilarityMinSimilarity = 0.95
        });

        result.Status.Should().Be(CleanupGroupRunStatus.Completed);
        result.TotalGroups.Should().Be(1);
        result.TotalMembers.Should().Be(2);

        var groups = await _repository.GetGroupsAsync(result.RunId, 0, 10);
        groups.Should().ContainSingle();
        groups[0].Strategy.Should().Be(CleanupGroupingStrategy.VisualSimilarity);
        groups[0].RepresentativeImageId.Should().Be(2);
        groups[0].EstimatedBytes.Should().Be(300);
        groups[0].MinSimilarity.Should().BeGreaterThan(0.95);

        var members = await _repository.GetGroupMembersAsync(groups[0].Id);
        members.Select(member => member.ImageId).Should().Equal(2, 1);
        members[0].Role.Should().Be(CleanupGroupMemberRole.Representative);
        members[0].SuggestedAction.Should().Be(CleanupSuggestedAction.Keep);
        members[1].Role.Should().Be(CleanupGroupMemberRole.CleanupCandidate);
        members[1].SuggestedAction.Should().Be(CleanupSuggestedAction.Review);
    }

    [Fact]
    public async Task GenerateGroupsAsync_IgnoresEmbeddingsFromDifferentModelIdentity()
    {
        await SeedImagesAsync(Image(1), Image(2));
        await SeedIndexesAsync(Indexed(1), Indexed(2));
        await SeedEmbeddingsAsync(
            Embedding(1, new[] { 1f, 0f }),
            Embedding(2, new[] { 1f, 0f }, modelHash: "old-hash"));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.VisualSimilarity,
            VisualSimilarityMinSimilarity = 0.95
        });

        result.Status.Should().Be(CleanupGroupRunStatus.Completed);
        result.TotalGroups.Should().Be(0);
        result.TotalMembers.Should().Be(0);
    }

    [Fact]
    public async Task GenerateGroupsAsync_BuildsLowValueGroupsFromCurrentScoreModel()
    {
        await SeedImagesAsync(
            Image(1, favorite: false, score: 0),
            Image(2, favorite: false, score: 0),
            Image(3, favorite: true, score: 0),
            Image(4, favorite: false, score: 0));
        await SeedIndexesAsync(
            Indexed(1, fileSize: 100),
            Indexed(2, fileSize: 200),
            Indexed(3, fileSize: 300),
            Indexed(4, fileSize: 400));
        await SeedScoresAsync(
            Score(1, 0.1),
            Score(2, 0.12),
            Score(3, 0.13),
            Score(4, 0.9));

        var result = await _service.GenerateGroupsAsync(new CleanupGroupingOptions
        {
            Strategy = CleanupGroupingStrategy.LowValueCandidates,
            ScoreCleanupThreshold = 0.35,
            MinimumGroupSize = 2
        });

        result.Status.Should().Be(CleanupGroupRunStatus.Completed);
        result.TotalGroups.Should().Be(1);
        result.TotalMembers.Should().Be(3);

        var groups = await _repository.GetGroupsAsync(result.RunId, 0, 10);
        groups.Should().ContainSingle();
        groups[0].Strategy.Should().Be(CleanupGroupingStrategy.LowValueCandidates);
        groups[0].RepresentativeImageId.Should().Be(3);
        groups[0].MinSimilarity.Should().Be(0.1);
        groups[0].MaxSimilarity.Should().Be(0.13);

        var members = await _repository.GetGroupMembersAsync(groups[0].Id);
        members.Select(member => member.ImageId).Should().Equal(3, 1, 2);
        members[0].Role.Should().Be(CleanupGroupMemberRole.Representative);
        members[1].Role.Should().Be(CleanupGroupMemberRole.CleanupCandidate);
        members[2].Role.Should().Be(CleanupGroupMemberRole.CleanupCandidate);
    }

    private async Task SeedImagesAsync(params Image[] images)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Images.AddRange(images);
        await context.SaveChangesAsync();
    }

    private async Task SeedIndexesAsync(params CleanupImageIndex[] indexes)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.CleanupImageIndexes.AddRange(indexes);
        await context.SaveChangesAsync();
    }

    private async Task SeedEmbeddingsAsync(params CleanupImageEmbedding[] embeddings)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.CleanupImageEmbeddings.AddRange(embeddings);
        await context.SaveChangesAsync();
    }

    private async Task SeedScoresAsync(params CleanupImageScore[] scores)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.CleanupImageScores.AddRange(scores);
        await context.SaveChangesAsync();
    }

    private static Image Image(int imageId, bool favorite = false, int score = 0)
    {
        return new Image
        {
            Id = imageId,
            Path = $"image-{imageId}.png",
            ProjectId = 1,
            ModeId = 1,
            Favorite = favorite,
            Score = score,
            Scheduler = "normal"
        };
    }

    private CleanupImageEmbedding Embedding(int imageId, float[] vector, string modelHash = "model-hash")
    {
        return new CleanupImageEmbedding
        {
            ImageId = imageId,
            ModelKey = "test-model",
            ModelHash = modelHash,
            RuntimeProvider = "CPU",
            Dimensions = vector.Length,
            Vector = _vectorCodec.Serialize(_vectorCodec.Normalize(vector)),
            Status = CleanupEmbeddingStatus.Indexed,
            IndexedAtUtc = DateTime.UtcNow
        };
    }

    private static CleanupImageScore Score(int imageId, double score, string modelHash = "score-hash")
    {
        return new CleanupImageScore
        {
            ImageId = imageId,
            ModelKey = "aesthetic-test-model",
            ModelHash = modelHash,
            ScoreName = "aesthetic",
            RuntimeProvider = "CPU",
            Score = score,
            MinScore = 0,
            MaxScore = 1,
            Status = CleanupScoreStatus.Indexed,
            IndexedAtUtc = DateTime.UtcNow
        };
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

    private sealed class StaticEmbeddingMetadataService : ICleanupEmbeddingModelMetadataService
    {
        public CleanupEmbeddingOptions Options { get; } = new();

        public CleanupEmbeddingModelValidationResult Validate(CleanupEmbeddingOptions options) => new();

        public Task<CleanupEmbeddingModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupEmbeddingModelIdentity
            {
                ModelKey = "test-model",
                ModelHash = "model-hash",
                RuntimeProvider = "CPU",
                Dimensions = 2
            });
    }

    private sealed class StaticScoringMetadataService : ICleanupScoringModelMetadataService
    {
        public CleanupScoringOptions Options { get; } = new()
        {
            Enabled = true,
            Model = new CleanupScoreModelOptions
            {
                ModelKey = "aesthetic-test-model",
                ModelPath = "score.onnx",
                ScoreName = "aesthetic",
                InputName = "input",
                OutputName = "score"
            }
        };

        public CleanupScoringValidationResult Validate(CleanupScoringOptions options) => new();

        public Task<CleanupScoreModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupScoreModelIdentity
            {
                ModelKey = "aesthetic-test-model",
                ModelHash = "score-hash",
                ScoreName = "aesthetic",
                MinScore = 0,
                MaxScore = 1,
                RuntimeProvider = "CPU"
            });
    }
}