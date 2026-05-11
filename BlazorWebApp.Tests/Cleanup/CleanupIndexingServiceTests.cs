using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupIndexingServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly CleanupRepository _repository;
    private readonly CleanupIndexingService _service;
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "cleanup-indexing-tests-" + Guid.NewGuid());

    public CleanupIndexingServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _factory = new TestDbContextFactory();
        _repository = new CleanupRepository(_factory);
        _service = new CleanupIndexingService(
            _factory,
            _repository,
            new CleanupPromptIndexService(),
            new CleanupImageHashService(),
            NullLogger<CleanupIndexingService>.Instance);
    }

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task IndexImagesAsync_IndexesImageAndSkipsUnchangedRerun()
    {
        var imagePath = CreateImage("indexed.png", MagickColors.Blue);
        await SeedImageAsync(new Image
        {
            Id = 1,
            Path = imagePath,
            ProjectId = 7,
            ModeId = 2,
            Prompt = "((Portrait:1.1)), blue_hair",
            Scheduler = "normal"
        });

        var first = await _service.IndexImagesAsync(new CleanupIndexingOptions { BatchSize = 10 });
        var second = await _service.IndexImagesAsync(new CleanupIndexingOptions { BatchSize = 10 });

        first.Indexed.Should().Be(1);
        first.Skipped.Should().Be(0);
        second.Indexed.Should().Be(0);
        second.Skipped.Should().Be(1);

        var index = await _repository.GetImageIndexAsync(1);
        index.Should().NotBeNull();
        index!.Status.Should().Be(CleanupIndexStatus.Indexed);
        index.FileExists.Should().BeTrue();
        index.FileSizeBytes.Should().BeGreaterThan(0);
        index.ExactHash.Should().HaveLength(64);
        index.PerceptualHash.Should().HaveLength(16);
        index.PromptNormalized.Should().Be("portrait, blue hair");
    }

    [Fact]
    public async Task IndexImagesAsync_RecordsMissingFilesWithoutAbortingBatch()
    {
        var imagePath = CreateImage("present.png", MagickColors.Green);
        await SeedImageAsync(new Image
        {
            Id = 1,
            Path = imagePath,
            ProjectId = 1,
            ModeId = 1,
            Prompt = "present",
            Scheduler = "normal"
        });
        await SeedImageAsync(new Image
        {
            Id = 2,
            Path = Path.Combine(_tempDirectory, "missing.png"),
            ProjectId = 1,
            ModeId = 1,
            Prompt = "missing",
            Scheduler = "normal"
        });

        var progress = new List<CleanupIndexingProgress>();
        var result = await _service.IndexImagesAsync(
            new CleanupIndexingOptions { BatchSize = 1 },
            new CapturingProgress<CleanupIndexingProgress>(progress.Add));

        result.Indexed.Should().Be(1);
        result.MissingFiles.Should().Be(1);
        result.Failed.Should().Be(0);
        result.TotalCandidates.Should().Be(2);
        progress.Should().NotBeEmpty();
        progress[0].TotalCandidates.Should().Be(2);
        GetProcessedCount(progress[0]).Should().Be(0);
        progress[^1].TotalCandidates.Should().Be(2);
        GetProcessedCount(progress[^1]).Should().Be(2);

        var missing = await _repository.GetImageIndexAsync(2);
        missing.Should().NotBeNull();
        missing!.Status.Should().Be(CleanupIndexStatus.MissingFile);
        missing.FileExists.Should().BeFalse();
    }

    private string CreateImage(string fileName, MagickColor color)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        using var image = new MagickImage(color, 32, 32);
        image.Write(path);
        return path;
    }

    private async Task SeedImageAsync(Image image)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Images.Add(image);
        await context.SaveChangesAsync();
    }

    private static int GetProcessedCount(CleanupIndexingProgress progress)
    {
        return progress.Indexed + progress.Skipped + progress.MissingFiles + progress.Failed;
    }

    private sealed class CapturingProgress<T> : IProgress<T>
    {
        private readonly Action<T> _capture;

        public CapturingProgress(Action<T> capture)
        {
            _capture = capture;
        }

        public void Report(T value)
        {
            _capture(value);
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