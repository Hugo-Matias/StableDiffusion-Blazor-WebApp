using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;

namespace BlazorWebApp.Tests.Workflows;

public class WanSCAILEmbedsWorkflowTests
{
    private readonly WanSCAILEmbedsWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseLiveScailDefaults()
    {
        var metadata = _workflow.Metadata;

        metadata.Description.Should().NotBeNullOrWhiteSpace();
        metadata.CompatibleResourceBaseModels.Should().Equal("Wan Video 14B i2v 480p");
        metadata.UsesDualModelLoras.Should().BeFalse();
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Model" &&
            asset.DefaultValue == "Wan21-14B-SCAIL-preview_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "TextEncoder" &&
            asset.DefaultValue == "umt5_xxl_fp16.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Vae" &&
            asset.DefaultValue == "wan_2.1_vae.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "SpeedLora" &&
            asset.DefaultValue == "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors");
        metadata.Assets.Should().NotContain(asset => asset.Parameter == "Controlnet");

        var motionSource = metadata.Sources.Should().ContainSingle(source => source.Id == "motion_video").Subject;
        motionSource.DefaultVideoOptions!.ForceRate.Should().Be(0);
        motionSource.DefaultVideoOptions.FrameLoadCap.Should().Be(0);
        motionSource.DefaultVideoOptions.SelectEveryNth.Should().Be(1);
        motionSource.DefaultVideoOptions.Format.Should().Be("AnimateDiff");
    }

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var result = _workflow.Build(CreateParameters());

        var action = () => JsonDocument.Parse(result.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void GetFragments_ShouldExposeEnhancementFragments()
    {
        var fragmentIds = _workflow.GetFragments()
            .Select(fragment => fragment.Metadata.Id)
            .ToList();

        fragmentIds.Should().ContainInOrder(
            "prompts",
            "scail_sampler",
            "scail_embeds",
            "scail_pose_detection",
            "seed_vr2",
            "frame_interpolation");
    }

    [Fact]
    public void GetFragments_ShouldAllowSeedVr2BatchSizeZeroForAutoFrameCount()
    {
        var seedVr2 = _workflow.GetFragments()
            .Single(fragment => fragment.Metadata.Id == "seed_vr2");

        var batchSize = seedVr2.Metadata.Parameters
            .Single(parameter => parameter.Name == "seedvr2_batch_size");

        batchSize.Min.Should().Be(0);
        batchSize.DefaultValue.Should().Be(1);
    }

    [Fact]
    public void Build_ShouldUseLiveImageResizeDefaultsForMotionVideoAndReference()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "target_width").Should().Be("INTConstant");
        Inputs(json, "target_width").GetProperty("value").GetInt32().Should().Be(512);
        NodeType(json, "target_height").Should().Be("INTConstant");
        Inputs(json, "target_height").GetProperty("value").GetInt32().Should().Be(896);

        NodeType(json, "resize_video").Should().Be("ImageResizeKJv2");
        var videoResize = Inputs(json, "resize_video");
        videoResize.GetProperty("upscale_method").GetString().Should().Be("lanczos");
        videoResize.GetProperty("keep_proportion").GetString().Should().Be("crop");
        videoResize.GetProperty("pad_color").GetString().Should().Be("0, 0, 0");
        videoResize.GetProperty("crop_position").GetString().Should().Be("center");
        videoResize.GetProperty("divisible_by").GetInt32().Should().Be(32);
        videoResize.GetProperty("device").GetString().Should().Be("cpu");
        InputRef(videoResize, "image").Should().Equal("load_video", "0");
        InputRef(videoResize, "width").Should().Equal("target_width", "0");
        InputRef(videoResize, "height").Should().Equal("target_height", "0");

        var refResize = Inputs(json, "resize_ref_image");
        refResize.GetProperty("pad_color").GetString().Should().Be("255,255,255");
        refResize.GetProperty("divisible_by").GetInt32().Should().Be(32);
        InputRef(refResize, "width").Should().Equal("target_width", "0");
        InputRef(refResize, "height").Should().Equal("target_height", "0");

        NodeType(json, "video_size").Should().Be("GetImageSizeAndCount");
        InputRef(Inputs(json, "video_size"), "image").Should().Equal("resize_video", "0");
        NodeType(json, "render_width").Should().Be("SimpleCalculatorKJ");
        NodeType(json, "render_height").Should().Be("SimpleCalculatorKJ");
        InputRef(Inputs(json, "render_width"), "a").Should().Equal("target_width", "0");
        InputRef(Inputs(json, "render_height"), "a").Should().Equal("target_height", "0");
        InputRef(Inputs(json, "empty_embeds"), "width").Should().Equal("resize_ref_image", "1");
        InputRef(Inputs(json, "empty_embeds"), "height").Should().Equal("resize_ref_image", "2");
        InputRef(Inputs(json, "empty_embeds"), "num_frames").Should().Equal("load_video", "1");
    }

    [Fact]
    public void Build_ShouldNotEmitRemovedResizeOrUni3CNodes()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        var nodeTypes = json.RootElement.EnumerateObject()
            .Select(node => node.Value.GetProperty("class_type").GetString())
            .ToList();

        nodeTypes.Should().NotContain("LayerUtility: ImageScaleByAspectRatio V2");
        nodeTypes.Should().NotContain("DWPreprocessor");
        nodeTypes.Should().NotContain("ConvertOpenPoseKeypointsToDWPose");
        nodeTypes.Should().NotContain("WanVideoUni3C_ControlnetLoader");
        nodeTypes.Should().NotContain("WanVideoUni3C_embeds");
        nodeTypes.Should().NotContain("WanVideoEncode");
    }

    [Fact]
    public void Build_ShouldWireScailEmbedsAndContextOptionsToSamplerExtraArgs()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "pose_embeds").Should().Be("WanVideoAddSCAILPoseEmbeds");
        NodeType(json, "extra_args").Should().Be("WanVideoSamplerExtraArgs");

        var poseInputs = Inputs(json, "pose_embeds");
        poseInputs.GetProperty("strength").GetDouble().Should().Be(1);
        poseInputs.GetProperty("start_percent").GetDouble().Should().Be(0);
        poseInputs.GetProperty("end_percent").GetDouble().Should().Be(0.5);
        InputRef(Inputs(json, "sampler"), "image_embeds").Should().Equal("pose_embeds", "0");

        var extraArgs = Inputs(json, "extra_args");
        extraArgs.GetProperty("riflex_freq_index").GetInt32().Should().Be(0);
        extraArgs.GetProperty("rope_function").GetString().Should().Be("comfy");
        InputRef(extraArgs, "context_options").Should().Equal("context_options", "0");
        extraArgs.TryGetProperty("uni3c_embeds", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldUseLiveSchemaNamesForScailNodes()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        var modelInputs = Inputs(json, "model_loader");
        modelInputs.GetProperty("model").GetString().Should().Be("Wan21-14B-SCAIL-preview_fp8_e4m3fn_scaled_KJ.safetensors");
        modelInputs.GetProperty("attention_mode").GetString().Should().Be("sageattn");
        InputRef(modelInputs, "compile_args").Should().Equal("compile_settings", "0");
        InputRef(modelInputs, "block_swap_args").Should().Equal("block_swap", "0");

        var blockSwapInputs = Inputs(json, "block_swap");
        blockSwapInputs.GetProperty("blocks_to_swap").GetInt32().Should().Be(23);
        blockSwapInputs.GetProperty("use_non_blocking").GetBoolean().Should().BeFalse();

        var loraInputs = Inputs(json, "lora_select");
        loraInputs.GetProperty("lora").GetString().Should().Be("Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors");
        loraInputs.GetProperty("strength").GetDouble().Should().Be(1);

        var textInputs = Inputs(json, "text_encode");
        NodeType(json, "text_encode").Should().Be("WanVideoTextEncodeCached");
        textInputs.GetProperty("model_name").GetString().Should().Be("umt5_xxl_fp16.safetensors");
        textInputs.GetProperty("precision").GetString().Should().Be("bf16");
        textInputs.GetProperty("quantization").GetString().Should().Be("disabled");
        textInputs.GetProperty("use_disk_cache").GetBoolean().Should().BeFalse();
        textInputs.GetProperty("device").GetString().Should().Be("gpu");

        var vaeInputs = Inputs(json, "vae_loader");
        vaeInputs.GetProperty("model_name").GetString().Should().Be("wan_2.1_vae.safetensors");
        vaeInputs.GetProperty("verbose").GetBoolean().Should().BeFalse();

        var onnxInputs = Inputs(json, "onnx_detection_loader");
        onnxInputs.GetProperty("vitpose_model").GetString().Should().Be("vitpose-l-wholebody.onnx");
        onnxInputs.GetProperty("yolo_model").GetString().Should().Be("yolov10m.onnx");
        onnxInputs.GetProperty("onnx_device").GetString().Should().Be("CUDAExecutionProvider");

        NodeType(json, "vitpose_detect").Should().Be("PoseDetectionVitPoseToDWPose");
        InputRef(Inputs(json, "vitpose_detect"), "images").Should().Equal("resize_video", "0");
        NodeType(json, "ref_vitpose_detect").Should().Be("PoseDetectionVitPoseToDWPose");
        InputRef(Inputs(json, "ref_vitpose_detect"), "images").Should().Equal("resize_ref_image", "0");

        var renderInputs = Inputs(json, "render_poses");
        renderInputs.GetProperty("draw_face").GetBoolean().Should().BeTrue();
        renderInputs.GetProperty("draw_hands").GetBoolean().Should().BeTrue();
        renderInputs.GetProperty("render_device").GetString().Should().Be("gpu");
        renderInputs.GetProperty("scale_hands").GetBoolean().Should().BeTrue();
        renderInputs.GetProperty("render_backend").GetString().Should().Be("taichi");
        InputRef(renderInputs, "dw_poses").Should().Equal("vitpose_detect", "0");
        InputRef(renderInputs, "ref_dw_pose").Should().Equal("ref_vitpose_detect", "0");
    }

    [Fact]
    public void Build_WithAdditionalLoras_ShouldChainWanLoraSelectorsAfterSpeedLora()
    {
        var parameters = CreateParameters();
        parameters.Loras.Add(new Lora
        {
            Name = "first_wan_lora.safetensors",
            Path = "Wan/first_wan_lora.safetensors",
            Strength = 0.75f,
            IsEnabled = true
        });
        parameters.Loras.Add(new Lora
        {
            Name = "second_wan_lora.safetensors",
            Strength = 0.45f,
            IsEnabled = true
        });

        using var json = JsonDocument.Parse(_workflow.Build(parameters).Json);

        var speedInputs = Inputs(json, "lora_select");
        speedInputs.TryGetProperty("prev_lora", out _).Should().BeFalse();
        speedInputs.GetProperty("lora").GetString().Should().Be("Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors");

        var firstLoraInputs = Inputs(json, "lora_select_0");
        NodeType(json, "lora_select_0").Should().Be("WanVideoLoraSelect");
        firstLoraInputs.GetProperty("lora").GetString().Should().Be("Wan/first_wan_lora.safetensors");
        firstLoraInputs.GetProperty("strength").GetDouble().Should().BeApproximately(0.75, 0.0001);
        InputRef(firstLoraInputs, "prev_lora").Should().Equal("lora_select", "0");

        var secondLoraInputs = Inputs(json, "lora_select_1");
        NodeType(json, "lora_select_1").Should().Be("WanVideoLoraSelect");
        secondLoraInputs.GetProperty("lora").GetString().Should().Be("second_wan_lora.safetensors");
        secondLoraInputs.GetProperty("strength").GetDouble().Should().BeApproximately(0.45, 0.0001);
        InputRef(secondLoraInputs, "prev_lora").Should().Equal("lora_select_0", "0");
        InputRef(Inputs(json, "set_loras"), "lora").Should().Equal("lora_select_1", "0");
    }

    [Fact]
    public void Build_WhenPromptsMissing_ShouldUseLivePromptDefaults()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(setPrompts: false)).Json);

        var textInputs = Inputs(json, "text_encode");
        textInputs.GetProperty("positive_prompt").GetString().Should().Be("Woman dancing");
        textInputs.GetProperty("negative_prompt").GetString().Should().Contain("色调艳丽");
        textInputs.GetProperty("negative_prompt").GetString().Should().Contain("倒着走");
    }

    [Fact]
    public void Build_WhenRuntimeOptionsChanged_ShouldWireFrameSelectionAndTargetSize()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(
            frameLoadCap: 81,
            skipFirstFrames: 12,
            selectEveryNth: 2,
            targetWidth: 640,
            targetHeight: 960)).Json);

        var loadVideoInputs = Inputs(json, "load_video");
        loadVideoInputs.GetProperty("frame_load_cap").GetInt32().Should().Be(81);
        loadVideoInputs.GetProperty("skip_first_frames").GetInt32().Should().Be(12);
        loadVideoInputs.GetProperty("select_every_nth").GetInt32().Should().Be(2);
        Inputs(json, "target_width").GetProperty("value").GetInt32().Should().Be(640);
        Inputs(json, "target_height").GetProperty("value").GetInt32().Should().Be(960);
    }

    [Fact]
    public void Build_WhenSourceVideoOptionsChanged_ShouldWireLoadVideoNode()
    {
        var parameters = CreateParameters(frameLoadCap: 81, skipFirstFrames: 12, selectEveryNth: 2);
        parameters.Sources["motion_video"].VideoOptions = new VideoSourceOptions
        {
            ForceRate = 24,
            CustomWidth = 640,
            CustomHeight = 960,
            FrameLoadCap = 48,
            SkipFirstFrames = 6,
            SelectEveryNth = 3,
            Format = "Wan"
        };

        using var json = JsonDocument.Parse(_workflow.Build(parameters).Json);

        var loadVideoInputs = Inputs(json, "load_video");
        loadVideoInputs.GetProperty("force_rate").GetDouble().Should().Be(24);
        loadVideoInputs.GetProperty("custom_width").GetInt32().Should().Be(640);
        loadVideoInputs.GetProperty("custom_height").GetInt32().Should().Be(960);
        loadVideoInputs.GetProperty("frame_load_cap").GetInt32().Should().Be(48);
        loadVideoInputs.GetProperty("skip_first_frames").GetInt32().Should().Be(6);
        loadVideoInputs.GetProperty("select_every_nth").GetInt32().Should().Be(3);
        loadVideoInputs.GetProperty("format").GetString().Should().Be("Wan");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(24);
    }

    [Fact]
    public void Build_WhenHandDetectionDisabled_ShouldDisableRenderedHands()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(detectHand: false)).Json);

        var renderInputs = Inputs(json, "render_poses");
        renderInputs.GetProperty("draw_hands").GetBoolean().Should().BeFalse();
        renderInputs.GetProperty("scale_hands").GetBoolean().Should().BeFalse();
        InputRef(renderInputs, "dw_poses").Should().Equal("vitpose_detect", "0");
        InputRef(renderInputs, "ref_dw_pose").Should().Equal("ref_vitpose_detect", "0");
    }

    [Fact]
    public void Build_WhenDwPoseAlignmentDisabled_ShouldOmitOptionalPoseAlignmentInputs()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(useDwPoseAlignment: false)).Json);

        var renderInputs = Inputs(json, "render_poses");
        renderInputs.TryGetProperty("dw_poses", out _).Should().BeFalse();
        renderInputs.TryGetProperty("ref_dw_pose", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldSaveDecodedVideoWithLiveOutputDefaults()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "output_size").Should().Be("GetImageSizeAndCount");
        InputRef(Inputs(json, "output_size"), "image").Should().Equal("decode", "0");

        var videoInputs = Inputs(json, "video_output");
        InputRef(videoInputs, "images").Should().Equal("output_size", "0");
        videoInputs.TryGetProperty("audio", out _).Should().BeFalse();
        videoInputs.GetProperty("frame_rate").GetDouble().Should().Be(16);
        videoInputs.GetProperty("pix_fmt").GetString().Should().Be("yuv420p");
        videoInputs.GetProperty("crf").GetInt32().Should().Be(19);
        videoInputs.GetProperty("save_metadata").GetBoolean().Should().BeTrue();
        videoInputs.GetProperty("trim_to_audio").GetBoolean().Should().BeFalse();
        videoInputs.GetProperty("save_output").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Build_WithSeedVr2Upscale_ShouldSaveUpscaledFrames()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(seedVr2Active: true)).Json);

        NodeType(json, "seedvr2_load_dit").Should().Be("SeedVR2LoadDiTModel");
        NodeType(json, "seedvr2_load_vae").Should().Be("SeedVR2LoadVAEModel");
        NodeType(json, "seedvr2_upscaler").Should().Be("SeedVR2VideoUpscaler");
        InputRef(Inputs(json, "seedvr2_upscaler"), "image").Should().Equal("decode", "0");
        InputRef(Inputs(json, "output_size"), "image").Should().Equal("seedvr2_upscaler", "0");
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("output_size", "0");
    }

    [Fact]
    public void Build_WithSeedVr2BatchSizeZero_ShouldResolveBatchSizeToFrameCount()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(
            frameLoadCap: 48,
            seedVr2Active: true,
            seedVr2BatchSize: 0)).Json);

        Inputs(json, "seedvr2_upscaler").GetProperty("batch_size").GetInt32().Should().Be(48);
    }

    [Fact]
    public void Build_WithFrameInterpolation_ShouldInterpolateFramesAndAdjustFrameRate()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(
            frameInterpolationActive: true,
            frameRate: 18,
            frameMultiplier: 3)).Json);

        NodeType(json, "upscale_frames").Should().Be("ImageScaleBy");
        NodeType(json, "clean_upscale").Should().Be("easy cleanGpuUsed");
        NodeType(json, "frame_interpolation").Should().Be("RIFE VFI");
        InputRef(Inputs(json, "upscale_frames"), "image").Should().Equal("decode", "0");
        InputRef(Inputs(json, "output_size"), "image").Should().Equal("frame_interpolation", "0");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(54);
    }

    [Fact]
    public void Build_WithSeedVr2AndFrameInterpolation_ShouldChainUpscaleIntoInterpolation()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(
            seedVr2Active: true,
            frameInterpolationActive: true,
            frameMultiplier: 2)).Json);

        InputRef(Inputs(json, "seedvr2_upscaler"), "image").Should().Equal("decode", "0");
        InputRef(Inputs(json, "upscale_frames"), "image").Should().Equal("seedvr2_upscaler", "0");
        InputRef(Inputs(json, "output_size"), "image").Should().Equal("frame_interpolation", "0");
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("output_size", "0");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(32);
    }

    private static GenerationParameters CreateParameters(
        bool detectHand = true,
        bool useDwPoseAlignment = true,
        int frameLoadCap = 0,
        int skipFirstFrames = 0,
        int selectEveryNth = 1,
        int targetWidth = 512,
        int targetHeight = 896,
        bool setPrompts = true,
        bool seedVr2Active = false,
        int seedVr2BatchSize = 1,
        bool frameInterpolationActive = false,
        int frameRate = 16,
        int frameMultiplier = 2)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Model"] = "Wan21-14B-SCAIL-preview_fp8_e4m3fn_scaled_KJ.safetensors",
                ["TextEncoder"] = "umt5_xxl_fp16.safetensors",
                ["ClipVision"] = "clip_vision_h.safetensors",
                ["Vae"] = "wan_2.1_vae.safetensors",
                ["SpeedLora"] = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["motion_video"] = new() { Filename = "motion.mp4", Width = 480, Height = 1216 },
                ["ref_image"] = new() { Filename = "reference.png", Width = 1024, Height = 1024 }
            }
        };

        if (setPrompts)
        {
            var prompts = parameters.GetOrCreateFragment("prompts");
            prompts.SetValue("positive", "Woman dancing");
            prompts.SetValue("negative", "blurry");
        }

        var sampler = parameters.GetOrCreateFragment("scail_sampler");
        sampler.SetValue("seed", 123L);
        sampler.SetValue("frame_rate", frameRate);

        var poseDetection = parameters.GetOrCreateFragment("scail_pose_detection");
        poseDetection.SetValue("detect_hand", detectHand);
        poseDetection.SetValue("frame_load_cap", frameLoadCap);
        poseDetection.SetValue("skip_first_frames", skipFirstFrames);
        poseDetection.SetValue("select_every_nth", selectEveryNth);
        poseDetection.SetValue("target_width", targetWidth);
        poseDetection.SetValue("target_height", targetHeight);

        var poseRendering = parameters.GetOrCreateFragment("scail_pose_rendering");
        poseRendering.SetValue("use_dw_pose_alignment", useDwPoseAlignment);

        var seedVr2 = parameters.GetOrCreateFragment("seed_vr2");
        seedVr2.IsActive = seedVr2Active;
        seedVr2.SetValue("seedvr2_model", "seedvr2_ema_7b-Q4_K_M.gguf");
        seedVr2.SetValue("seedvr2_vae_model", "ema_vae_fp16.safetensors");
        seedVr2.SetValue("seedvr2_seed", 123L);
        seedVr2.SetValue("seedvr2_resolution", 2048);
        seedVr2.SetValue("seedvr2_batch_size", seedVr2BatchSize);

        var frameInterpolation = parameters.GetOrCreateFragment("frame_interpolation");
        frameInterpolation.IsActive = frameInterpolationActive;
        frameInterpolation.SetValue("rife_model", "rife49.pth");
        frameInterpolation.SetValue("frame_multiplier", frameMultiplier);
        frameInterpolation.SetValue("scale_by", 2.0);

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
