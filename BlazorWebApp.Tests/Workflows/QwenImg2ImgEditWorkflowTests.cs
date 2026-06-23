using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the QwenImg2ImgEditWorkflow C# implementation.
/// </summary>
public class QwenImg2ImgEditWorkflowTests
{
    private readonly QwenImg2ImgEditWorkflow _workflow = new();

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Img2Img (Edit)");
    }

    [Fact]
    public void Metadata_ShouldHaveQwenBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Qwen);
    }

    [Fact]
    public void Metadata_ShouldHaveImg2ImgMode()
    {
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Img);
    }

    [Fact]
    public void Metadata_ShouldHaveDeterministicId()
    {
        var expected = WorkflowMetadata.GenerateDeterministicId(
            Data.Enums.ModelBase.Qwen, ModeType.Img2Img, "Img2Img (Edit)");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new QwenImg2ImgEditWorkflow();
        _workflow.Metadata.Id.Should().Be(other.Metadata.Id);
    }

    [Fact]
    public void Metadata_ShouldHaveThreeAssets()
    {
        _workflow.Metadata.Assets.Should().HaveCount(3);
    }

    [Fact]
    public void Metadata_Assets_ShouldHaveCorrectTypes()
    {
        var assets = _workflow.Metadata.Assets.ToList();
        assets.Should().Contain(a => a.Parameter == "Model" && a.Type == BlazorWebApp.Workflows.Models.AssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Vae);
    }

    [Fact]
    public void Metadata_ShouldHaveSourceImage()
    {
        var sources = _workflow.Metadata.Sources.ToList();
        sources.Should().HaveCount(1);
        sources[0].Id.Should().Be("source_image");
        sources[0].Type.Should().Be(SourceType.Image);
        sources[0].Required.Should().BeTrue();
    }

    [Fact]
    public void Metadata_ModelDefault_ShouldBeEditModel()
    {
        var modelAsset = _workflow.Metadata.Assets.First(a => a.Parameter == "Model");
        modelAsset.DefaultValue.Should().Be("qwen_image_edit_2509_fp8_e4m3fn.safetensors");
    }

    #endregion

    #region GetFragments Tests

    [Fact]
    public void GetFragments_ShouldReturnTwoFragments()
    {
        _workflow.GetFragments().Should().HaveCount(2);
    }

    [Fact]
    public void GetFragments_ShouldIncludeExpectedFragments()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
    }

    #endregion

    #region Build Tests - Core Pipeline

    [Fact]
    public void Build_WithMinimalParameters_ShouldGenerateValidWorkflow()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);

        workflow.Should().NotBeNull();
        workflow.Json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_ShouldGenerateValidJson()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);

        var action = () => JsonDocument.Parse(workflow.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldContainImageLoaderNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("image_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("LoadImage");
        node.GetProperty("inputs").GetProperty("image").GetString().Should().Be("source.png");
    }

    [Fact]
    public void Build_ShouldContainImageScaleNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("image_scale", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ImageScaleToTotalPixels");
    }

    [Fact]
    public void Build_ShouldContainLoadQwenEditNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("lora_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("model_sampling", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("cfg_norm", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_LoraLoader_ShouldUseCorrectClassType()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("lora_loader").GetProperty("class_type")
            .GetString().Should().Be("LoraLoaderModelOnly");
    }

    [Fact]
    public void Build_ShouldContainVaeEncodeNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("vae_encoder", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("VAEEncode");
    }

    [Fact]
    public void Build_ShouldContainEncodeEditNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("encode_positive", out var pos).Should().BeTrue();
        json.RootElement.TryGetProperty("encode_negative", out var neg).Should().BeTrue();
        pos.GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
        neg.GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
    }

    [Fact]
    public void Build_EncodeEdit_ShouldReferenceImageInput()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("encode_positive").GetProperty("inputs");
        inputs.GetProperty("image1")[0].GetString().Should().Be("image_scale");
    }

    [Fact]
    public void Build_ShouldContainStandardSamplerNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("sampler_main", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("KSampler");
    }

    [Fact]
    public void Build_ShouldContainVaeDecodeAndSaveNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("vae_decoder", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("save", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldNotContainEmptyLatentNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("empty_latent", out _).Should().BeFalse();
    }

    #endregion

    #region Build Tests - Pipeline Chain

    [Fact]
    public void Build_ShouldHaveCorrectPipelineChain()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // image_loader -> image_scale
        json.RootElement.GetProperty("image_scale").GetProperty("inputs")
            .GetProperty("image")[0].GetString().Should().Be("image_loader");

        // image_scale -> vae_encoder (pixels)
        json.RootElement.GetProperty("vae_encoder").GetProperty("inputs")
            .GetProperty("pixels")[0].GetString().Should().Be("image_scale");

        // image_scale -> encode_positive/negative (image1)
        json.RootElement.GetProperty("encode_positive").GetProperty("inputs")
            .GetProperty("image1")[0].GetString().Should().Be("image_scale");

        // unet -> lora -> model_sampling -> cfg_norm -> sampler (model chain)
        json.RootElement.GetProperty("lora_loader").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("unet_loader");
        json.RootElement.GetProperty("model_sampling").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("lora_loader");
        json.RootElement.GetProperty("cfg_norm").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("model_sampling");
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("cfg_norm");

        // vae_encoder -> sampler (latent)
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("latent_image")[0].GetString().Should().Be("vae_encoder");

        // sampler -> vae_decoder
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs")
            .GetProperty("samples")[0].GetString().Should().Be("sampler_main");
    }

    #endregion

    #region Build Tests - Source Image

    [Fact]
    public void Build_WithSourceImage_ShouldUseFilePath()
    {
        var parameters = CreateMinimalParameters();
        parameters.Sources["source_image"] = new SourceAsset
        {
            FilePath = "/images/photo.jpg",
            Filename = "photo.jpg"
        };

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("image_loader").GetProperty("inputs")
            .GetProperty("image").GetString().Should().Be("/images/photo.jpg");
    }

    [Fact]
    public void Build_WithSourceImage_NoFilePath_ShouldFallbackToFilename()
    {
        var parameters = CreateMinimalParameters();
        parameters.Sources["source_image"] = new SourceAsset
        {
            Filename = "uploaded.png"
        };

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("image_loader").GetProperty("inputs")
            .GetProperty("image").GetString().Should().Be("uploaded.png");
    }

    #endregion

    #region Build Tests - Custom Parameters

    [Fact]
    public void Build_WithCustomAssets_ShouldUseProvidedValues()
    {
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom_edit_model.safetensors";

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("unet_loader").GetProperty("inputs")
            .GetProperty("unet_name").GetString().Should().Be("custom_edit_model.safetensors");
    }

    [Fact]
    public void Build_WithCustomDenoise_ShouldUseProvidedValue()
    {
        var parameters = CreateMinimalParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 0.8);

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("denoise").GetDouble().Should().Be(0.8);
    }

    #endregion

    #region Helper Methods

    private static GenerationParameters CreateMinimalParameters()
    {
        var parameters = new GenerationParameters();

        // Source image
        parameters.Sources["source_image"] = new SourceAsset
        {
            FilePath = "source.png",
            Filename = "source.png"
        };

        // Prompts
        var promptsFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "make it colorful");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "");

        // Sampler
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "euler");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "simple");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 4);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);

        // Assets
        parameters.Assets["Model"] = "qwen_image_edit_2509_fp8_e4m3fn.safetensors";
        parameters.Assets["Clip"] = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        parameters.Assets["Vae"] = "qwen_image_vae.safetensors";

        return parameters;
    }

    #endregion
}
