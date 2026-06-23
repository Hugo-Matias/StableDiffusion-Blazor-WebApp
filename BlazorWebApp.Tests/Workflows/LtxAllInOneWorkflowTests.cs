using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Templates.Ltx;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

public class LtxAllInOneWorkflowTests
{
    private readonly LtxAllInOneWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeOptionalFrameAndAudioSources()
    {
        var sources = _workflow.Metadata.Sources.ToList();

        sources.Should().HaveCount(3);
        sources.Should().Contain(s => s.Id == "first_frame" && s.Type == WfSourceType.Image && !s.Required);
        sources.Should().Contain(s => s.Id == "last_frame" && s.Type == WfSourceType.Image && !s.Required);
        sources.Should().Contain(s => s.Id == "audio_track" && s.Type == WfSourceType.Audio && !s.Required);
    }

    [Fact]
    public void Metadata_ShouldExposeCheckpointAndDiffusionAssets()
    {
        var assets = _workflow.Metadata.Assets.ToList();

        assets.Should().Contain(a => a.Parameter == "Checkpoint" && a.Type == WfAssetType.CheckpointModel);
        assets.Should().Contain(a => a.Parameter == "UNet" && a.Type == WfAssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == WfAssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "Clip2" && a.Type == WfAssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "VideoVae" && a.Type == WfAssetType.Vae);
        assets.Should().Contain(a => a.Parameter == "AudioVae" && a.Type == WfAssetType.Vae);
        assets.Should().NotContain(a => a.Parameter == "PreviewVae");
    }

    [Fact]
    public void SamplerModeDefaults_ShouldMatchComfyTextAndImageBranches()
    {
        var textToVideo = LtxAllInOneSamplerFragment.CreateTextToVideoDefaults();
        var imageToVideo = LtxAllInOneSamplerFragment.CreateImageToVideoDefaults();

        textToVideo.Stage1Steps.Should().Be(8);
        textToVideo.Stage2Steps.Should().Be(8);
        textToVideo.Stage1BasicSteps.Should().Be(8);
        textToVideo.Stage2BasicSteps.Should().Be(2);
        textToVideo.Stage2Denoise.Should().Be(0.15);
        textToVideo.Stage1VideoFlowTerminal.Should().Be(0.15);
        textToVideo.Stage2VideoFlowStart.Should().Be(0.80);
        textToVideo.Stage2VideoFlowTerminal.Should().Be(0.0);

        imageToVideo.Stage1Steps.Should().Be(4);
        imageToVideo.Stage2Steps.Should().Be(4);
        imageToVideo.Stage1BasicSteps.Should().Be(8);
        imageToVideo.Stage2BasicSteps.Should().Be(2);
        imageToVideo.Stage2Denoise.Should().Be(0.29);
        imageToVideo.Stage1VideoFlowTerminal.Should().Be(0.25);
        imageToVideo.Stage2VideoFlowStart.Should().Be(0.68);
        imageToVideo.Stage2VideoFlowTerminal.Should().Be(0.10);
    }

    [Fact]
    public void Build_WithoutSources_ShouldUseTextToVideoPathAndVideoFlowSigmas()
    {
        var parameters = CreateParameters(includeSources: false);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.TryGetProperty("ltx_first_frame_load_image", out _).Should().BeFalse();
        root.TryGetProperty("ltx_last_frame_load_image", out _).Should().BeFalse();
        root.TryGetProperty("stage1_first_frame_guide", out _).Should().BeFalse();
        root.TryGetProperty("ltx_load_audio", out _).Should().BeFalse();
        root.GetProperty("empty_ltx_latent_audio").GetProperty("class_type").GetString().Should().Be("LTXVEmptyLatentAudio");

        root.TryGetProperty("stage1_basic_scheduler", out _).Should().BeFalse();
        root.TryGetProperty("stage2_basic_scheduler", out _).Should().BeFalse();
        root.GetProperty("stage1_videoflow_base_scheduler").GetProperty("class_type").GetString().Should().Be("BasicScheduler");
        root.GetProperty("stage1_videoflow_catmull_rom").GetProperty("class_type").GetString().Should().Be("Sigmas CatmullRom");
        root.GetProperty("stage1_videoflow_pad").GetProperty("class_type").GetString().Should().Be("Sigmas Pad");
        root.GetProperty("stage1_sampler").GetProperty("inputs").GetProperty("sigmas")[0].GetString().Should().Be("stage1_videoflow_pad");
        root.GetProperty("stage1_videoflow_rescale").GetProperty("inputs").GetProperty("start").GetDouble().Should().Be(1.0);
        root.GetProperty("stage1_videoflow_rescale").GetProperty("inputs").GetProperty("end").GetDouble().Should().Be(0.15);
        root.GetProperty("stage2_videoflow_split_start").GetProperty("class_type").GetString().Should().Be("Sigmas Split Value");
        root.GetProperty("stage2_videoflow_split_start").GetProperty("inputs").GetProperty("split_value").GetDouble().Should().Be(0.80);
        root.GetProperty("stage2_videoflow_catmull_rom").GetProperty("inputs").GetProperty("points").GetInt32().Should().Be(9);
        // Stage 2 clamps midpoint into (0, start) using small epsilons. With start=0.80, midpoint=0.54 -> unchanged.
        root.GetProperty("stage2_videoflow_calculator").GetProperty("inputs").GetProperty("variables.a").GetDouble().Should().Be(0.5);
        root.GetProperty("stage2_videoflow_calculator").GetProperty("inputs").GetProperty("variables.c").GetDouble().Should().Be(0.54);
        // Stage 2 resample chain + shifted-terminal fallback when user terminal == 0 (T2V default).
        root.GetProperty("stage2_videoflow_stage2_catmull_coarse").GetProperty("inputs").GetProperty("points").GetInt32().Should().Be(1000);
        root.GetProperty("stage2_videoflow_stage2_split_low").GetProperty("inputs").GetProperty("split_value").GetDouble().Should().Be(0.80);
        // Fine catmull tracks user step count (Steps + 1, clamped to >= 5). T2V default Stage2Steps = 8 -> 9.
        root.GetProperty("stage2_videoflow_stage2_catmull_fine").GetProperty("inputs").GetProperty("points").GetInt32().Should().Be(9);
        root.GetProperty("stage2_videoflow_stage2_shifted_terminal").GetProperty("class_type").GetString().Should().Be("easy indexAnything");
        root.GetProperty("stage2_videoflow_stage2_shifted_terminal").GetProperty("inputs").GetProperty("index").GetInt32().Should().Be(-2);
        root.GetProperty("stage2_videoflow_stage2_rescale").GetProperty("inputs").GetProperty("end")[0].GetString().Should().Be("stage2_videoflow_stage2_shifted_terminal");
        root.GetProperty("stage2_sampler").GetProperty("inputs").GetProperty("sigmas")[0].GetString().Should().Be("stage2_videoflow_stage2_pad");
        root.TryGetProperty("stage2_videoflow_rescale", out _).Should().BeFalse();
        root.TryGetProperty("stage2_videoflow_shaped_pad", out _).Should().BeFalse();
        root.TryGetProperty("stage1_sigmas", out _).Should().BeFalse();
        root.TryGetProperty("stage2_sigmas", out _).Should().BeFalse();

        root.TryGetProperty("ltx_preview_vae_loader", out _).Should().BeFalse();
        root.TryGetProperty("ltx_preview_override", out _).Should().BeFalse();

        root.GetProperty("ltx_source_sage_attention").GetProperty("inputs").GetProperty("allow_compile").GetBoolean().Should().BeTrue();

        var latentInputs = root.GetProperty("empty_ltx_latent_video").GetProperty("inputs");
        latentInputs.GetProperty("width").GetInt32().Should().Be(864);
        latentInputs.GetProperty("height").GetInt32().Should().Be(576);

        root.GetProperty("ltx_rife_interpolation").GetProperty("class_type").GetString().Should().Be("RIFE VFI");
        root.GetProperty("ltx_vhs_video_combine").GetProperty("inputs").GetProperty("frame_rate").GetInt32().Should().Be(50);
    }

    [Fact]
    public void Build_WithBasicSigmaMode_ShouldUseBasicSchedulers()
    {
        var parameters = CreateParameters(includeSources: false);
        var sampler = parameters.GetOrCreateFragment("ltx_all_in_one_sampler");
        sampler.SetValue("sigma_mode", "basic");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.GetProperty("stage1_basic_scheduler").GetProperty("class_type").GetString().Should().Be("BasicScheduler");
        root.GetProperty("stage2_basic_scheduler").GetProperty("class_type").GetString().Should().Be("BasicScheduler");
        root.GetProperty("stage1_basic_scheduler").GetProperty("inputs").GetProperty("steps").GetInt32().Should().Be(8);
        root.GetProperty("stage2_basic_scheduler").GetProperty("inputs").GetProperty("steps").GetInt32().Should().Be(2);
        root.GetProperty("stage2_basic_scheduler").GetProperty("inputs").GetProperty("denoise").GetDouble().Should().Be(0.15);
        root.TryGetProperty("stage1_videoflow_base_scheduler", out _).Should().BeFalse();
        root.TryGetProperty("stage1_sigmas", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithFrameSourcesAndBasicSigmaMode_ShouldUseImageToVideoBasicDefaults()
    {
        var parameters = CreateParameters(includeSources: true, configureGuides: false);
        var sampler = parameters.GetOrCreateFragment("ltx_all_in_one_sampler");
        sampler.SetValue("sigma_mode", "basic");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.GetProperty("stage1_basic_scheduler").GetProperty("inputs").GetProperty("steps").GetInt32().Should().Be(8);
        root.GetProperty("stage1_basic_scheduler").GetProperty("inputs").GetProperty("denoise").GetDouble().Should().Be(1.0);
        root.GetProperty("stage2_basic_scheduler").GetProperty("inputs").GetProperty("steps").GetInt32().Should().Be(2);
        root.GetProperty("stage2_basic_scheduler").GetProperty("inputs").GetProperty("denoise").GetDouble().Should().Be(0.29);
    }

    [Fact]
    public void Build_WithFrameAndAudioSources_ShouldAddGuidesAndAudioEncoding()
    {
        var parameters = CreateParameters(includeSources: true);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.GetProperty("ltx_first_frame_load_image").GetProperty("inputs").GetProperty("image").GetString().Should().Be("first.png");
        root.GetProperty("ltx_last_frame_load_image").GetProperty("inputs").GetProperty("image").GetString().Should().Be("last.png");
        root.GetProperty("ltx_load_audio").GetProperty("inputs").GetProperty("audio").GetString().Should().Be("voice.wav");
        root.GetProperty("ltx_audio_vae_encode").GetProperty("class_type").GetString().Should().Be("LTXVAudioVAEEncode");
        root.TryGetProperty("empty_ltx_latent_audio", out _).Should().BeFalse();

        var firstGuideInputs = root.GetProperty("stage1_first_frame_guide").GetProperty("inputs");
        firstGuideInputs.GetProperty("frame_idx").GetInt32().Should().Be(0);
        firstGuideInputs.GetProperty("strength").GetDouble().Should().Be(0.75);

        var lastGuideInputs = root.GetProperty("stage1_last_frame_guide").GetProperty("inputs");
        lastGuideInputs.GetProperty("frame_idx").GetInt32().Should().Be(-1);
        lastGuideInputs.GetProperty("strength").GetDouble().Should().Be(0.6);

        root.GetProperty("stage2_first_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(0.75);
        root.GetProperty("stage2_last_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(0.6);
        // After Pass 1 sampling, LTXVCropGuides strips the conditioning-frame padding and
        // re-registers ltx_positive_output to point at its cropped cond output, so the
        // Stage 2 first frame guide reads from ltx_crop_guides_pass1[0] (matches reference
        // Comfy graph: 1194:1183.positive <- 389:1050,0).
        root.GetProperty("stage2_first_frame_guide").GetProperty("inputs").GetProperty("positive")[0].GetString().Should().Be("ltx_crop_guides_pass1");
        root.GetProperty("stage2_last_frame_guide").GetProperty("inputs").GetProperty("positive")[0].GetString().Should().Be("stage2_first_frame_guide");
        root.GetProperty("stage2_guider").GetProperty("inputs").GetProperty("positive")[0].GetString().Should().Be("stage2_last_frame_guide");
        root.GetProperty("stage2_sampler").GetProperty("inputs").GetProperty("latent_image")[0].GetString().Should().Be("ltx_concat_av_pass2");
        root.TryGetProperty("ltx_crop_guides", out _).Should().BeFalse();

        // LTXVCropGuides nodes (one per pass) for I2V/A2V - required to avoid the image
        // burning into the late frames of the decoded video.
        var crop1 = root.GetProperty("ltx_crop_guides_pass1");
        crop1.GetProperty("class_type").GetString().Should().Be("LTXVCropGuides");
        crop1.GetProperty("inputs").GetProperty("latent")[0].GetString().Should().Be("ltx_separate_pass1");
        root.GetProperty("ltx_upsample").GetProperty("inputs").GetProperty("samples")[0].GetString().Should().Be("ltx_crop_guides_pass1");

        var crop2 = root.GetProperty("ltx_crop_guides_pass2");
        crop2.GetProperty("class_type").GetString().Should().Be("LTXVCropGuides");
        crop2.GetProperty("inputs").GetProperty("latent")[0].GetString().Should().Be("ltx_final_separate_av");
        root.GetProperty("ltx_final_vae_decode_tiled").GetProperty("inputs").GetProperty("samples")[0].GetString().Should().Be("ltx_crop_guides_pass2");

        var stage2SplitInputs = root.GetProperty("stage2_videoflow_split_start").GetProperty("inputs");
        stage2SplitInputs.GetProperty("split_value").GetDouble().Should().Be(0.68);
        root.GetProperty("stage1_videoflow_catmull_rom").GetProperty("inputs").GetProperty("points").GetInt32().Should().Be(5);

        // Stage 2 I2V terminal=0.10 (>0) wires Rescale.end as a static value (no shifted-terminal node).
        var stage2ResampleRescaleInputs = root.GetProperty("stage2_videoflow_stage2_rescale").GetProperty("inputs");
        stage2ResampleRescaleInputs.GetProperty("start").GetDouble().Should().Be(0.68);
        stage2ResampleRescaleInputs.GetProperty("end").GetDouble().Should().Be(0.10);
        root.TryGetProperty("stage2_videoflow_stage2_shifted_terminal", out _).Should().BeFalse();
        root.GetProperty("stage2_videoflow_calculator").GetProperty("inputs").GetProperty("variables.a").GetDouble().Should().Be(0.5);
        root.GetProperty("stage2_videoflow_calculator").GetProperty("inputs").GetProperty("variables.c").GetDouble().Should().Be(0.54);
        root.GetProperty("stage2_first_frame_guide").GetProperty("inputs").GetProperty("negative")[0].GetString().Should().Be("ltx_stage2_zero_negative");
        root.GetProperty("stage2_sampler").GetProperty("inputs").GetProperty("sigmas")[0].GetString().Should().Be("stage2_videoflow_stage2_pad");
        root.TryGetProperty("stage2_videoflow_shaped_pad", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithDefaultFrameGuideSettings_ShouldMatchComfyStrengths()
    {
        var parameters = CreateParameters(includeSources: true, configureGuides: false);
        parameters.GetOrCreateFragment("ltx_frame_guides");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.GetProperty("stage1_first_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(1.0);
        root.GetProperty("stage1_last_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(1.0);
        root.GetProperty("stage2_first_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(1.0);
        root.GetProperty("stage2_last_frame_guide").GetProperty("inputs").GetProperty("strength").GetDouble().Should().Be(1.0);
    }

    [Fact]
    public void Build_WithManualSigmas_ShouldSkipSchedulersAndEmitManualSigmas()
    {
        var parameters = CreateParameters(includeSources: false);
        var sampler = parameters.GetOrCreateFragment("ltx_all_in_one_sampler");
        sampler.SetValue("use_manual_sigmas", true);
        sampler.SetValue("stage1_sigmas", "1.0, 0.5, 0.0");
        sampler.SetValue("stage2_sigmas", "0.2, 0.0");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.TryGetProperty("stage1_basic_scheduler", out _).Should().BeFalse();
        root.TryGetProperty("stage2_basic_scheduler", out _).Should().BeFalse();
        root.TryGetProperty("stage1_videoflow_base_scheduler", out _).Should().BeFalse();
        root.GetProperty("stage1_sigmas").GetProperty("class_type").GetString().Should().Be("ManualSigmas");
        root.GetProperty("stage1_sigmas").GetProperty("inputs").GetProperty("sigmas").GetString().Should().Be("1.0, 0.5, 0.0");
        root.GetProperty("stage2_sigmas").GetProperty("inputs").GetProperty("sigmas").GetString().Should().Be("0.2, 0.0");
    }

    private static GenerationParameters CreateParameters(bool includeSources, bool configureGuides = true)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Checkpoint"] = "selected-checkpoint.safetensors",
                ["UNet"] = "selected-unet.safetensors",
                ["Clip"] = "selected-gemma.safetensors",
                ["Clip2"] = "selected-projection.safetensors",
                ["VideoVae"] = "selected-video-vae.safetensors",
                ["AudioVae"] = "selected-audio-vae.safetensors",
                ["UpscaleModel"] = "selected-upscaler.safetensors"
            }
        };

        if (includeSources)
        {
            parameters.Sources = new Dictionary<string, SourceAsset>
            {
                ["first_frame"] = new() { Filename = "first.png", FilePath = "first.png" },
                ["last_frame"] = new() { Filename = "last.png", FilePath = "last.png" },
                ["audio_track"] = new() { Filename = "voice.wav", FilePath = "voice.wav" }
            };
        }

        var video = parameters.GetOrCreateFragment("ltx_all_in_one_video_settings");
        video.SetValue("width", 1728);
        video.SetValue("height", 1152);
        video.SetValue("duration", 1);
        video.SetValue("frame_rate", 25);
        video.SetValue("img_compression", 35);
        video.SetValue("i2v_strength", 0.9);

        if (configureGuides)
        {
            var guides = parameters.GetOrCreateFragment("ltx_frame_guides");
            guides.SetValue("first_strength", 0.75);
            guides.SetValue("last_strength", 0.6);
        }

        var sampler = parameters.GetOrCreateFragment("ltx_all_in_one_sampler");
        sampler.SetValue("stage1_seed", 42L);
        sampler.SetValue("stage2_seed", 43L);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("scheduler", "linear_quadratic");
        sampler.SetValue("stage1_steps", includeSources ? 4 : 8);
        sampler.SetValue("stage2_steps", includeSources ? 4 : 8);
        sampler.SetValue("stage1_denoise", 1.0);
        sampler.SetValue("stage2_denoise", includeSources ? 0.29 : 0.15);

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a cinematic test clip");
        prompts.SetValue("negative", "low quality");

        return parameters;
    }
}
