using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.ZImage;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the ZImageImg2ImgWorkflow C# implementation.
/// Verifies the workflow generates valid ComfyUI JSON for Img2Img generation.
/// </summary>
public class ZImageImg2ImgWorkflowTests
{
    private readonly ZImageImg2ImgWorkflow _workflow;

    public ZImageImg2ImgWorkflowTests()
    {
        _workflow = new ZImageImg2ImgWorkflow();
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Img2Img");
    }

    [Fact]
    public void Metadata_ShouldHaveZImageBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.ZImage);
    }

    [Fact]
    public void Metadata_ShouldHaveImg2ImgMode()
    {
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Img);
    }

    [Fact]
    public void Metadata_ShouldHaveThreeAssets()
    {
        _workflow.Metadata.Assets.Should().HaveCount(3);
    }

    [Fact]
    public void Metadata_Assets_ShouldHaveCorrectParameters()
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
        sources[0].Type.Should().Be(BlazorWebApp.Workflows.Models.SourceType.Image);
        sources[0].Required.Should().BeTrue();
    }

    #endregion

    #region GetFragments Tests

    [Fact]
    public void GetFragments_ShouldReturnFourFragments()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().HaveCount(4);
    }

    [Fact]
    public void GetFragments_ShouldIncludePromptsFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
    }

    [Fact]
    public void GetFragments_ShouldIncludeSamplerFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
    }

    [Fact]
    public void GetFragments_ShouldIncludeSeedVR2Fragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "seed_vr2");
    }

    [Fact]
    public void GetFragments_ShouldIncludeDetailerFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "detailer");
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
    public void Build_ShouldContainUnetLoaderNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("unet_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("UNETLoader");
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
    public void Build_VaeEncode_ShouldReferenceScaledImage()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        inputs.GetProperty("pixels")[0].GetString().Should().Be("image_scale");
    }

    [Fact]
    public void Build_ShouldContainSamplerNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("sampler_main", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ClownsharKSampler_Beta");
    }

    [Fact]
    public void Build_Sampler_ShouldDefaultDenoiseBelowOne()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("denoise").GetDouble().Should().BeLessThan(1.0);
    }

    [Fact]
    public void Build_Sampler_ShouldReferenceVaeEncoderLatent()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("latent_image")[0].GetString().Should().Be("vae_encoder");
    }

    [Fact]
    public void Build_ShouldContainVaeDecodeNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("vae_decoder", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainSaveNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("save", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldNotContainEmptyLatentNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Img2Img uses VaeEncode, not EmptyLatent
        json.RootElement.TryGetProperty("empty_latent", out _).Should().BeFalse();
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

    #region Build Tests - Custom Assets

    [Fact]
    public void Build_WithCustomAssets_ShouldUseProvidedValues()
    {
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom_model.safetensors";

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("unet_loader").GetProperty("inputs")
            .GetProperty("unet_name").GetString().Should().Be("custom_model.safetensors");
    }

    [Fact]
    public void Build_WithCustomDenoise_ShouldUseProvidedValue()
    {
        var parameters = CreateMinimalParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 0.5);

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("denoise").GetDouble().Should().Be(0.5);
    }

    #endregion

    #region Build Tests - Optional Enhancements

    [Fact]
    public void Build_WithoutSeedVR2_ShouldNotContainSeedVR2Nodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("seedvr2_load_dit", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithSeedVR2Active_ShouldContainSeedVR2Nodes()
    {
        var parameters = CreateParametersWithSeedVR2();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("seedvr2_load_dit", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithDetailerActive_ShouldContainDetailerNodes()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutDetailer_ShouldNotContainDetailerNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_unet_loader", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("detailer", out _).Should().BeFalse();
    }

    #endregion

    #region Build Tests - Pipeline Chain Verification

    [Fact]
    public void Build_ShouldHaveCorrectPipelineChain()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // image_loader -> image_scale (via image ref)
        json.RootElement.GetProperty("image_scale").GetProperty("inputs")
            .GetProperty("image")[0].GetString().Should().Be("image_loader");

        // image_scale -> vae_encoder (via pixels ref, registered as image_input)
        json.RootElement.GetProperty("vae_encoder").GetProperty("inputs")
            .GetProperty("pixels")[0].GetString().Should().Be("image_scale");

        // vae_encoder -> sampler (via latent_image, registered as latent_output)
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("latent_image")[0].GetString().Should().Be("vae_encoder");

        // sampler -> vae_decoder (via samples, registered as latent_output overwrite)
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs")
            .GetProperty("samples")[0].GetString().Should().Be("sampler_main");
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
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "a beautiful landscape");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "");

        // Sampler
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "linear/euler");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "simple");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 9);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 0.75);
        samplerFragment.SetValue(FragmentKeys.Params.Eta, 0.5);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);

        // Assets
        parameters.Assets["Model"] = "z_image_turbo_bf16.safetensors";
        parameters.Assets["Clip"] = "qwen_3_4b.safetensors";
        parameters.Assets["Vae"] = "ae.safetensors";

        return parameters;
    }

    private static GenerationParameters CreateParametersWithSeedVR2()
    {
        var parameters = CreateMinimalParameters();

        var seedVr2Fragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.SeedVR2);
        seedVr2Fragment.IsActive = true;
        seedVr2Fragment.SetValue("seedvr2_model", "seedvr2_ema_7b-Q4_K_M.gguf");
        seedVr2Fragment.SetValue("seedvr2_vae_model", "ema_vae_fp16.safetensors");
        seedVr2Fragment.SetValue("seedvr2_seed", 42L);
        seedVr2Fragment.SetValue("seedvr2_resolution", 2048);

        return parameters;
    }

    private static GenerationParameters CreateParametersWithDetailer()
    {
        var parameters = CreateMinimalParameters();

        var detailerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Detailer);
        detailerFragment.IsActive = true;
        detailerFragment.SetValue("detailer_detection_model", "bbox/face_yolov8m.pt");
        detailerFragment.SetValue("detailer_sampler", "dpmpp_2m");
        detailerFragment.SetValue("detailer_scheduler", "beta");
        detailerFragment.SetValue("detailer_seed", 42L);
        detailerFragment.SetValue("detailer_steps", 20);
        detailerFragment.SetValue("detailer_cfg", 8.0);
        detailerFragment.SetValue("detailer_denoise", 0.65);

        return parameters;
    }

    #endregion
}
