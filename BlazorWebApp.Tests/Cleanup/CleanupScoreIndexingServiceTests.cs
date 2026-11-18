using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupScoreIndexingServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CleanupRepository _repository;

    public CleanupScoreIndexingServiceTests()
    {
        _repository = new CleanupRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task IndexImageScoreAsync_PersistsScore()
    {
        await SeedImageAsync(new Image
        {
            Id = 20,
            Path = "image.png",
            ProjectId = 1,
            ModeId = 1,
            Scheduler = "normal"
        });
        var service = CreateService(0.42);

        var score = await service.IndexImageScoreAsync(20);

        score.Status.Should().Be(CleanupScoreStatus.Indexed);
        score.ModelKey.Should().Be("aesthetic-v1");
        score.ScoreName.Should().Be("aesthetic");
        score.RuntimeProvider.Should().Be("CPU");
        score.Score.Should().Be(0.42);

        var loaded = await _repository.GetImageScoreAsync(20, "aesthetic-v1", "hash-a", "aesthetic");
        loaded.Should().NotBeNull();
        loaded!.Score.Should().Be(0.42);
    }

    [Fact]
    public async Task IndexImageScoreAsync_PersistsErrorStatusWhenProviderFails()
    {
        await SeedImageAsync(new Image
        {
            Id = 21,
            Path = "image.png",
            ProjectId = 1,
            ModeId = 1,
            Scheduler = "normal"
        });
        var service = CreateService(0, new InvalidOperationException("provider failed"));

        var score = await service.IndexImageScoreAsync(21);

        score.Status.Should().Be(CleanupScoreStatus.Error);
        score.ErrorMessage.Should().Contain("provider failed");
    }

    private CleanupScoreIndexingService CreateService(double score, Exception? exception = null)
        => new(
            _factory,
            _repository,
            new StaticScoringMetadataService(),
            new FakeImageScoringService(score, exception),
            NullLogger<CleanupScoreIndexingService>.Instance);

    private async Task SeedImageAsync(Image image)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Images.Add(image);
        await context.SaveChangesAsync();
    }

    private sealed class StaticScoringMetadataService : ICleanupScoringModelMetadataService
    {
        public CleanupScoringOptions Options { get; } = new();

        public CleanupScoringValidationResult Validate(CleanupScoringOptions options) => new();

        public Task<CleanupScoreModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupScoreModelIdentity
            {
                ModelKey = "aesthetic-v1",
                ModelHash = "hash-a",
                ScoreName = "aesthetic",
                MinScore = 0,
                MaxScore = 1,
                RuntimeProvider = "CPU"
            });
    }

    private sealed class FakeImageScoringService : IImageScoringService
    {
        private readonly double _score;
        private readonly Exception? _exception;

        public FakeImageScoringService(double score, Exception? exception)
        {
            _score = score;
            _exception = exception;
        }

        public Task<double> ScoreImageAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(_score);
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