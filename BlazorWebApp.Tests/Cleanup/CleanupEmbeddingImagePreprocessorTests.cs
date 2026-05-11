using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using ImageMagick;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupEmbeddingImagePreprocessorTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "cleanup-embedding-preprocessor-tests-" + Guid.NewGuid());
    private readonly CleanupEmbeddingImagePreprocessor _preprocessor = new();

    public CleanupEmbeddingImagePreprocessorTests()
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
    public void Preprocess_BuildsNchwTensorWithConfiguredNormalization()
    {
        var path = CreateImage("red.png", MagickColors.Red);
        var model = new CleanupEmbeddingModelOptions
        {
            InputWidth = 2,
            InputHeight = 2,
            InputLayout = "NCHW",
            Mean = new List<float> { 0f, 0f, 0f },
            StandardDeviation = new List<float> { 1f, 1f, 1f }
        };

        var tensor = _preprocessor.Preprocess(path, model);

        tensor.Dimensions.Should().Equal(1, 3, 2, 2);
        tensor.Values.Should().HaveCount(12);
        tensor.Values.Take(4).Should().OnlyContain(value => value == 1f);
        tensor.Values.Skip(4).Should().OnlyContain(value => value == 0f);
    }

    [Fact]
    public void Preprocess_BuildsNhwcTensorWhenConfigured()
    {
        var path = CreateImage("green.png", MagickColors.Green);
        var model = new CleanupEmbeddingModelOptions
        {
            InputWidth = 1,
            InputHeight = 1,
            InputLayout = "NHWC",
            Mean = new List<float> { 0f, 0f, 0f },
            StandardDeviation = new List<float> { 1f, 1f, 1f }
        };

        var tensor = _preprocessor.Preprocess(path, model);

        tensor.Dimensions.Should().Equal(1, 1, 1, 3);
        tensor.Values.Should().HaveCount(3);
        tensor.Values[0].Should().BeApproximately(0f, 0.0001f);
        tensor.Values[1].Should().BeApproximately(0.5019f, 0.001f);
        tensor.Values[2].Should().BeApproximately(0f, 0.0001f);
    }

    private string CreateImage(string fileName, MagickColor color)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        using var image = new MagickImage(color, 2, 2);
        image.Write(path);
        return path;
    }
}