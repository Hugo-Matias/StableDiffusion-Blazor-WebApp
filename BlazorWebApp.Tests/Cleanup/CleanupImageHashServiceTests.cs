using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using ImageMagick;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupImageHashServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "cleanup-hash-tests-" + Guid.NewGuid());
    private readonly CleanupImageHashService _service = new();

    public CleanupImageHashServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ComputeExactHashAsync_ReturnsStableSha256()
    {
        var path = Path.Combine(_tempDirectory, "sample.bin");
        await File.WriteAllTextAsync(path, "cleanup-index");

        var first = await _service.ComputeExactHashAsync(path);
        var second = await _service.ComputeExactHashAsync(path);

        first.Should().Be(second);
        first.Should().HaveLength(64);
    }

    [Fact]
    public void ComputePerceptualHash_ReturnsStableSixteenCharacterHash()
    {
        var path = Path.Combine(_tempDirectory, "sample.png");
        using (var image = new MagickImage(MagickColors.Red, 32, 32))
        {
            image.Write(path);
        }

        var first = _service.ComputePerceptualHash(path);
        var second = _service.ComputePerceptualHash(path);

        first.Should().Be(second);
        first.Should().MatchRegex("^[0-9a-f]{16}$");
    }
}