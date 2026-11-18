using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorkflowAssetType = BlazorWebApp.Workflows.Models.AssetType;

namespace BlazorWebApp.Tests.Workflows;

public class QwenSwapAnythingSam31WorkflowTests
{
    private readonly QwenSwapAnythingSam31Workflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeQwenImg2ImgWorkflow()
    {
        _workflow.Metadata.Title.Should().Be("Swap Anything (SAM 3.1)");
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Qwen);
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Img);
        _workflow.Metadata.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Metadata_ShouldExposeCheckpointAssetsAndSources()
    {
        var assets = _workflow.Metadata.Assets.ToList();
        assets.Should().Contain(a => a.Parameter == "QwenCheckpoint" && a.Type == WorkflowAssetType.CheckpointModel);
        assets.Should().Contain(a => a.Parameter == "SamCheckpoint" && a.Type == WorkflowAssetType.CheckpointModel);

        var sources = _workflow.Metadata.Sources.ToList();
        sources.Should().Contain(s => s.Id == "target_image" && s.Required && s.Type == SourceType.Image);
        sources.Should().Contain(s => s.Id == "reference_image" && s.Required && s.Type == SourceType.Image);
    }

    [Fact]
    public void GetFragments_ShouldExposePromptSettingsAndSampler()
    {
        var fragments = _workflow.GetFragments().ToList();

        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
        fragments.Should().Contain(f => f.Metadata.Id == "swap_anything_settings" && f.Metadata.Component == "QwenSwapAnythingSettingsForm");
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler" && f.Metadata.Component == "SamplerForm");
    }

    [Fact]
    public void Build_ShouldUseWasImagesToRgbReplacement()
    {
        using var json = BuildJson();

        json.RootElement.GetProperty("reference_images_to_rgb").GetProperty("class_type").GetString()
            .Should().Be("Images to RGB");
        json.RootElement.TryGetProperty("reference_preview_bridge", out _).Should().BeFalse();
        json.RootElement.EnumerateObject().Should().NotContain(p => p.Value.GetProperty("class_type").GetString() == "Image to RGB [RvTools]");
    }

    [Fact]
    public void Build_ShouldReplaceLatentSwitcherWithConfiguredTargetLatent()
    {
        using var json = BuildJson();

        json.RootElement.EnumerateObject().Should().NotContain(p => p.Value.GetProperty("class_type").GetString() == "Latent Switch (JPS)");
        var samplerInputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        samplerInputs.GetProperty("latent_image")[0].GetString().Should().Be("target_vae_encode");
    }

    [Fact]
    public void Build_WithEmptyLatentSource_ShouldFeedSamplerFromEmptyLatent()
    {
        var parameters = CreateParameters();
        parameters.GetOrCreateFragment("swap_anything_settings").SetValue("latent_source", "empty");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var samplerInputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        samplerInputs.GetProperty("latent_image")[0].GetString().Should().Be("empty_latent");
        json.RootElement.GetProperty("empty_latent").GetProperty("inputs").GetProperty("width").GetInt32().Should().Be(896);
    }

    [Fact]
    public void Build_ShouldWireSamMasksAndQwenConditioning()
    {
        using var json = BuildJson();

        json.RootElement.GetProperty("target_sam_a_detect").GetProperty("class_type").GetString().Should().Be("SAM3_Detect");
        json.RootElement.GetProperty("target_mask_composite").GetProperty("inputs").GetProperty("operation").GetString().Should().Be("add");
        json.RootElement.GetProperty("qwen_encode_positive").GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
        json.RootElement.GetProperty("positive_reference_method").GetProperty("class_type").GetString().Should().Be("FluxKontextMultiReferenceLatentMethod");
    }

    [Fact]
    public void Build_ShouldUseStandardImageSavePrefix()
    {
        using var json = BuildJson();

        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("filename_prefix").GetString()
            .Should().Be(SaveFragment.StandardFilenamePrefix);
    }

    private JsonDocument BuildJson()
    {
        var workflow = _workflow.Build(CreateParameters());
        return JsonDocument.Parse(workflow.Json);
    }

    private static GenerationParameters CreateParameters()
    {
        var parameters = new GenerationParameters();
        parameters.Assets["QwenCheckpoint"] = "Base/Qwen-Rapid-AIO-NSFW-v19.safetensors";
        parameters.Assets["SamCheckpoint"] = "SAM/sam3.1_multiplex_fp16.safetensors";
        parameters.Sources["target_image"] = new SourceAsset { Filename = "target.png" };
        parameters.Sources["reference_image"] = new SourceAsset { Filename = "reference.png" };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "replace face and hair from image2");
        prompts.SetValue("negative", "bad quality");

        var settings = parameters.GetOrCreateFragment("swap_anything_settings");
        settings.SetValue("sam_prompt_a", "face");
        settings.SetValue("sam_prompt_b", "hair");
        settings.SetValue("reference_prompt", "face,hair");
        settings.SetValue("sam_threshold", 0.5);
        settings.SetValue("sam_refine_iterations", 2);
        settings.SetValue("grow_mask_expand", 20);
        settings.SetValue("target_megapixels", 1.0);
        settings.SetValue("reference_megapixels", 1.0);
        settings.SetValue("reference_scale_length", 1328);
        settings.SetValue("latent_source", "target");

        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", "er_sde");
        sampler.SetValue("scheduler", "beta");
        sampler.SetValue("steps", 4);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("seed", 12345L);

        return parameters;
    }
}