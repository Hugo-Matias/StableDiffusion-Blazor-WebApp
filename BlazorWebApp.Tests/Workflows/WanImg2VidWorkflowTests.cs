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
/// Tests for WanImg2VidWorkflow (Phase 6 Step 5).
/// </summary>
public class WanImg2VidWorkflowTests
{
    private readonly WanImg2VidWorkflow _workflow = new();

    #region Metadata

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        _workflow.Metadata.Title.Should().Be("Img2Vid");
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
        assets.Should().Contain(a => a.Parameter == "HighModel" && a.Type == WfAssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "LowModel" && a.Type == WfAssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == WfAssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "ClipVision" && a.Type == WfAssetType.ClipVision);
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == WfAssetType.Vae);
    }

    [Fact]
    public void Metadata_ShouldHaveOneSource()
    {
        _workflow.Metadata.Sources.Should().HaveCount(1);
        var source = _workflow.Metadata.Sources.First();
        source.Id.Should().Be("source_image");
        source.Type.Should().Be(WfSourceType.Image);
        source.Required.Should().BeTrue();
    }

    [Fact]
    public void Metadata_ShouldHaveDeterministicId()
    {
        var expected = WorkflowMetadata.GenerateDeterministicId(
            Data.Enums.ModelBase.Wan, ModeType.Img2Vid, "Img2Vid");
        _workflow.Metadata.Id.Should().Be(expected);
    }

    [Fact]
    public void Metadata_Id_ShouldBeStableAcrossInstances()
    {
        var other = new WanImg2VidWorkflow();
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
        fragments[1].Metadata.Id.Should().Be("wan_load_image");
        fragments[2].Metadata.Id.Should().Be("painter_i2v");
        fragments[3].Metadata.Id.Should().Be("sampler_advanced");
        fragments[4].Metadata.Id.Should().Be("frame_interpolation");
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
        // Expected nodes (no LoRA, no interpolation):
        // 3 high model (unet, sage, torch) + 3 low model + 2 clip/vae
        // + 2 model_sampling + 2 image (load, resize) + 2 clip_vision (load, encode)
        // + 2 prompts + 1 painter + 2 samplers + 1 clean + 1 vae_decode + 1 save_video
        // = 22 nodes
        var nodeCount = 0;
        foreach (var _ in json.RootElement.EnumerateObject()) nodeCount++;
        nodeCount.Should().Be(22);
    }

    [Fact]
    public void Build_ShouldContainDualModelNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("high_unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("high_sage", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("high_torch", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("low_unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("low_sage", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("low_torch", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainDualSamplerNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("sampler_high", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sampler_low", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainCoreNodes()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("clip_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("load_image", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("image_resize", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_vision_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_vision_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("positive_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("negative_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("painter_i2v", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clean_sampler", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("vae_decoder", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("video_output", out _).Should().BeTrue();
    }

    #endregion

    #region Build - Auto Split

    [Fact]
    public void Build_AutoSplitEnabled_ShouldSplitStepsAtMidpoint()
    {
        var parameters = CreateDefaultParameters(steps: 8, autoSplit: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var highInputs = json.RootElement.GetProperty("sampler_high").GetProperty("inputs");
        highInputs.GetProperty("start_at_step").GetInt32().Should().Be(0);
        highInputs.GetProperty("end_at_step").GetInt32().Should().Be(4);

        var lowInputs = json.RootElement.GetProperty("sampler_low").GetProperty("inputs");
        lowInputs.GetProperty("start_at_step").GetInt32().Should().Be(4);
        lowInputs.GetProperty("end_at_step").GetInt32().Should().Be(10000);
    }

    [Fact]
    public void Build_AutoSplitEnabled_OddSteps_ShouldRoundMidpoint()
    {
        var parameters = CreateDefaultParameters(steps: 7, autoSplit: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        // Math.Round(7/2.0) = Math.Round(3.5) = 4
        var highInputs = json.RootElement.GetProperty("sampler_high").GetProperty("inputs");
        highInputs.GetProperty("end_at_step").GetInt32().Should().Be(4);

        var lowInputs = json.RootElement.GetProperty("sampler_low").GetProperty("inputs");
        lowInputs.GetProperty("start_at_step").GetInt32().Should().Be(4);
    }

    [Fact]
    public void Build_LowSampler_ShouldHaveSeedZeroAndNoNoise()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var lowInputs = json.RootElement.GetProperty("sampler_low").GetProperty("inputs");
        lowInputs.GetProperty("noise_seed").GetInt64().Should().Be(0);
        lowInputs.GetProperty("add_noise").GetString().Should().Be("disable");
        lowInputs.GetProperty("return_with_leftover_noise").GetString().Should().Be("disable");
    }

    [Fact]
    public void Build_HighSampler_ShouldHaveUserSeedAndNoise()
    {
        var parameters = CreateDefaultParameters(seed: 12345);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var highInputs = json.RootElement.GetProperty("sampler_high").GetProperty("inputs");
        highInputs.GetProperty("noise_seed").GetInt64().Should().Be(12345);
        highInputs.GetProperty("add_noise").GetString().Should().Be("enable");
        highInputs.GetProperty("return_with_leftover_noise").GetString().Should().Be("enable");
    }

    [Fact]
    public void Build_LowSampler_ShouldChainFromHighSamplerLatent()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var lowInputs = json.RootElement.GetProperty("sampler_low").GetProperty("inputs");
        lowInputs.GetProperty("latent_image")[0].GetString().Should().Be("sampler_high");
    }

    #endregion

    #region Build - LoRA

    [Fact]
    public void Build_WithHighLora_ShouldAddHighLoraNode()
    {
        var parameters = CreateDefaultParameters();
        parameters.Loras.Add(new Lora
        {
            Name = "test_lora",
            HighPath = "loras/test_high.safetensors",
            Strength = 0.8f,
            IsEnabled = true
        });

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("high_lora_loader_0", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("low_lora_loader_0", out _).Should().BeFalse();

        // ModelSampling should chain from lora output
        var highSamplingInputs = json.RootElement.GetProperty("high_model_sampling").GetProperty("inputs");
        highSamplingInputs.GetProperty("model")[0].GetString().Should().Be("high_lora_loader_0");
    }

    [Fact]
    public void Build_WithDualLora_ShouldAddBothLoraNodes()
    {
        var parameters = CreateDefaultParameters();
        parameters.Loras.Add(new Lora
        {
            Name = "dual_lora",
            HighPath = "loras/dual_high.safetensors",
            LowPath = "loras/dual_low.safetensors",
            Strength = 0.7f,
            IsEnabled = true
        });

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("high_lora_loader_0", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("low_lora_loader_0", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_NoLoras_ShouldChainModelDirectlyToSampling()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var highSamplingInputs = json.RootElement.GetProperty("high_model_sampling").GetProperty("inputs");
        highSamplingInputs.GetProperty("model")[0].GetString().Should().Be("high_torch");

        var lowSamplingInputs = json.RootElement.GetProperty("low_model_sampling").GetProperty("inputs");
        lowSamplingInputs.GetProperty("model")[0].GetString().Should().Be("low_torch");
    }

    #endregion

    #region Build - Frame Interpolation

    [Fact]
    public void Build_WithFrameInterpolation_ShouldAddInterpolationNodes()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("upscale_frames", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clean_upscale", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("frame_interpolation", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithFrameInterpolation_ShouldAdjustFrameRate()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: true, frameRate: 16, frameMultiplier: 2);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("frame_rate").GetInt32().Should().Be(32); // 16 * 2
    }

    [Fact]
    public void Build_WithFrameInterpolation_ShouldUseFramesOutput()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: true);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("images")[0].GetString().Should().Be("frame_interpolation");
    }

    [Fact]
    public void Build_WithoutFrameInterpolation_ShouldUseImageOutput()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: false);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("images")[0].GetString().Should().Be("vae_decoder");
    }

    [Fact]
    public void Build_WithoutFrameInterpolation_ShouldUseBaseFrameRate()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: false, frameRate: 24);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        var saveInputs = json.RootElement.GetProperty("video_output").GetProperty("inputs");
        saveInputs.GetProperty("frame_rate").GetInt32().Should().Be(24);
    }

    [Fact]
    public void Build_WithoutFrameInterpolation_ShouldNotContainInterpolationNodes()
    {
        var parameters = CreateDefaultParameters(frameInterpolationActive: false);

        var result = _workflow.Build(parameters);
        var json = JsonDocument.Parse(result.Json);

        json.RootElement.TryGetProperty("upscale_frames", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("clean_upscale", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("frame_interpolation", out _).Should().BeFalse();
    }

    #endregion

    #region Build - Registry

    [Fact]
    public void Build_ShouldReturnRegistryWithOutputs()
    {
        var parameters = CreateDefaultParameters();

        var result = _workflow.Build(parameters);

        result.Registry.Should().NotBeNull();
        result.Registry!.HasOutput("latent_output").Should().BeTrue();
        result.Registry.HasOutput("image_output").Should().BeTrue();
        result.Registry.HasOutput("vae_output").Should().BeTrue();
    }

    #endregion

    #region Helpers

    private static GenerationParameters CreateDefaultParameters(
        int steps = 8,
        long seed = 42,
        bool autoSplit = true,
        bool frameInterpolationActive = false,
        int frameRate = 16,
        int frameMultiplier = 2)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["HighModel"] = "wan22RemixT2VI2V_i2vHighV20.safetensors",
                ["LowModel"] = "wan22RemixT2VI2V_i2vLowV20.safetensors",
                ["Clip"] = "umt5_xxl_fp8_e4m3fn_scaled.safetensors",
                ["ClipVision"] = "clip_vision_h.safetensors",
                ["Vae"] = "wan_2.1_vae.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["source_image"] = new SourceAsset { Filename = "test_image.png", FilePath = "test_image.png" }
            }
        };

        // Sampler fragment
        var sampler = parameters.GetOrCreateFragment("sampler_advanced");
        sampler.SetValue("seed", seed);
        sampler.SetValue("steps", steps);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("sampler_name", "euler");
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("auto_split", autoSplit);

        // PainterI2V fragment
        var painter = parameters.GetOrCreateFragment("painter_i2v");
        painter.SetValue("length", 81);
        painter.SetValue("batch_size", 1);
        painter.SetValue("motion_amplitude", 1.1);
        painter.SetValue("shift", 5);
        painter.SetValue("frame_rate", frameRate);

        // Prompts fragment
        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a beautiful video");
        prompts.SetValue("negative", "");

        // WanLoadImage fragment
        var loadImage = parameters.GetOrCreateFragment("wan_load_image");
        loadImage.SetValue("width", 768);
        loadImage.SetValue("height", 768);

        // Frame interpolation fragment
        var interpolation = parameters.GetOrCreateFragment("frame_interpolation");
        interpolation.IsActive = frameInterpolationActive;
        interpolation.SetValue("rife_model", "rife49.pth");
        interpolation.SetValue("frame_multiplier", frameMultiplier);
        interpolation.SetValue("scale_by", 2.0);

        return parameters;
    }

    #endregion
}
