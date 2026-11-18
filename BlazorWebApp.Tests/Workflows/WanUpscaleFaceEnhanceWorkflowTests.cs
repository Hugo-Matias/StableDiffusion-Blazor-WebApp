using System.Text.Json;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

public class WanUpscaleFaceEnhanceWorkflowTests
{
    private readonly WanUpscaleFaceEnhanceWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseWanUtilityDefaults()
    {
        var metadata = _workflow.Metadata;

        metadata.Title.Should().Be("Upscale + Face Enhance");
        metadata.Base.Should().Be(BlazorWebApp.Data.Enums.ModelBase.Wan);
        metadata.Mode.Should().Be(ModeType.Vid2Vid);
        metadata.CompatibleResourceBaseModels.Should().Equal("Wan Video 2.2 T2V-A14B");
        metadata.UsesDualModelLoras.Should().BeFalse();

        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "LowModel" && asset.Type == WfAssetType.DiffusionModel);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Vae" && asset.Type == WfAssetType.Vae);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "SpeedLora" && asset.Type == WfAssetType.Lora);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "TextEncoder" && asset.Type == WfAssetType.Clip);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "UpscaleModel" && asset.Type == WfAssetType.UpscaleModel);
        metadata.Sources.Should().ContainSingle(source => source.Id == "source_video" && source.Type == WfSourceType.Video && source.Required);
    }

    [Fact]
    public void GetFragments_ShouldExposeSettingsAndToggleableFaceEnhance()
    {
        var fragments = _workflow.GetFragments().Select(fragment => fragment.Metadata).ToList();

        fragments.Select(fragment => fragment.Id).Should().Equal(
            WanUpscaleFaceEnhanceSettingsFragment.FragmentId,
            WanFaceEnhancePassFragment.FragmentId);

        fragments[0].Component.Should().Be("WanUpscaleFaceEnhanceSettingsForm");
        fragments[1].Component.Should().Be("WanFaceEnhancePassForm");
        fragments[1].DefaultActive.Should().BeTrue();
        fragments[1].Parameters.Should().Contain(parameter => parameter.Name == "crop_resolution" && (int)parameter.DefaultValue! == 768);
    }

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var action = () => JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldEmitSourceLoaderAndSingleLowModelStack()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "load_video").Should().Be("VHS_LoadVideo");
        Inputs(json, "load_video").GetProperty("video").GetString().Should().Be("source.mp4");
        Inputs(json, "load_video").GetProperty("frame_load_cap").GetInt32().Should().Be(81);
        Inputs(json, "load_video").GetProperty("format").GetString().Should().Be("None");

        NodeType(json, "model_loader").Should().Be("WanVideoModelLoader");
        Inputs(json, "model_loader").GetProperty("model").GetString().Should().Be("Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors");
        Inputs(json, "model_loader").GetProperty("quantization").GetString().Should().Be("fp8_e4m3fn_scaled");
        Inputs(json, "model_loader").GetProperty("attention_mode").GetString().Should().Be("sageattn");

        NodeType(json, "block_swap").Should().Be("WanVideoBlockSwap");
        Inputs(json, "block_swap").GetProperty("blocks_to_swap").GetInt32().Should().Be(27);
        Inputs(json, "block_swap").GetProperty("use_non_blocking").GetBoolean().Should().BeFalse();

        NodeType(json, "lora_select").Should().Be("WanVideoLoraSelect");
        Inputs(json, "lora_select").GetProperty("lora").GetString().Should().Be("Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors");
        Inputs(json, "lora_select").GetProperty("strength").GetDouble().Should().Be(0.7);

        NodeType(json, "vae_loader").Should().Be("WanVideoVAELoader");
        Inputs(json, "vae_loader").GetProperty("model_name").GetString().Should().Be("wan_2.1_vae.safetensors");
    }

    [Fact]
    public void Build_ShouldEmitUpscaleAndFinalOnlySavePath()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "upscale_pixel_upscale").Should().Be("CRT_UpscaleModelAdv");
        InputRef(Inputs(json, "upscale_pixel_upscale"), "image").Should().Be(("load_video", 0));
        Inputs(json, "upscale_pixel_upscale").GetProperty("upscale_model_name").GetString().Should().Be("Remacri (foolhardy).pth");

        NodeType(json, "upscale_text_encode").Should().Be("WanVideoTextEncodeCached");
        Inputs(json, "upscale_text_encode").GetProperty("model_name").GetString().Should().Be("umt5_xxl_fp16.safetensors");

        NodeType(json, "upscale_caption").Should().Be("Florence2Run");
        Inputs(json, "upscale_caption").GetProperty("seed").GetInt64().Should().Be(1);

        NodeType(json, "upscale_sampler").Should().Be("WanVideoSampler");
        InputRef(Inputs(json, "upscale_sampler"), "model").Should().Be(("set_loras", 0));
        InputRef(Inputs(json, "upscale_sampler"), "samples").Should().Be(("upscale_encode", 0));
        InputRef(Inputs(json, "upscale_decode"), "samples").Should().Be(("upscale_sampler", 1));

        NodeType(json, "final_video_output").Should().Be("VHS_VideoCombine");
        Inputs(json, "final_video_output").GetProperty("filename_prefix").GetString().Should().Be("Upscale + FaceEnhance");
        InputRef(Inputs(json, "final_video_output"), "images").Should().Be(("face_uncrop", 0));
        json.RootElement.TryGetProperty("upscale_video_output", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WhenFaceEnhanceDisabled_ShouldSaveUpscaleOutputDirectly()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(faceActive: false)).Json);

        json.RootElement.TryGetProperty("face_uncrop", out _).Should().BeFalse();
        InputRef(Inputs(json, "final_video_output"), "images").Should().Be(("upscale_decode", 0));
    }

    [Fact]
    public void Build_ShouldUseFaceEnhanceDefaultsWhenEnabled()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "face_primary_clipseg").Should().Be("BatchCLIPSeg");
        Inputs(json, "face_primary_clipseg").GetProperty("threshold").GetDouble().Should().Be(0.25);
        NodeType(json, "face_crop_resize").Should().Be("ImageResize+");
        Inputs(json, "face_crop_resize").GetProperty("width").GetInt32().Should().Be(768);
        NodeType(json, "face_sampler").Should().Be("WanVideoSampler");
        InputRef(Inputs(json, "face_sampler"), "text_embeds").Should().Be(("upscale_nag", 0));
        InputRef(Inputs(json, "face_decode"), "samples").Should().Be(("face_sampler", 1));
        NodeType(json, "face_uncrop").Should().Be("BatchUncropAdvanced");
    }

    private static GenerationParameters CreateParameters(bool faceActive = true)
    {
        return new GenerationParameters
        {
            Assets = new Dictionary<string, string>(),
            Sources = new Dictionary<string, SourceAsset>
            {
                ["source_video"] = new()
                {
                    Type = "video",
                    Filename = "source.mp4"
                }
            },
            Fragments = new Dictionary<string, FragmentParameters>
            {
                [WanUpscaleFaceEnhanceSettingsFragment.FragmentId] = new()
                {
                    IsActive = true,
                    Values = new Dictionary<string, object?>()
                },
                [WanFaceEnhancePassFragment.FragmentId] = new()
                {
                    IsActive = faceActive,
                    Values = new Dictionary<string, object?>()
                }
            }
        };
    }

    private static string NodeType(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("class_type").GetString()!;
    }

    private static JsonElement Inputs(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("inputs");
    }

    private static (string nodeId, int outputIndex) InputRef(JsonElement inputs, string inputName)
    {
        var input = inputs.GetProperty(inputName);
        return (input[0].GetString()!, input[1].GetInt32());
    }
}