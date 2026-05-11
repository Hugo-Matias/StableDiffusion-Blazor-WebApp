using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupEmbeddingIndexingServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly CleanupRepository _repository;
    private readonly CleanupEmbeddingVectorCodec _codec = new();

    public CleanupEmbeddingIndexingServiceTests()
    {
        _repository = new CleanupRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task IndexImageEmbeddingAsync_PersistsSerializedNormalizedVector()
    {
        await SeedImageAsync(new Image
        {
            Id = 10,
            Path = "image.png",
            ProjectId = 1,
            ModeId = 1,
            Scheduler = "normal"
        });
        var service = CreateService(new[] { 3f, 4f });

        var embedding = await service.IndexImageEmbeddingAsync(10);

        embedding.Status.Should().Be(CleanupEmbeddingStatus.Indexed);
        embedding.ModelKey.Should().Be("test-model");
        embedding.RuntimeProvider.Should().Be("CPU");
        embedding.Dimensions.Should().Be(2);

        var loaded = await _repository.GetImageEmbeddingAsync(10, "test-model", "hash-a");
        loaded.Should().NotBeNull();
        var vector = _codec.Deserialize(loaded!.Vector, loaded.Dimensions);
        vector[0].Should().BeApproximately(0.6f, 0.0001f);
        vector[1].Should().BeApproximately(0.8f, 0.0001f);
    }

    [Fact]
    public async Task IndexImageEmbeddingAsync_PersistsErrorStatusWhenProviderFails()
    {
        await SeedImageAsync(new Image
        {
            Id = 11,
            Path = "image.png",
            ProjectId = 1,
            ModeId = 1,
            Scheduler = "normal"
        });
        var service = CreateService(Array.Empty<float>(), new InvalidOperationException("provider failed"));

        var embedding = await service.IndexImageEmbeddingAsync(11);

        embedding.Status.Should().Be(CleanupEmbeddingStatus.Error);
        embedding.ErrorMessage.Should().Contain("provider failed");
    }

    private CleanupEmbeddingIndexingService CreateService(float[] vector, Exception? exception = null)
        => new(
            _factory,
            _repository,
            new StaticMetadataService(),
            new FakeImageEmbeddingService(vector, exception),
            _codec,
            NullLogger<CleanupEmbeddingIndexingService>.Instance);

    private async Task SeedImageAsync(Image image)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Images.Add(image);
        await context.SaveChangesAsync();
    }

    private sealed class StaticMetadataService : ICleanupEmbeddingModelMetadataService
    {
        public CleanupEmbeddingOptions Options { get; } = new();

        public CleanupEmbeddingModelValidationResult Validate(CleanupEmbeddingOptions options) => new();

        public Task<CleanupEmbeddingModelIdentity> GetIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupEmbeddingModelIdentity
            {
                ModelKey = "test-model",
                ModelHash = "hash-a",
                Dimensions = 2,
                RuntimeProvider = "CPU"
            });
    }

    private sealed class FakeImageEmbeddingService : IImageEmbeddingService
    {
        private readonly float[] _vector;
        private readonly Exception? _exception;

        public FakeImageEmbeddingService(float[] vector, Exception? exception)
        {
            _vector = vector;
            _exception = exception;
        }

        public Task<float[]> GenerateEmbeddingAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(_vector);
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