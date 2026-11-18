using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;
using WorkflowAssetType = BlazorWebApp.Workflows.Models.AssetType;

namespace BlazorWebApp.Tests.Workflows;

public class WanOneToAllAnimationWorkflowTests
{
    private readonly WanOneToAllAnimationWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseApprovedOneToAllDefaults()
    {
        var metadata = _workflow.Metadata;

        metadata.Title.Should().Be("One-To-All Animation");
        metadata.Description.Should().NotBeNullOrWhiteSpace();
        metadata.CompatibleResourceBaseModels.Should().Equal("Wan Video 14B i2v 720p");
        metadata.UsesDualModelLoras.Should().BeFalse();
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Model" && asset.Type == WorkflowAssetType.DiffusionModel &&
            asset.DefaultValue == "Wan21-OneToAllAnimation_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "TextEncoder" && asset.Type == WorkflowAssetType.Clip &&
            asset.DefaultValue == "umt5_xxl_fp8_e4m3fn_scaled.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Vae" && asset.Type == WorkflowAssetType.Vae &&
            asset.DefaultValue == "wan_2.1_vae.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "SpeedLora" && asset.Type == WorkflowAssetType.Lora &&
            asset.DefaultValue == "Speed/lightx2v_T2V_14B_cfg_step_distill_v2_lora_rank128_bf16.safetensors");

        metadata.Sources.Should().ContainSingle(source => source.Id == "reference_image" && source.Type == SourceType.Image && source.Required);
        var motionSource = metadata.Sources.Should().ContainSingle(source => source.Id == "motion_video").Subject;
        motionSource.Type.Should().Be(SourceType.Video);
        motionSource.Required.Should().BeTrue();
        motionSource.DefaultVideoOptions!.ForceRate.Should().Be(24);
        motionSource.DefaultVideoOptions.Format.Should().Be("Wan");
    }

    [Fact]
    public void GetFragments_ShouldExposeOneToAllControlsAndInterpolation()
    {
        var fragmentIds = _workflow.GetFragments().Select(fragment => fragment.Metadata.Id).ToList();

        fragmentIds.Should().ContainInOrder(
            "prompts",
            WanOneToAllControlsFragment.FragmentId,
            WanOneToAllPoseFragment.FragmentId,
            WanOneToAllEmbedsFragment.FragmentId,
            WanOneToAllSamplerFragment.FragmentId,
            "frame_interpolation");

        _workflow.GetFragments().Single(fragment => fragment.Metadata.Id == WanOneToAllControlsFragment.FragmentId)
            .Metadata.Component.Should().Be("WanOneToAllControlsForm");
    }

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var result = _workflow.Build(CreateParameters());

        var action = () => JsonDocument.Parse(result.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldResolveReferenceByLongerEdgeAndResizeVideoToMatch()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(width: 640, height: 960)).Json);

        NodeType(json, "max_longer_edge").Should().Be("INTConstant");
        Inputs(json, "max_longer_edge").GetProperty("value").GetInt32().Should().Be(960);
        NodeType(json, "reference_longer_edge").Should().Be("ResizeImagesByLongerEdge");
        InputRef(Inputs(json, "reference_longer_edge"), "images").Should().Equal("load_reference_image", "0");

        var referenceResize = Inputs(json, "resize_reference_image");
        referenceResize.GetProperty("keep_proportion").GetString().Should().Be("resize");
        referenceResize.GetProperty("divisible_by").GetInt32().Should().Be(16);
        InputRef(referenceResize, "width").Should().Equal("reference_ceiling_size", "1");
        InputRef(referenceResize, "height").Should().Equal("reference_ceiling_size", "2");

        var videoResize = Inputs(json, "resize_video");
        videoResize.GetProperty("keep_proportion").GetString().Should().Be("crop");
        videoResize.GetProperty("divisible_by").GetInt32().Should().Be(16);
        InputRef(videoResize, "width").Should().Equal("resize_reference_image", "1");
        InputRef(videoResize, "height").Should().Equal("resize_reference_image", "2");

        InputRef(Inputs(json, "pose_detection"), "width").Should().Equal("resize_reference_image", "1");
        InputRef(Inputs(json, "pose_detection"), "height").Should().Equal("resize_reference_image", "2");
    }

    [Fact]
    public void Build_ShouldUseApprovedLoaderSubstitutionsAndBlockSwap()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        Inputs(json, "model_loader").GetProperty("model").GetString().Should().Be("Wan21-OneToAllAnimation_fp8_e4m3fn_scaled_KJ.safetensors");
        Inputs(json, "model_loader").GetProperty("attention_mode").GetString().Should().Be("sageattn");
        Inputs(json, "block_swap").GetProperty("blocks_to_swap").GetInt32().Should().Be(25);
        Inputs(json, "t5_loader").GetProperty("model_name").GetString().Should().Be("umt5_xxl_fp8_e4m3fn_scaled.safetensors");
        Inputs(json, "speed_lora").GetProperty("lora").GetString().Should().Be("Speed/lightx2v_T2V_14B_cfg_step_distill_v2_lora_rank128_bf16.safetensors");
        Inputs(json, "speed_lora").GetProperty("low_mem_load").GetBoolean().Should().BeTrue();
        InputRef(Inputs(json, "set_loras"), "lora").Should().Equal("speed_lora", "0");
    }

    [Fact]
    public void Build_ShouldEmitOneToAllLoopAndFinalAudioOutput()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "loop_start").Should().Be("easy forLoopStart");
        NodeType(json, "loop_end").Should().Be("easy forLoopEnd");
        NodeType(json, "extend_embeds").Should().Be("WanVideoAddOneToAllExtendEmbeds");
        NodeType(json, "loop_pose_embeds").Should().Be("WanVideoAddOneToAllPoseEmbeds");
        NodeType(json, "sampler").Should().Be("WanVideoSampler");
        NodeType(json, "decode").Should().Be("WanVideoDecode");

        InputRef(Inputs(json, "sampler"), "scheduler").Should().Equal("scheduler", "3");
        InputRef(Inputs(json, "sampler"), "image_embeds").Should().Equal("select_image_embeds", "0");
        InputRef(Inputs(json, "loop_end"), "initial_value1").Should().Equal("select_output_images", "0");
        InputRef(Inputs(json, "final_image_range"), "num_frames").Should().Equal("load_video", "1");

        var output = Inputs(json, "video_output");
        output.GetProperty("frame_rate").GetDouble().Should().Be(24);
        output.GetProperty("trim_to_audio").GetBoolean().Should().BeFalse();
        output.GetProperty("format").GetString().Should().Be("video/h264-mp4");
        InputRef(output, "audio").Should().Equal("load_video", "2");
        InputRef(output, "images").Should().Equal("final_image_range", "0");
    }

    [Fact]
    public void Build_WithFrameInterpolation_ShouldSaveInterpolatedFramesAndMultiplyFrameRate()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(frameInterpolationActive: true, frameMultiplier: 3)).Json);

        NodeType(json, "frame_interpolation").Should().Be("RIFE VFI");
        Inputs(json, "frame_interpolation").GetProperty("multiplier").GetInt32().Should().Be(3);
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("frame_interpolation", "0");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(72);
    }

    private static GenerationParameters CreateParameters(int width = 480, int height = 832, bool frameInterpolationActive = false, int frameMultiplier = 2)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Model"] = "Wan21-OneToAllAnimation_fp8_e4m3fn_scaled_KJ.safetensors",
                ["TextEncoder"] = "umt5_xxl_fp8_e4m3fn_scaled.safetensors",
                ["Vae"] = "wan_2.1_vae.safetensors",
                ["SpeedLora"] = "Speed/lightx2v_T2V_14B_cfg_step_distill_v2_lora_rank128_bf16.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["reference_image"] = new() { Filename = "reference.png", Width = 720, Height = 1280 },
                ["motion_video"] = new()
                {
                    Filename = "motion.mp4",
                    Width = 720,
                    Height = 1280,
                    VideoOptions = new VideoSourceOptions
                    {
                        ForceRate = 24,
                        CustomWidth = 0,
                        CustomHeight = 0,
                        FrameLoadCap = 0,
                        SkipFirstFrames = 0,
                        SelectEveryNth = 1,
                        Format = "Wan"
                    }
                }
            }
        };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "woman dancing");
        prompts.SetValue("negative", "blurry face");

        var controls = parameters.GetOrCreateFragment(WanOneToAllControlsFragment.FragmentId);
        controls.SetValue("width", width);
        controls.SetValue("height", height);
        controls.SetValue("window", 81);
        controls.SetValue("overlap", 5);
        controls.SetValue("fps", 24);
        controls.SetValue("blocks_to_swap", 25);

        var pose = parameters.GetOrCreateFragment(WanOneToAllPoseFragment.FragmentId);
        pose.SetValue("vitpose_model", "vitpose-l-wholebody.onnx");
        pose.SetValue("yolo_model", "yolov10m.onnx");
        pose.SetValue("onnx_device", "CUDAExecutionProvider");
        pose.SetValue("align_to", "ref");
        pose.SetValue("draw_face_points", "full");
        pose.SetValue("draw_head", "full");

        var embeds = parameters.GetOrCreateFragment(WanOneToAllEmbedsFragment.FragmentId);
        embeds.SetValue("ref_strength", 1.0);
        embeds.SetValue("ref_start_percent", 0.0);
        embeds.SetValue("ref_end_percent", 1.0);
        embeds.SetValue("init_pose_strength", 1.0);
        embeds.SetValue("init_pose_cfg_scale", 1.96);
        embeds.SetValue("loop_pose_strength", 1.0);
        embeds.SetValue("loop_pose_cfg_scale", 1.5);
        embeds.SetValue("pose_start_percent", 0.0);
        embeds.SetValue("pose_end_percent", 1.0);

        var sampler = parameters.GetOrCreateFragment(WanOneToAllSamplerFragment.FragmentId);
        sampler.SetValue("scheduler", "unipc");
        sampler.SetValue("scheduler_steps", 2);
        sampler.SetValue("sampler_steps", 8);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("shift", 7.0);
        sampler.SetValue("seed", 0L);
        sampler.SetValue("force_offload", true);

        var interpolation = parameters.GetOrCreateFragment("frame_interpolation");
        interpolation.IsActive = frameInterpolationActive;
        interpolation.SetValue("rife_model", "rife49.pth");
        interpolation.SetValue("frame_multiplier", frameMultiplier);
        interpolation.SetValue("scale_by", 2.0);

        return parameters;
    }

    private static string NodeType(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("class_type").GetString()!;
    }

    private static JsonElement Inputs(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("inputs");
    }

    private static string[] InputRef(JsonElement inputs, string inputName)
    {
        return inputs.GetProperty(inputName).EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Number ? value.GetInt32().ToString() : value.GetString()!)
            .ToArray();
    }
}