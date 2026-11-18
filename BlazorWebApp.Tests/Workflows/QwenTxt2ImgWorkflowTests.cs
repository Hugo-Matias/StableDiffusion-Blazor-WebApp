using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the QwenTxt2ImgWorkflow C# implementation.
/// </summary>
public class QwenTxt2ImgWorkflowTests
{
    private readonly QwenTxt2ImgWorkflow _workflow = new();

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Txt2Img");
    }

    [Fact]
    public void Metadata_ShouldHaveQwenBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Qwen);
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
            Data.Enums.ModelBase.Qwen, ModeType.Txt2Img, "Txt2Img");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new QwenTxt2ImgWorkflow();
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
    public void Metadata_ShouldHaveNoSources()
    {
        _workflow.Metadata.Sources.Should().BeEmpty();
    }

    #endregion

    #region GetFragments Tests

    [Fact]
    public void GetFragments_ShouldReturnFiveFragments()
    {
        _workflow.GetFragments().Should().HaveCount(5);
    }

    [Fact]
    public void GetFragments_ShouldIncludeExpectedFragments()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
        fragments.Should().Contain(f => f.Metadata.Id == "latent");
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
        fragments.Should().Contain(f => f.Metadata.Id == "seed_vr2");
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
    public void Build_ShouldContainUnetLoaderNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("unet_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("UNETLoader");
    }

    [Fact]
    public void Build_ClipLoader_ShouldUseQwenImageType()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("clip_loader").GetProperty("inputs");
        inputs.GetProperty("type").GetString().Should().Be("qwen_image");
    }

    [Fact]
    public void Build_ShouldContainEmptyLatentSD3Node()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("empty_latent", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("EmptySD3LatentImage");
    }

    [Fact]
    public void Build_ShouldContainModelSamplingAuraFlowNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("model_sampler_auraflow", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ModelSamplingAuraFlow");
    }

    [Fact]
    public void Build_ModelSamplingAuraFlow_ShouldUseDefaultShift()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("model_sampler_auraflow").GetProperty("inputs")
            .GetProperty("shift").GetDouble().Should().Be(3.10);
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
    public void Build_Sampler_ShouldReferenceModelSamplingOutput()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("model_sampler_auraflow");
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

    #endregion

    #region Build Tests - Sampler References

    [Fact]
    public void Build_Sampler_ShouldReferenceLatentOutput()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("latent_image")[0].GetString().Should().Be("empty_latent");
    }

    #endregion

    #region Build Tests - Custom Parameters

    [Fact]
    public void Build_WithCustomAssets_ShouldUseProvidedValues()
    {
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom_qwen.safetensors";

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("unet_loader").GetProperty("inputs")
            .GetProperty("unet_name").GetString().Should().Be("custom_qwen.safetensors");
    }

    [Fact]
    public void Build_WithCustomModelShift_ShouldUseProvidedValue()
    {
        var parameters = CreateMinimalParameters();
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue("model_shift", 5.0);

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("model_sampler_auraflow").GetProperty("inputs")
            .GetProperty("shift").GetDouble().Should().Be(5.0);
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
    public void Build_WithDetailer_ShouldUseQwenImageClipType()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("detailer_clip_loader").GetProperty("inputs")
            .GetProperty("type").GetString().Should().Be("qwen_image");
    }

    [Fact]
    public void Build_WithoutDetailer_ShouldNotContainDetailerNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_unet_loader", out _).Should().BeFalse();
    }

    #endregion

    #region Build Tests - Pipeline Chain

    [Fact]
    public void Build_ShouldHaveCorrectPipelineChain()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // model_sampler_auraflow references lora_positive model output
        json.RootElement.GetProperty("model_sampler_auraflow").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("lora_positive");

        // sampler references model_sampler_auraflow (overwritten model_output)
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("model_sampler_auraflow");

        // vae_decoder references sampler (latent_output)
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs")
            .GetProperty("samples")[0].GetString().Should().Be("sampler_main");
    }

    #endregion

    #region Helper Methods

    private static GenerationParameters CreateMinimalParameters()
    {
        var parameters = new GenerationParameters();

        var promptsFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "a beautiful scene");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "");

        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "linear/euler");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "simple");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 4);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);
        samplerFragment.SetValue("model_shift", 3.10);

        var latentFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
        latentFragment.SetValue("width", 872);
        latentFragment.SetValue("height", 1248);
        latentFragment.SetValue("batch_size", 1);

        parameters.Assets["Model"] = "qwen_image_fp8_e4m3fn.safetensors";
        parameters.Assets["Clip"] = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        parameters.Assets["Vae"] = "qwen_image_vae.safetensors";

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
