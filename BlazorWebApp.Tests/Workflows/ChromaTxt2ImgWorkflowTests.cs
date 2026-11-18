using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Chroma;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the ChromaTxt2ImgWorkflow C# implementation.
/// </summary>
public class ChromaTxt2ImgWorkflowTests
{
    private readonly ChromaTxt2ImgWorkflow _workflow = new();

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Txt2Img");
    }

    [Fact]
    public void Metadata_ShouldHaveChromaBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Chroma);
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
            Data.Enums.ModelBase.Chroma, ModeType.Txt2Img, "Txt2Img");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new ChromaTxt2ImgWorkflow();
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
        assets.Should().Contain(a => a.Parameter == "Clip1" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "VAE" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Vae);
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
        fragments.Should().Contain(f => f.Metadata.Id == "upscale");
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
        node.GetProperty("inputs").GetProperty("unet_name").GetString().Should().Be("Chroma1-HD.safetensors");
    }

    [Fact]
    public void Build_ClipLoader_ShouldUseChromaType()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("clip_loader").GetProperty("inputs");
        inputs.GetProperty("type").GetString().Should().Be("chroma");
    }

    [Fact]
    public void Build_ShouldContainT5TokenizerOptionsNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("t5_tokenizer", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("T5TokenizerOptions");
        node.GetProperty("inputs").GetProperty("min_padding").GetInt32().Should().Be(1);
        node.GetProperty("inputs").GetProperty("min_length").GetInt32().Should().Be(0);
    }

    [Fact]
    public void Build_T5Tokenizer_ShouldReferenceClipLoader()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("t5_tokenizer").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("clip_loader");
    }

    [Fact]
    public void Build_LoraLoaders_ShouldReferenceT5TokenizerInsteadOfClipLoader()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // LoRA loaders should reference t5_tokenizer (not clip_loader directly)
        json.RootElement.GetProperty("lora_positive").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("t5_tokenizer");
        json.RootElement.GetProperty("lora_negative").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("t5_tokenizer");
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
    public void Build_ShouldContainSamplerNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("sampler_main", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ClownsharKSampler_Beta");
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

    #region Build Tests - Pipeline Chain

    [Fact]
    public void Build_ShouldHaveCorrectPipelineChain()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // t5_tokenizer -> clip_loader
        json.RootElement.GetProperty("t5_tokenizer").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("clip_loader");

        // lora_positive -> t5_tokenizer (clip ref)
        json.RootElement.GetProperty("lora_positive").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("t5_tokenizer");

        // sampler -> lora_positive (model ref, via model_output)
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("lora_positive");

        // vae_decoder -> sampler (latent_output)
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs")
            .GetProperty("samples")[0].GetString().Should().Be("sampler_main");
    }

    #endregion

    #region Build Tests - Custom Parameters

    [Fact]
    public void Build_WithCustomAssets_ShouldUseProvidedValues()
    {
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom_chroma.safetensors";

        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("unet_loader").GetProperty("inputs")
            .GetProperty("unet_name").GetString().Should().Be("custom_chroma.safetensors");
    }

    #endregion

    #region Build Tests - Upscale

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
        json.RootElement.TryGetProperty("upscale_unsample", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_resample_1", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_resample_2", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithUpscale_ShouldUseCfg1()
    {
        var parameters = CreateParametersWithUpscale();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("upscale_unsample").GetProperty("inputs")
            .GetProperty("cfg").GetDouble().Should().Be(1.0);
    }

    #endregion

    #region Build Tests - Detailer

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
    public void Build_WithDetailer_ShouldUseChromaClipType()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("detailer_clip_loader").GetProperty("inputs")
            .GetProperty("type").GetString().Should().Be("chroma");
    }

    [Fact]
    public void Build_WithDetailer_ShouldContainT5TokenizerForDetailerScope()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("detailer_t5_tokenizer", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("T5TokenizerOptions");
    }

    [Fact]
    public void Build_WithDetailer_LoraLoaders_ShouldReferenceScopedT5Tokenizer()
    {
        var parameters = CreateParametersWithDetailer();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("detailer_lora_positive").GetProperty("inputs")
            .GetProperty("clip")[0].GetString().Should().Be("detailer_t5_tokenizer");
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

    #region Build Tests - No FlowSelect

    [Fact]
    public void Build_ShouldNotContainFlowSelectNodes()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);

        // C# conditionals replace FlowSelect routing - no FlowSelect nodes should exist
        workflow.Json.Should().NotContain("FlowSelect");
    }

    [Fact]
    public void Build_ShouldContainOnlyOneSaveNode()
    {
        var parameters = CreateMinimalParameters();
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Only one save node (monolithic had 4)
        var saveCount = json.RootElement.EnumerateObject()
            .Count(p => p.Value.GetProperty("class_type").GetString() == "SaveImage");
        saveCount.Should().Be(1);
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
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "multistep/res_2m");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "beta");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 5.5);
        samplerFragment.SetValue(FragmentKeys.Params.Eta, 0.5);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);

        var latentFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
        latentFragment.SetValue("width", 872);
        latentFragment.SetValue("height", 1248);
        latentFragment.SetValue("batch_size", 1);

        parameters.Assets["Model"] = "Chroma1-HD.safetensors";
        parameters.Assets["Clip1"] = "t5xxl_fp8_e4m3fn_scaled.safetensors";
        parameters.Assets["VAE"] = "ae.safetensors";

        return parameters;
    }

    private static GenerationParameters CreateParametersWithUpscale()
    {
        var parameters = CreateMinimalParameters();

        var upscaleFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Upscale);
        upscaleFragment.IsActive = true;
        upscaleFragment.SetValue("upscale_model", "4x-UltraSharpV2.safetensors");
        upscaleFragment.SetValue("upscale_width", 1744);
        upscaleFragment.SetValue("upscale_height", 2496);
        upscaleFragment.SetValue("upscale_steps", 20);
        upscaleFragment.SetValue("upscale_denoise", 1.0);

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
