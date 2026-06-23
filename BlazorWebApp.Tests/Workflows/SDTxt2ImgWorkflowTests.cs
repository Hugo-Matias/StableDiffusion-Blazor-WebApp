using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.SD;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the SDTxt2ImgWorkflow C# implementation.
/// Verifies the workflow generates valid ComfyUI JSON for StableDiffusion Txt2Img.
/// </summary>
public class SDTxt2ImgWorkflowTests
{
    private readonly SDTxt2ImgWorkflow _workflow;

    public SDTxt2ImgWorkflowTests()
    {
        _workflow = new SDTxt2ImgWorkflow();
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Txt2Img");
    }

    [Fact]
    public void Metadata_ShouldHaveStableDiffusionBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.StableDiffusion);
    }

    [Fact]
    public void Metadata_ShouldHaveTxt2ImgMode()
    {
        _workflow.Metadata.Mode.Should().Be(ModeType.Txt2Img);
    }

    [Fact]
    public void Metadata_ShouldHaveDeterministicId()
    {
        var expected = WorkflowMetadata.GenerateDeterministicId(
            Data.Enums.ModelBase.StableDiffusion, ModeType.Txt2Img, "Txt2Img");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new SDTxt2ImgWorkflow();
        _workflow.Metadata.Id.Should().Be(other.Metadata.Id);
    }

    [Fact]
    public void Metadata_ShouldHaveOneAsset()
    {
        _workflow.Metadata.Assets.Should().HaveCount(1);
    }

    [Fact]
    public void Metadata_Asset_ShouldBeCheckpointModel()
    {
        var asset = _workflow.Metadata.Assets.First();
        asset.Parameter.Should().Be("Model");
        asset.Type.Should().Be(BlazorWebApp.Workflows.Models.AssetType.CheckpointModel);
    }

    [Fact]
    public void Metadata_ShouldHaveNoSources()
    {
        _workflow.Metadata.Sources.Should().BeEmpty();
    }

    #endregion

    #region GetFragments Tests

    [Fact]
    public void GetFragments_ShouldReturnExpectedCount()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().HaveCount(5);
    }

    [Fact]
    public void GetFragments_ShouldIncludeLatentFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "latent");
    }

    [Fact]
    public void GetFragments_ShouldIncludeSamplerFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
    }

    [Fact]
    public void GetFragments_ShouldIncludeUpscaleFragment()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "upscale");
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
    public void Build_ShouldContainCheckpointLoaderNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("model_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("CheckpointLoaderSimple");
        node.GetProperty("inputs").GetProperty("ckpt_name").GetString()
            .Should().Be("Base/v1-5-pruned-emaonly.safetensors");
    }

    [Fact]
    public void Build_ShouldContainPromptPrimitives()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("text_prompt", out var positive).Should().BeTrue();
        positive.GetProperty("class_type").GetString().Should().Be("PrimitiveStringMultiline");
        positive.GetProperty("inputs").GetProperty("value").GetString().Should().Be("a beautiful landscape");

        json.RootElement.TryGetProperty("text_negative", out var negative).Should().BeTrue();
        negative.GetProperty("inputs").GetProperty("value").GetString().Should().Be("ugly");
    }

    [Fact]
    public void Build_ShouldContainLoraLoaders()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("lora_positive", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("PCLazyLoraLoader");

        json.RootElement.TryGetProperty("lora_negative", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainTextEncoders()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("encode_positive", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("PCLazyTextEncode");

        json.RootElement.TryGetProperty("encode_negative", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainEmptyLatentNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("empty_latent", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("EmptyLatentImage");
    }

    [Fact]
    public void Build_EmptyLatent_ShouldUseSDLatentClass()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // SD uses EmptyLatentImage, not EmptySD3LatentImage
        json.RootElement.GetProperty("empty_latent")
            .GetProperty("class_type").GetString().Should().Be("EmptyLatentImage");
    }

    [Fact]
    public void Build_EmptyLatent_ShouldUseSDDefaultResolution()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("empty_latent").GetProperty("inputs");
        inputs.GetProperty("width").GetInt32().Should().Be(512);
        inputs.GetProperty("height").GetInt32().Should().Be(768);
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

    #endregion

    #region Build Tests - Pipeline Chain

    [Fact]
    public void Build_LoraPositive_ShouldReferenceCheckpointLoader()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("lora_positive").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("model_loader");
        inputs.GetProperty("clip")[0].GetString().Should().Be("model_loader");
        inputs.GetProperty("clip")[1].GetInt32().Should().Be(1);
    }

    [Fact]
    public void Build_Sampler_ShouldReferenceLoraAndLatent()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("lora_positive");
        inputs.GetProperty("positive")[0].GetString().Should().Be("encode_positive");
        inputs.GetProperty("negative")[0].GetString().Should().Be("encode_negative");
        inputs.GetProperty("latent_image")[0].GetString().Should().Be("empty_latent");
    }

    [Fact]
    public void Build_VaeDecode_ShouldReferenceSamplerOutput()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("vae_decoder").GetProperty("inputs");
        inputs.GetProperty("samples")[0].GetString().Should().Be("sampler_main");
    }

    [Fact]
    public void Build_VaeDecode_ShouldReferenceCheckpointVae()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("vae_decoder").GetProperty("inputs");
        // VAE output comes from checkpoint loader slot 2
        inputs.GetProperty("vae")[0].GetString().Should().Be("model_loader");
        inputs.GetProperty("vae")[1].GetInt32().Should().Be(2);
    }

    #endregion

    #region Build Tests - Sampler Defaults

    [Fact]
    public void Build_Sampler_ShouldUseSDDefaults()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("cfg").GetDouble().Should().Be(5.5);
        inputs.GetProperty("steps").GetInt32().Should().Be(20);
    }

    #endregion

    #region Build Tests - Optional Enhancements

    [Fact]
    public void Build_WithoutUpscale_ShouldNotContainUpscaleNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("upscale_model_loader", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithUpscaleActive_ShouldContainUpscaleNodes()
    {
        var parameters = CreateParametersWithUpscale();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("upscale_model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_with_model", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_unsample", out _).Should().BeTrue();
    }

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

        // Detailer uses LoadCheckpoint (not LoadDiffusion) with detailer_ scope
        json.RootElement.TryGetProperty("detailer_model_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("CheckpointLoaderSimple");

        json.RootElement.TryGetProperty("detailer", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithDetailerActive_ShouldHaveScopedLoraLoaders()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_lora_positive", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_lora_negative", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_encode_positive", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_encode_negative", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutDetailer_ShouldNotContainDetailerNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_model_loader", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("detailer", out _).Should().BeFalse();
    }

    #endregion

    #region Build Tests - Custom Assets

    [Fact]
    public void Build_WithCustomCheckpoint_ShouldUseProvidedValue()
    {
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom-checkpoint.safetensors";

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("model_loader").GetProperty("inputs")
            .GetProperty("ckpt_name").GetString().Should().Be("custom-checkpoint.safetensors");
    }

    #endregion

    #region Helper Methods

    private static GenerationParameters CreateMinimalParameters()
    {
        var parameters = new GenerationParameters();

        // Prompts
        var promptsFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "a beautiful landscape");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "ugly");

        // Latent
        var latentFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
        latentFragment.SetValue(FragmentKeys.Params.Width, 512);
        latentFragment.SetValue(FragmentKeys.Params.Height, 768);
        latentFragment.SetValue(FragmentKeys.Params.BatchSize, 1);

        // Sampler
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "multistep/res_2m");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "beta");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 5.5);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Eta, 0.5);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);

        // Assets
        parameters.Assets["Model"] = "Base/v1-5-pruned-emaonly.safetensors";

        return parameters;
    }

    private static GenerationParameters CreateParametersWithUpscale()
    {
        var parameters = CreateMinimalParameters();

        var upscaleFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Upscale);
        upscaleFragment.IsActive = true;
        upscaleFragment.SetValue("upscale_model", "4x-UltraSharpV2.safetensors");
        upscaleFragment.SetValue("upscale_width", 1024);
        upscaleFragment.SetValue("upscale_height", 1536);
        upscaleFragment.SetValue("upscale_steps", 20);
        upscaleFragment.SetValue("upscale_denoise", 1.0);

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
