using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupOnnxIndexingServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly FakeEmbeddingIndexer _embeddingIndexer = new();
    private readonly FakeScoreIndexer _scoreIndexer = new();
    private readonly FakeComfyBatchIndexer _comfyBatchIndexer = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task IndexAsync_SkipsCurrentModelRowsAndIndexesMissingRows()
    {
        await SeedImagesAsync(1, 2);
        await SeedIndexesAsync(projectId: 7, 1, 2);
        await SeedExistingOnnxRowsAsync(1);
        var service = CreateService(embeddingsEnabled: true, scoresEnabled: true);

        var result = await service.IndexAsync(new CleanupOnnxIndexingOptions
        {
            ProjectId = 7,
            BatchSize = 1
        });

        result.Processed.Should().Be(2);
        result.EmbeddingsSkipped.Should().Be(1);
        result.EmbeddingsIndexed.Should().Be(1);
        result.ScoresSkipped.Should().Be(1);
        result.ScoresIndexed.Should().Be(1);
        _embeddingIndexer.ImageIds.Should().ContainSingle().Which.Should().Be(2);
        _scoreIndexer.ImageIds.Should().ContainSingle().Which.Should().Be(2);
    }

    [Fact]
    public async Task IndexAsync_ThrowsWhenNoOnnxOptionsAreEnabled()
    {
        var service = CreateService(embeddingsEnabled: false, scoresEnabled: false);

        var act = () => service.IndexAsync(new CleanupOnnxIndexingOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cleanup:Embeddings*Cleanup:Scoring*");
    }

    [Fact]
    public async Task IndexAsync_UsesComfyBatchIndexerWhenRuntimeIsComfy()
    {
        await SeedImagesAsync(1, 2, 3);
        await SeedIndexesAsync(projectId: 7, 1, 2, 3);
        _comfyBatchIndexer.Result = new CleanupComfyBatchIndexingResult
        {
            EmbeddingsIndexed = 3,
            ScoresIndexed = 3
        };
        var service = CreateService(
            embeddingsEnabled: true,
            scoresEnabled: true,
            embeddingProvider: CleanupEmbeddingRuntimeProvider.ComfyUI,
            scoringProvider: CleanupEmbeddingRuntimeProvider.ComfyUI);

        var result = await service.IndexAsync(new CleanupOnnxIndexingOptions
        {
            ProjectId = 7,
            BatchSize = 50
        });

        result.Processed.Should().Be(3);
        result.EmbeddingsIndexed.Should().Be(3);
        result.ScoresIndexed.Should().Be(3);
        _comfyBatchIndexer.Calls.Should().Be(1);
        _comfyBatchIndexer.EmbeddingImageIds.Should().Equal(1, 2, 3);
        _comfyBatchIndexer.ScoreImageIds.Should().Equal(1, 2, 3);
        _embeddingIndexer.ImageIds.Should().BeEmpty();
        _scoreIndexer.ImageIds.Should().BeEmpty();
    }

    private CleanupOnnxIndexingService CreateService(
        bool embeddingsEnabled,
        bool scoresEnabled,
        CleanupEmbeddingRuntimeProvider embeddingProvider = CleanupEmbeddingRuntimeProvider.CUDA,
        CleanupEmbeddingRuntimeProvider scoringProvider = CleanupEmbeddingRuntimeProvider.CUDA)
        => new(
            _factory,
            new StaticEmbeddingMetadataService(embeddingsEnabled, embeddingProvider),
            new StaticScoringMetadataService(scoresEnabled, scoringProvider),
            _embeddingIndexer,
            _scoreIndexer,
            _comfyBatchIndexer,
            NullLogger<CleanupOnnxIndexingService>.Instance);

    private async Task SeedImagesAsync(params int[] imageIds)
    {
        await using var context = await _factory.CreateDbContextAsync();
        foreach (var imageId in imageIds)
        {
            context.Images.Add(new Image
            {
                Id = imageId,
                Path = $"image-{imageId}.png",
                ProjectId = 7,
                ModeId = 1,
                Scheduler = "normal"
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedIndexesAsync(int projectId, params int[] imageIds)
    {
        await using var context = await _factory.CreateDbContextAsync();
        foreach (var imageId in imageIds)
        {
            context.CleanupImageIndexes.Add(new CleanupImageIndex
            {
                ImageId = imageId,
                ProjectId = projectId,
                ModeId = 1,
                ImagePath = $"image-{imageId}.png",
                FileExists = true,
                Status = CleanupIndexStatus.Indexed,
                IndexedAtUtc = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedExistingOnnxRowsAsync(int imageId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.CleanupImageEmbeddings.Add(new CleanupImageEmbedding
        {
            ImageId = imageId,
            ModelKey = "embedding-model",
            ModelHash = "embedding-hash",
            RuntimeProvider = "CUDA",
            Dimensions = 2,
            Vector = new byte[] { 1, 2 },
            Status = CleanupEmbeddingStatus.Indexed,
            IndexedAtUtc = DateTime.UtcNow
        });
        context.CleanupImageScores.Add(new CleanupImageScore
        {
            ImageId = imageId,
            ModelKey = "score-model",
            ModelHash = "score-hash",
            ScoreName = "aesthetic",
            RuntimeProvider = "CUDA",
            Score = 0.8,
            Status = CleanupScoreStatus.Indexed,
            IndexedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private sealed class StaticEmbeddingMetadataService : ICleanupEmbeddingModelMetadataService
    {
        public StaticEmbeddingMetadataService(bool enabled, CleanupEmbeddingRuntimeProvider provider)
        {
            Options = new CleanupEmbeddingOptions { Enabled = enabled, RuntimeProvider = provider };
        }

        public CleanupEmbeddingOptions Options { get; }

        public CleanupEmbeddingModelValidationResult Validate(CleanupEmbeddingOptions options) => new();

        public Task<CleanupEmbeddingModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupEmbeddingModelIdentity
            {
                ModelKey = "embedding-model",
                ModelHash = "embedding-hash",
                Dimensions = 2,
                RuntimeProvider = Options.RuntimeProvider.ToString()
            });
    }

    private sealed class StaticScoringMetadataService : ICleanupScoringModelMetadataService
    {
        public StaticScoringMetadataService(bool enabled, CleanupEmbeddingRuntimeProvider provider)
        {
            Options = new CleanupScoringOptions { Enabled = enabled, RuntimeProvider = provider };
        }

        public CleanupScoringOptions Options { get; }

        public CleanupScoringValidationResult Validate(CleanupScoringOptions options) => new();

        public Task<CleanupScoreModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupScoreModelIdentity
            {
                ModelKey = "score-model",
                ModelHash = "score-hash",
                ScoreName = "aesthetic",
                MinScore = 0,
                MaxScore = 1,
                RuntimeProvider = Options.RuntimeProvider.ToString()
            });
    }

    private sealed class FakeComfyBatchIndexer : ICleanupComfyBatchIndexingService
    {
        public int Calls { get; private set; }
        public List<int> EmbeddingImageIds { get; } = new();
        public List<int> ScoreImageIds { get; } = new();
        public CleanupComfyBatchIndexingResult Result { get; set; } = new();

        public Task<CleanupComfyBatchIndexingResult> IndexBatchAsync(
            IReadOnlyCollection<int> embeddingImageIds,
            IReadOnlyCollection<int> scoreImageIds,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            EmbeddingImageIds.AddRange(embeddingImageIds);
            ScoreImageIds.AddRange(scoreImageIds);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeEmbeddingIndexer : ICleanupEmbeddingIndexingService
    {
        public List<int> ImageIds { get; } = new();

        public Task<CleanupImageEmbedding> IndexImageEmbeddingAsync(int imageId, CancellationToken cancellationToken = default)
        {
            ImageIds.Add(imageId);
            return Task.FromResult(new CleanupImageEmbedding
            {
                ImageId = imageId,
                ModelKey = "embedding-model",
                ModelHash = "embedding-hash",
                RuntimeProvider = "CUDA",
                Dimensions = 2,
                Status = CleanupEmbeddingStatus.Indexed
            });
        }
    }

    private sealed class FakeScoreIndexer : ICleanupScoreIndexingService
    {
        public List<int> ImageIds { get; } = new();

        public Task<CleanupImageScore> IndexImageScoreAsync(int imageId, CancellationToken cancellationToken = default)
        {
            ImageIds.Add(imageId);
            return Task.FromResult(new CleanupImageScore
            {
                ImageId = imageId,
                ModelKey = "score-model",
                ModelHash = "score-hash",
                ScoreName = "aesthetic",
                RuntimeProvider = "CUDA",
                Score = 0.8,
                Status = CleanupScoreStatus.Indexed
            });
        }
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