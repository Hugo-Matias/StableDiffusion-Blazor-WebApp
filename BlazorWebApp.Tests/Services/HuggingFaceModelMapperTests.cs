using BlazorWebApp.Data.Dtos.HuggingFace;
using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class HuggingFaceModelMapperTests
{
    // -----------------------------------------------------------------------
    // Type inference
    // -----------------------------------------------------------------------

    [Fact]
    public void InferHints_WithLoraTag_ReturnsLoraType()
    {
        var model = MakeModel(tags: ["lora", "text-to-image"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.TypeHint.Should().Be("LORA");
    }

    [Fact]
    public void InferHints_WithVaeTag_ReturnsVaeType()
    {
        var model = MakeModel(tags: ["vae", "diffusers"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.TypeHint.Should().Be("VAE");
    }

    [Fact]
    public void InferHints_WithControlnetTag_ReturnsControlnetType()
    {
        var model = MakeModel(tags: ["controlnet"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.TypeHint.Should().Be("Controlnet");
    }

    [Fact]
    public void InferHints_WithTextToImagePipelineAndNoSpecificTag_ReturnsCheckpoint()
    {
        var model = MakeModel(pipelineTag: "text-to-image", tags: ["diffusers"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.TypeHint.Should().Be("Checkpoint");
    }

    [Fact]
    public void InferHints_WithGgufFile_ReturnsCheckpointType()
    {
        var model = MakeModel(tags: []);
        var hints = HuggingFaceModelMapper.InferHints(model, selectedFilePath: "model.gguf");
        hints.TypeHint.Should().Be("Checkpoint");
    }

    [Fact]
    public void InferHints_WithNoUsefulSignal_ReturnsNullType()
    {
        var model = MakeModel(tags: [], pipelineTag: null);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.TypeHint.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // SubType inference
    // -----------------------------------------------------------------------

    [Fact]
    public void InferHints_WithSafetensorsFile_ReturnsSafeTensorSubtype()
    {
        var model = MakeModel(tags: []);
        var hints = HuggingFaceModelMapper.InferHints(model, selectedFilePath: "model.safetensors");
        hints.SubTypeHint.Should().Be("SafeTensor");
    }

    [Fact]
    public void InferHints_WithGgufFile_ReturnsGgufSubtype()
    {
        var model = MakeModel(tags: []);
        var hints = HuggingFaceModelMapper.InferHints(model, selectedFilePath: "model.gguf");
        hints.SubTypeHint.Should().Be("GGUF");
    }

    [Fact]
    public void InferHints_WithNoFile_ReturnsNullSubtype()
    {
        var model = MakeModel(tags: []);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.SubTypeHint.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // BaseModel inference
    // -----------------------------------------------------------------------

    [Fact]
    public void InferHints_WithFluxTag_ReturnsFluxBaseModel()
    {
        var model = MakeModel(tags: ["flux"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.BaseModelHint.Should().Be("Flux");
    }

    [Fact]
    public void InferHints_WithSdxlTag_ReturnsSdxlBaseModel()
    {
        var model = MakeModel(tags: ["sdxl"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.BaseModelHint.Should().Be("SDXL 1.0");
    }

    [Fact]
    public void InferHints_WithFluxInRepoId_ReturnsFluxBaseModel()
    {
        var model = MakeModel(id: "someone/flux-lora-model", tags: []);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.BaseModelHint.Should().Be("Flux");
    }

    [Fact]
    public void InferHints_WithNoBaseModelSignal_ReturnsNullBaseModel()
    {
        var model = MakeModel(tags: [], id: "author/unknown-model");
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.BaseModelHint.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Confidence
    // -----------------------------------------------------------------------

    [Fact]
    public void InferHints_WhenBothTypeAndBaseModelPresent_IsConfident()
    {
        var model = MakeModel(tags: ["lora", "sdxl"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.IsConfident.Should().BeTrue();
    }

    [Fact]
    public void InferHints_WhenBaseModelMissing_IsNotConfident()
    {
        var model = MakeModel(tags: ["lora"]);
        var hints = HuggingFaceModelMapper.InferHints(model);
        hints.IsConfident.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // BuildResource
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildResource_PopulatesHuggingFaceFields()
    {
        var model = MakeModel(id: "author/my-model", tags: ["lora"]);
        var resource = HuggingFaceModelMapper.BuildResource(
            model,
            localFilename: "my-model.safetensors",
            repoFilePath: "my-model.safetensors",
            revision: "main",
            sizeKb: 1024.0,
            sha: "abc123");

        resource.HuggingFaceModelId.Should().Be("author/my-model");
        resource.HuggingFaceRevision.Should().Be("main");
        resource.HuggingFaceFilePath.Should().Be("my-model.safetensors");
        resource.HuggingFaceSha.Should().Be("abc123");
        resource.Filename.Should().Be("my-model.safetensors");
        resource.Author.Should().Be("author");
        resource.SizeKb.Should().Be(1024.0);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static HuggingFaceModelSummaryDto MakeModel(
        string id = "author/model",
        List<string>? tags = null,
        string? pipelineTag = null)
    {
        return new HuggingFaceModelSummaryDto
        {
            Id = id,
            PipelineTag = pipelineTag,
            Tags = tags ?? [],
        };
    }
}
