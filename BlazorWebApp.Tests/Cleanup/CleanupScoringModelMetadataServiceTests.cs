using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupScoringModelMetadataServiceTests
{
    [Fact]
    public void Validate_ReturnsErrorsWhenEnabledConfigurationIsIncomplete()
    {
        var service = CreateService(new CleanupScoringOptions { Enabled = true });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Contains("ModelKey"));
        result.Errors.Should().Contain(error => error.Contains("ModelPath"));
        result.Errors.Should().Contain(error => error.Contains("InputName"));
        result.Errors.Should().Contain(error => error.Contains("OutputName"));
    }

    [Fact]
    public void Validate_AllowsComfyConfigurationWithoutLocalOnnxMetadata()
    {
        var service = CreateService(new CleanupScoringOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupScoreModelOptions
            {
                ModelKey = "blazor_quality_v1",
                ScoreName = "quality",
                MinScore = 0,
                MaxScore = 1
            }
        });

        var result = service.Validate(service.Options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetIdentityAsync_ComputesModelHash()
    {
        var modelPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(modelPath, new byte[] { 1, 2, 3 });
            var service = CreateService(new CleanupScoringOptions
            {
                Enabled = true,
                RuntimeProvider = CleanupEmbeddingRuntimeProvider.CPU,
                Model = new CleanupScoreModelOptions
                {
                    ModelKey = "aesthetic-v1",
                    ModelPath = modelPath,
                    ScoreName = "aesthetic",
                    InputName = "input",
                    OutputName = "score"
                }
            });

            var identity = await service.GetIdentityAsync();

            identity.ModelKey.Should().Be("aesthetic-v1");
            identity.ScoreName.Should().Be("aesthetic");
            identity.ModelHash.Should().NotBeNullOrWhiteSpace();
            identity.RuntimeProvider.Should().Be("CPU");
        }
        finally
        {
            File.Delete(modelPath);
        }
    }

    [Fact]
    public async Task GetIdentityAsync_UsesStableRemoteHashForComfy()
    {
        var service = CreateService(new CleanupScoringOptions
        {
            Enabled = true,
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupScoreModelOptions
            {
                ModelKey = "blazor_quality_v1",
                ScoreName = "quality",
                MinScore = 0,
                MaxScore = 1
            }
        });

        var identity = await service.GetIdentityAsync();

        identity.ModelKey.Should().Be("blazor_quality_v1");
        identity.ScoreName.Should().Be("quality");
        identity.ModelHash.Should().NotBeNullOrWhiteSpace();
        identity.RuntimeProvider.Should().Be("ComfyUI");
    }

    private static CleanupScoringModelMetadataService CreateService(CleanupScoringOptions options)
        => new(Options.Create(options));
}