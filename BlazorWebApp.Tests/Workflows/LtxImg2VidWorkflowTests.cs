using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Ltx;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

public class LtxImg2VidWorkflowTests
{
    private readonly LtxImg2VidWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeRecommendedI2VAssets()
    {
        var assets = _workflow.Metadata.Assets.ToList();

        assets.Should().HaveCount(6);
        assets.Should().Contain(a => a.Parameter == "Checkpoint"
            && a.Type == WfAssetType.CheckpointModel
            && a.DefaultValue == "ltx-2.3-22b-dev-fp8.safetensors");
        assets.Should().Contain(a => a.Parameter == "Clip"
            && a.Type == WfAssetType.Clip
            && a.DefaultValue == "gemma_3_12B_it_fp4_mixed.safetensors");
        assets.Should().Contain(a => a.Parameter == "AvProjectionCkpt"
            && a.Type == WfAssetType.CheckpointModel
            && a.DefaultValue == "ltx-2.3-22b-dev-fp8.safetensors");
        assets.Should().Contain(a => a.Parameter == "AudioVae"
            && a.Type == WfAssetType.CheckpointModel
            && a.DefaultValue == "ltx-2.3-22b-dev-fp8.safetensors");
        assets.Should().Contain(a => a.Parameter == "UpscaleModel"
            && a.Type == WfAssetType.LatentUpscaleModel
            && a.DefaultValue == "ltx-2.3-spatial-upscaler-x2-1.0.safetensors");
        assets.Should().Contain(a => a.Parameter == "PreviewVae"
            && a.Type == WfAssetType.Vae
            && a.DefaultValue == "taeltx2_3.safetensors");
    }

    [Fact]
    public void Metadata_ShouldRequireSourceImage()
    {
        var source = _workflow.Metadata.Sources.Should().ContainSingle().Subject;

        source.Id.Should().Be("source_image");
        source.Type.Should().Be(WfSourceType.Image);
        source.Required.Should().BeTrue();
    }

    [Fact]
    public void GetFragments_ShouldUseLatentFormForI2VResolution()
    {
        var fragments = _workflow.GetFragments().ToList();
        var resolution = fragments.Single(f => f.Metadata.Id == "ltx_load_image").Metadata;
        var videoSettings = fragments.Single(f => f.Metadata.Id == "ltx_video_settings").Metadata;

        resolution.Component.Should().Be("LatentForm");
        resolution.Parameters.Should().Contain(p => p.Name == "width");
        resolution.Parameters.Should().Contain(p => p.Name == "height");
        resolution.Parameters.Should().Contain(p => p.Name == "batch_size");
        videoSettings.Parameters.Should().NotContain(p => p.Name == "width");
        videoSettings.Parameters.Should().NotContain(p => p.Name == "height");
    }

    [Fact]
    public void Build_ShouldUseSelectedWorkflowAssets()
    {
        var parameters = CreateParameters();

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        root.GetProperty("checkpoint_loader").GetProperty("inputs").GetProperty("ckpt_name").GetString()
            .Should().Be("selected-checkpoint.safetensors");
        root.GetProperty("text_encoder_loader").GetProperty("inputs").GetProperty("text_encoder").GetString()
            .Should().Be("selected-gemma.safetensors");
        root.GetProperty("text_encoder_loader").GetProperty("inputs").GetProperty("ckpt_name").GetString()
            .Should().Be("selected-av-projection.safetensors");
        root.GetProperty("audio_vae_loader").GetProperty("class_type").GetString()
            .Should().Be("LTXVAudioVAELoader");
        root.GetProperty("audio_vae_loader").GetProperty("inputs").GetProperty("ckpt_name").GetString()
            .Should().Be("selected-audio-vae.safetensors");
        root.GetProperty("upscale_model_loader").GetProperty("inputs").GetProperty("model_name").GetString()
            .Should().Be("selected-upscaler.safetensors");
        root.GetProperty("preview_vae_loader").GetProperty("inputs").GetProperty("vae_name").GetString()
            .Should().Be("selected-preview-vae.safetensors");
        root.GetProperty("ltx_preview_override").GetProperty("inputs").GetProperty("preview_rate").GetInt32()
            .Should().Be(12);
    }

    [Fact]
    public void Build_ShouldUseLatentFormResolutionAndBatchSize()
    {
        var parameters = CreateParameters(width: 1408, height: 768, batchSize: 3, duration: 2, frameRate: 12);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);
        var root = json.RootElement;

        var videoInputs = root.GetProperty("empty_ltx_latent_video").GetProperty("inputs");
        videoInputs.GetProperty("width").GetInt32().Should().Be(704);
        videoInputs.GetProperty("height").GetInt32().Should().Be(384);
        videoInputs.GetProperty("length").GetInt32().Should().Be(25);
        videoInputs.GetProperty("batch_size").GetInt32().Should().Be(3);

        var audioInputs = root.GetProperty("empty_ltx_latent_audio").GetProperty("inputs");
        audioInputs.GetProperty("frames_number").GetInt32().Should().Be(25);
        audioInputs.GetProperty("frame_rate").GetInt32().Should().Be(12);
        audioInputs.GetProperty("batch_size").GetInt32().Should().Be(3);
    }

    private static GenerationParameters CreateParameters(
        int width = 1280,
        int height = 720,
        int batchSize = 1,
        int duration = 5,
        int frameRate = 12)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Checkpoint"] = "selected-checkpoint.safetensors",
                ["Clip"] = "selected-gemma.safetensors",
                ["AvProjectionCkpt"] = "selected-av-projection.safetensors",
                ["AudioVae"] = "selected-audio-vae.safetensors",
                ["UpscaleModel"] = "selected-upscaler.safetensors",
                ["PreviewVae"] = "selected-preview-vae.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["source_image"] = new() { Filename = "source.webp", FilePath = "source.webp" }
            }
        };

        var image = parameters.GetOrCreateFragment("ltx_load_image");
        image.SetValue("width", width);
        image.SetValue("height", height);
        image.SetValue("batch_size", batchSize);

        var video = parameters.GetOrCreateFragment("ltx_video_settings");
        video.SetValue("duration", duration);
        video.SetValue("frame_rate", frameRate);
        video.SetValue("img_compression", 38);
        video.SetValue("i2v_strength", 0.8);

        var sampler = parameters.GetOrCreateFragment("ltx_sampler");
        sampler.SetValue("seed", 42L);
        sampler.SetValue("cfg", 1.0);

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a cinematic test clip");
        prompts.SetValue("negative", "low quality");

        return parameters;
    }
}
