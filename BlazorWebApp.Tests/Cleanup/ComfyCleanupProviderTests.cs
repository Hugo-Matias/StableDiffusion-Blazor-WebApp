using System.Text.Json;
using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Services;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace BlazorWebApp.Tests.Cleanup;

public class ComfyCleanupProviderTests : IDisposable
{
    private readonly string _imagePath = Path.Combine(Path.GetTempPath(), "comfy-cleanup-provider-" + Guid.NewGuid() + ".png");

    public ComfyCleanupProviderTests()
    {
        File.WriteAllBytes(_imagePath, new byte[] { 1, 2, 3 });
    }

    public void Dispose()
    {
        if (File.Exists(_imagePath))
        {
            File.Delete(_imagePath);
        }
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_PostsCleanupEmbeddingWorkflowAndParsesVector()
    {
        object? capturedPayload = null;
        var comfy = CreateComfyMock();
        comfy.Setup(service => service.PostTextPromptAsync(It.IsAny<object>(), "payload_cleanup_embedding.json", It.IsAny<Guid?>()))
            .Callback<object, string, Guid?>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync(new LLMResponse
            {
                Text = JsonSerializer.Serialize(new
                {
                    modelKey = "blazor_stats_v1_512",
                    dimensions = 16,
                    embedding = Enumerable.Range(1, 16).Select(value => value / 100f).ToArray()
                })
            });
        var service = new ComfyImageEmbeddingService(Microsoft.Extensions.Options.Options.Create(new CleanupEmbeddingOptions
        {
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupEmbeddingModelOptions
            {
                ModelKey = "blazor_stats_v1_512",
                Dimensions = 16
            }
        }), comfy.Object);

        var vector = await service.GenerateEmbeddingAsync(_imagePath);

        vector.Should().HaveCount(16);
        vector[0].Should().BeApproximately(0.01f, 0.0001f);
        vector[^1].Should().BeApproximately(0.16f, 0.0001f);
        JsonSerializer.Serialize(capturedPayload).Should().Contain("BlazorCleanupImageEmbedding");
    }

    [Fact]
    public async Task ScoreImageAsync_PostsCleanupScoreWorkflowAndParsesScore()
    {
        object? capturedPayload = null;
        var comfy = CreateComfyMock();
        comfy.Setup(service => service.PostTextPromptAsync(It.IsAny<object>(), "payload_cleanup_score.json", It.IsAny<Guid?>()))
            .Callback<object, string, Guid?>((payload, _, _) => capturedPayload = payload)
            .ReturnsAsync(new LLMResponse
            {
                Text = "{\"modelKey\":\"blazor_quality_v1\",\"scoreName\":\"quality\",\"score\":0.72,\"minScore\":0,\"maxScore\":1}"
            });
        var service = new ComfyImageScoringService(Microsoft.Extensions.Options.Options.Create(new CleanupScoringOptions
        {
            RuntimeProvider = CleanupEmbeddingRuntimeProvider.ComfyUI,
            Model = new CleanupScoreModelOptions
            {
                ModelKey = "blazor_quality_v1",
                ScoreName = "quality",
                MinScore = 0,
                MaxScore = 1
            }
        }), comfy.Object);

        var score = await service.ScoreImageAsync(_imagePath);

        score.Should().BeApproximately(0.72, 0.0001);
        JsonSerializer.Serialize(capturedPayload).Should().Contain("BlazorCleanupImageScore");
    }

    [Fact]
    public void CreateBatchPayload_AddsMultipleImageOutputsToOnePrompt()
    {
        var workflow = ComfyCleanupWorkflowFactory.CreateBatchPayload(
            new Dictionary<int, string>
            {
                [10] = "first.png",
                [20] = "second.png"
            },
            new HashSet<int> { 10, 20 },
            new HashSet<int> { 10 },
            new CleanupEmbeddingOptions
            {
                Model = new CleanupEmbeddingModelOptions
                {
                    ModelKey = "blazor_stats_v1_512",
                    Dimensions = 512
                }
            },
            new CleanupScoringOptions
            {
                Model = new CleanupScoreModelOptions
                {
                    ModelKey = "blazor_quality_v1",
                    ScoreName = "quality",
                    MinScore = 0,
                    MaxScore = 1
                }
            });

        var json = JsonSerializer.Serialize(workflow.Payload);

        workflow.Outputs.Should().HaveCount(3);
        workflow.Outputs.Values.Count(output => output.Signal == ComfyCleanupBatchSignal.Embedding).Should().Be(2);
        workflow.Outputs.Values.Count(output => output.Signal == ComfyCleanupBatchSignal.Score).Should().Be(1);
        workflow.Outputs.Values.Select(output => output.ImageId).Should().Contain(new[] { 10, 20 });
        json.Should().Contain("first.png");
        json.Should().Contain("second.png");
        json.Should().Contain("BlazorCleanupImageEmbedding");
        json.Should().Contain("BlazorCleanupImageScore");
    }

    private static Mock<IComfyUIService> CreateComfyMock()
    {
        var comfy = new Mock<IComfyUIService>();
        comfy.Setup(service => service.UploadStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .ReturnsAsync("uploaded.png");
        return comfy;
    }
}