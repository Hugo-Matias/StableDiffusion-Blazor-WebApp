using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for WanSteadyDancerWorkflow (Phase 6 Step 7).
/// </summary>
public class WanSteadyDancerWorkflowTests
{
    private readonly WanSteadyDancerWorkflow _workflow = new();

    #region Metadata

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("SteadyDancer");
    }

    [Fact]
    public void Metadata_ShouldHaveWanBase()
    {
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Wan);
    }

    [Fact]
    public void Metadata_ShouldHaveImg2VidMode()
    {
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Vid);
    }

    [Fact]
    public void Metadata_ShouldHaveFiveAssets()
    {
        _workflow.Metadata.Assets.Should().HaveCount(5);
        var assets = _workflow.Metadata.Assets.ToList();
        assets.Should().Contain(a => a.Parameter == "Model" && a.Type == WfAssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == WfAssetType.Vae);
        assets.Should().Contain(a => a.Parameter == "ClipVision" && a.Type == WfAssetType.ClipVision);
        assets.Should().Contain(a => a.Parameter == "TextEncoder" && a.Type == WfAssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "SpeedLora" && a.Type == WfAssetType.Lora);
    }

    [Fact]
    public void Metadata_ShouldHaveTwoSources()
    {
        _workflow.Metadata.Sources.Should().HaveCount(2);
        var sources = _workflow.Metadata.Sources.ToList();

        var videoSource = sources.First(s => s.Id == "source_video");
        videoSource.Type.Should().Be(WfSourceType.Video);
        videoSource.Required.Should().BeTrue();

        var imageSource = sources.First(s => s.Id == "reference_image");
        imageSource.Type.Should().Be(WfSourceType.Image);
        imageSource.Required.Should().BeFalse();
    }

    [Fact]
    public void Metadata_ShouldHaveDeterministicId()
    {
        var expected = WorkflowMetadata.GenerateDeterministicId(
            Data.Enums.ModelBase.Wan, ModeType.Img2Vid, "SteadyDancer");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new WanSteadyDancerWorkflow();
        _workflow.Metadata.Id.Should().Be(other.Metadata.Id);
    }

    #endregion

    #region GetFragments

    [Fact]
    public void GetFragments_ShouldReturnFourUIFragments()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments.Should().HaveCount(5);
    }

    [Fact]
    public void GetFragments_ShouldReturnCorrectFragmentTypes()
    {
        var fragments = _workflow.GetFragments().ToList();
        fragments[0].Metadata.Id.Should().Be("prompts");
        fragments[1].Metadata.Id.Should().Be("load_video");
        fragments[2].Metadata.Id.Should().Be("sampler_wan");
        fragments[3].Metadata.Id.Should().Be("context_options");
        fragments[4].Metadata.Id.Should().Be("steadydancer_embeds");
    }

    #endregion

    #region Build - Basic Pipeline

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);

        result.Should().NotBeNull();
        result.Json.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_DefaultParameters_ShouldHaveExpectedNodeCount()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);

        var json = JsonDocument.Parse(result.Json);
        // Expected nodes (no preview concat):
        // 1 load_video + 1 get_size + 1 load_image + 1 resize_subject
        // + 6 LoadWanModel (compile, loader, blockswap, setblockswap, loraselect, setloras)
        // + 1 LoadWanVae + 1 LoadClipVision + 1 TextEncode
        // + 4 PoseDetection (onnx, detection, draw, resize)
        // + 2 I2VEncode (clipvision_encode, i2v_encode)
        // + 4 SteadyDancerEmbeds (pose_encode, get_first, pose_clip, add_steadydancer)
        // + 1 ContextOptions
        // + 3 SamplerWan (scheduler, settings, sampler)
        // + 1 DecodeWan
        // + 1 SaveVideo
        // = 29 nodes
        var nodeCount = 0;
        foreach (var _ in json.RootElement.EnumerateObject()) nodeCount++;
        nodeCount.Should().Be(29);
    }

    [Fact]
    public void Build_ShouldContainVideoLoadingNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("load_video", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("get_size", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainImageNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("load_image", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("resize_subject", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainWanModelNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("compile_settings", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("block_swap", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("set_block_swap", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("lora_select", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("set_loras", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainEncodingNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_vision_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("text_encode", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainPoseDetectionNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("onnx_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("pose_detection", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("draw_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("resize_pose", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainI2VAndEmbedsNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("clip_vision_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("i2v_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("pose_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("get_first_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("pose_clip_vision", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("add_steadydancer", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainSamplerAndDecodeNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("context_opts", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("scheduler", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sampler_settings", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sampler", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("decode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("video_output", out _).Should().BeTrue();
    }

    #endregion

    #region Build - Sampler Parameters

    [Fact]
    public void Build_ShouldSetSamplerParameters()
    {
        var parameters = CreateDefaultParameters(steps: 8, cfg: 2.5, seed: 99999, scheduler: "euler");

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var settingsInputs = json.RootElement.GetProperty("sampler_settings").GetProperty("inputs");
        settingsInputs.GetProperty("steps").GetInt32().Should().Be(8);
        settingsInputs.GetProperty("cfg").GetDouble().Should().BeApproximately(2.5, 0.01);
        settingsInputs.GetProperty("seed").GetInt64().Should().Be(99999);

        var schedulerInputs = json.RootElement.GetProperty("scheduler").GetProperty("inputs");
        schedulerInputs.GetProperty("scheduler").GetString().Should().Be("euler");
    }

    [Fact]
    public void Build_ShouldSetContextOptions()
    {
        var parameters = CreateDefaultParameters(contextFrames: 120, contextOverlap: 32);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var contextInputs = json.RootElement.GetProperty("context_opts").GetProperty("inputs");
        contextInputs.GetProperty("context_frames").GetInt32().Should().Be(120);
        contextInputs.GetProperty("context_overlap").GetInt32().Should().Be(32);
    }

    [Fact]
    public void Build_ShouldSetPoseStrengthParameters()
    {
        var parameters = CreateDefaultParameters(poseStrengthSpatial: 0.7, poseStrengthTemporal: 0.5);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var embedsInputs = json.RootElement.GetProperty("add_steadydancer").GetProperty("inputs");
        embedsInputs.GetProperty("pose_strength_spatial").GetDouble().Should().BeApproximately(0.7, 0.01);
        embedsInputs.GetProperty("pose_strength_temporal").GetDouble().Should().BeApproximately(0.5, 0.01);
    }

    #endregion

    #region Build - Assets

    [Fact]
    public void Build_ShouldUseCustomModelAsset()
    {
        var parameters = CreateDefaultParameters();
        parameters.Assets["Model"] = "custom_model.safetensors";

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var loaderInputs = json.RootElement.GetProperty("model_loader").GetProperty("inputs");
        loaderInputs.GetProperty("model").GetString().Should().Be("custom_model.safetensors");
    }

    [Fact]
    public void Build_ShouldUseCustomLoraAsset()
    {
        var parameters = CreateDefaultParameters();
        parameters.Assets["SpeedLora"] = "custom_lora.safetensors";

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var loraInputs = json.RootElement.GetProperty("lora_select").GetProperty("inputs");
        loraInputs.GetProperty("lora").GetString().Should().Be("custom_lora.safetensors");
    }

    [Fact]
    public void Build_ShouldUseCustomTextEncoderAsset()
    {
        var parameters = CreateDefaultParameters();
        parameters.Assets["TextEncoder"] = "custom_encoder.safetensors";

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var textInputs = json.RootElement.GetProperty("text_encode").GetProperty("inputs");
        textInputs.GetProperty("model_name").GetString().Should().Be("custom_encoder.safetensors");
    }

    #endregion

    #region Build - Preview Concatenation

    [Fact]
    public void Build_WithAppendPreview_ShouldAddConcatNodes()
    {
        var parameters = CreateDefaultParameters(appendPreview: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("concat_source_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("concat_final", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithAppendPreview_ShouldUsePreviewConcatAsVideoInput()
    {
        var parameters = CreateDefaultParameters(appendPreview: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("images")[0].GetString().Should().Be("concat_final");
    }

    [Fact]
    public void Build_WithAppendPreview_ShouldHaveTwoExtraNodes()
    {
        var paramsNoPreview = CreateDefaultParameters(appendPreview: false);
        var paramsPreview = CreateDefaultParameters(appendPreview: true);

        var resultNoPreview = _workflow.Build(paramsNoPreview);
        var resultPreview = _workflow.Build(paramsPreview);

        var countNoPreview = CountNodes(resultNoPreview.Json);
        var countPreview = CountNodes(resultPreview.Json);

        countPreview.Should().Be(countNoPreview + 2);
    }

    [Fact]
    public void Build_WithoutAppendPreview_ShouldUseImageOutputAsVideoInput()
    {
        var parameters = CreateDefaultParameters(appendPreview: false);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("images")[0].GetString().Should().Be("decode");
    }

    [Fact]
    public void Build_WithoutAppendPreview_ShouldNotContainConcatNodes()
    {
        var parameters = CreateDefaultParameters(appendPreview: false);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("concat_source_pose", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("concat_final", out _).Should().BeFalse();
    }

    #endregion

    #region Build - Frame Rate

    [Fact]
    public void Build_ShouldSetOutputFrameRate()
    {
        var parameters = CreateDefaultParameters(outputFrameRate: 30);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("frame_rate").GetInt32().Should().Be(30);
    }

    #endregion

    #region Build - Registry

    [Fact]
    public void Build_ShouldReturnRegistryWithKeyOutputs()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);

        result.Registry.Should().NotBeNull();
        result.Registry!.HasOutput("video_frames").Should().BeTrue();
        result.Registry.HasOutput("model_output").Should().BeTrue();
        result.Registry.HasOutput("vae_output").Should().BeTrue();
        result.Registry.HasOutput("text_embeds").Should().BeTrue();
        result.Registry.HasOutput("pose_images").Should().BeTrue();
        result.Registry.HasOutput("image_embeds").Should().BeTrue();
        result.Registry.HasOutput("steadydancer_embeds").Should().BeTrue();
        result.Registry.HasOutput("latent_output").Should().BeTrue();
        result.Registry.HasOutput("image_output").Should().BeTrue();
    }

    #endregion

    #region Build - Prompts

    [Fact]
    public void Build_ShouldSetPrompts()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var textInputs = json.RootElement.GetProperty("text_encode").GetProperty("inputs");
        textInputs.GetProperty("positive_prompt").GetString().Should().Be("a person dancing gracefully");
        textInputs.GetProperty("negative_prompt").GetString().Should().Be("blurry");
    }

    #endregion

    #region Helpers

    private static GenerationParameters CreateDefaultParameters(
        int steps = 4,
        double cfg = 1.0,
        int shift = 5,
        long seed = 42,
        string scheduler = "dpm++_sde",
        int contextFrames = 81,
        int contextOverlap = 16,
        double poseStrengthSpatial = 1.0,
        double poseStrengthTemporal = 1.0,
        bool appendPreview = false,
        int outputFrameRate = 24)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Model"] = "Wan21_SteadyDancer_fp8_e4m3fn_scaled_KJ.safetensors",
                ["Vae"] = "wan_2.1_vae.safetensors",
                ["ClipVision"] = "clip_vision_h.safetensors",
                ["TextEncoder"] = "umt5_xxl_fp16.safetensors",
                ["SpeedLora"] = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["source_video"] = new SourceAsset { Filename = "dance_video.mp4", FilePath = "dance_video.mp4" },
                ["reference_image"] = new SourceAsset { Filename = "reference.png", FilePath = "reference.png" }
            }
        };

        // Sampler fragment
        var sampler = parameters.GetOrCreateFragment("sampler_wan");
        sampler.SetValue("steps", steps);
        sampler.SetValue("cfg", cfg);
        sampler.SetValue("shift", shift);
        sampler.SetValue("seed", seed);
        sampler.SetValue("scheduler", scheduler);
        sampler.SetValue("lora_strength", 1.0);
        sampler.SetValue("output_frame_rate", outputFrameRate);
        sampler.SetValue("append_preview", appendPreview);

        // Context options fragment
        var context = parameters.GetOrCreateFragment("context_options");
        context.SetValue("context_frames", contextFrames);
        context.SetValue("context_overlap", contextOverlap);

        // SteadyDancer embeds fragment
        var embeds = parameters.GetOrCreateFragment("steadydancer_embeds");
        embeds.SetValue("pose_strength_spatial", poseStrengthSpatial);
        embeds.SetValue("pose_strength_temporal", poseStrengthTemporal);

        // Load video fragment
        var video = parameters.GetOrCreateFragment("load_video");
        video.SetValue("force_rate", 16);
        video.SetValue("custom_width", 480);
        video.SetValue("custom_height", 832);
        video.SetValue("frame_load_cap", 176);
        video.SetValue("skip_first_frames", 0);
        video.SetValue("select_every_nth", 1);

        // Prompts fragment
        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a person dancing gracefully");
        prompts.SetValue("negative", "blurry");

        return parameters;
    }

    private static int CountNodes(string json)
    {
        var doc = JsonDocument.Parse(json);
        var count = 0;
        foreach (var _ in doc.RootElement.EnumerateObject()) count++;
        return count;
    }

    #endregion
}
