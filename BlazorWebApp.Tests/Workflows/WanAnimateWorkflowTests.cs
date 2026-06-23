using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;

namespace BlazorWebApp.Tests.Workflows;

public class WanAnimateWorkflowTests
{
    private readonly WanAnimateWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseWanAnimateDefaults()
    {
        var metadata = _workflow.Metadata;

        metadata.Title.Should().Be("Wan Animate");
        metadata.CompatibleResourceBaseModels.Should().Equal("Wan Video 2.2 I2V-A14B");
        metadata.UsesDualModelLoras.Should().BeFalse();
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Model" &&
            asset.DefaultValue == "Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Clip" &&
            asset.DefaultValue == "umt5_xxl_fp8_e4m3fn_scaled.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Vae" &&
            asset.DefaultValue == "wan_2.1_vae.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "RelightLora" &&
            asset.DefaultValue == "Wan/WanAnimate_relight_lora_fp16.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "SpeedLora" &&
            asset.DefaultValue == "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "DetailerModel" &&
            asset.DefaultValue == "anima-preview3-base.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "DetailerClip" &&
            asset.DefaultValue == "qwen_3_06b_base.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "DetailerVae" &&
            asset.DefaultValue == "qwen_image_vae.safetensors");

        metadata.Sources.Should().ContainSingle(source => source.Id == "reference_image");
        var motionSource = metadata.Sources.Should().ContainSingle(source => source.Id == "motion_video").Subject;
        motionSource.DefaultVideoOptions!.ForceRate.Should().Be(16);
        motionSource.DefaultVideoOptions.CustomWidth.Should().Be(0);
        motionSource.DefaultVideoOptions.CustomHeight.Should().Be(0);
        motionSource.DefaultVideoOptions.Format.Should().Be("AnimateDiff");

        var faceSource = metadata.Sources.Should().ContainSingle(source => source.Id == "external_face_video").Subject;
        faceSource.Label.Should().Be("Face Video");
        faceSource.Type.Should().Be(SourceType.Video);
        faceSource.Required.Should().BeFalse();
        faceSource.DefaultVideoOptions!.ForceRate.Should().Be(16);

        var backgroundVideoSource = metadata.Sources.Should().ContainSingle(source => source.Id == "external_background_video").Subject;
        backgroundVideoSource.Label.Should().Be("Background Video");
        backgroundVideoSource.Type.Should().Be(SourceType.Video);
        backgroundVideoSource.Required.Should().BeFalse();
        backgroundVideoSource.DefaultVideoOptions!.Format.Should().Be("AnimateDiff");

        var backgroundImageSource = metadata.Sources.Should().ContainSingle(source => source.Id == "external_background_image").Subject;
        backgroundImageSource.Label.Should().Be("Background Image");
        backgroundImageSource.Type.Should().Be(SourceType.Image);
        backgroundImageSource.Required.Should().BeFalse();
    }

    [Fact]
    public void GetFragments_ShouldExposeResolutionPanelBackedCeilingControls()
    {
        var fragments = _workflow.GetFragments().Select(fragment => fragment.Metadata).ToList();

        fragments.Select(fragment => fragment.Id).Should().ContainInOrder(
            "prompts",
            WanAnimateResolutionFragment.FragmentId,
            "main_sampler",
            WanVideoEnhanceFragment.FragmentId,
            FragmentKeys.Fragments.Detailer,
            "seed_vr2",
            "frame_interpolation",
            WanAnimateDiagnosticsFragment.FragmentId);

        var resolution = fragments.Single(fragment => fragment.Id == WanAnimateResolutionFragment.FragmentId);
        resolution.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Latent);
        resolution.Component.Should().Be("LatentForm");
        resolution.Parameters.Should().Contain(parameter => parameter.Name == "width" && (int)parameter.DefaultValue! == 832);
        resolution.Parameters.Should().Contain(parameter => parameter.Name == "height" && (int)parameter.DefaultValue! == 480);
        resolution.Parameters.Should().Contain(parameter => parameter.Name == "batch_size" && (int)parameter.DefaultValue! == 1);
    }

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var result = _workflow.Build(CreateParameters());

        var action = () => JsonDocument.Parse(result.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldResolveReferenceSizeAsLongerEdgeCeilingAndCropVideoToMatch()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(widthCeiling: 640, heightCeiling: 960)).Json);

        NodeType(json, "max_longer_edge").Should().Be("INTConstant");
        Inputs(json, "max_longer_edge").GetProperty("value").GetInt32().Should().Be(960);

        NodeType(json, "reference_longer_edge").Should().Be("ResizeImagesByLongerEdge");
        InputRef(Inputs(json, "reference_longer_edge"), "images").Should().Equal("load_reference_image", "0");
        InputRef(Inputs(json, "reference_longer_edge"), "longer_edge").Should().Equal("max_longer_edge", "0");

        var referenceResize = Inputs(json, "resize_reference_image");
        referenceResize.GetProperty("keep_proportion").GetString().Should().Be("resize");
        referenceResize.GetProperty("crop_position").GetString().Should().Be("center");
        referenceResize.GetProperty("divisible_by").GetInt32().Should().Be(16);
        InputRef(referenceResize, "width").Should().Equal("reference_ceiling_size", "1");
        InputRef(referenceResize, "height").Should().Equal("reference_ceiling_size", "2");

        var videoResize = Inputs(json, "resize_video");
        videoResize.GetProperty("keep_proportion").GetString().Should().Be("crop");
        videoResize.GetProperty("crop_position").GetString().Should().Be("center");
        videoResize.GetProperty("divisible_by").GetInt32().Should().Be(16);
        InputRef(videoResize, "width").Should().Equal("resize_reference_image", "1");
        InputRef(videoResize, "height").Should().Equal("resize_reference_image", "2");

        var animateInputs = Inputs(json, "animate_to_video");
        InputRef(animateInputs, "width").Should().Equal("resize_reference_image", "1");
        InputRef(animateInputs, "height").Should().Equal("resize_reference_image", "2");
        InputRef(animateInputs, "length").Should().Equal("load_video", "1");
    }

    [Fact]
    public void Build_ShouldEmitWanAnimateSourceNodesAndDefaults()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "model_loader").Should().Be("DiffusionModelLoaderKJ");
        Inputs(json, "model_loader").GetProperty("model_name").GetString().Should().Be("Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors");
        Inputs(json, "model_loader").GetProperty("weight_dtype").GetString().Should().Be("fp16");
        Inputs(json, "model_loader").GetProperty("sage_attention").GetString().Should().Be("auto");

        NodeType(json, "compiled_model").Should().Be("TorchCompileModelWanVideoV2");
        InputRef(Inputs(json, "compiled_model"), "model").Should().Equal("fixed_speed_lora", "0");

        NodeType(json, "clip_loader").Should().Be("CLIPLoader");
        Inputs(json, "clip_loader").GetProperty("type").GetString().Should().Be("wan");
        NodeType(json, "vae_loader").Should().Be("VAELoader");

        NodeType(json, "onnx_detection_loader").Should().Be("OnnxDetectionModelLoader");
        NodeType(json, "pose_face_detection").Should().Be("PoseAndFaceDetection");
        NodeType(json, "draw_vit_pose").Should().Be("DrawViTPose");
        NodeType(json, "sam2_loader").Should().Be("DownloadAndLoadSAM2Model");
        NodeType(json, "sam2_segmentation").Should().Be("Sam2Segmentation");
        NodeType(json, "grow_mask").Should().Be("GrowMaskWithBlur");
        Inputs(json, "grow_mask").GetProperty("incremental_expandrate").GetDouble().Should().Be(0.0);
        NodeType(json, "blockify_mask").Should().Be("BlockifyMask");
        NodeType(json, "background_image").Should().Be("DrawMaskOnImage");

        var animateInputs = Inputs(json, "animate_to_video");
        animateInputs.GetProperty("continue_motion_max_frames").GetInt32().Should().Be(5);
        animateInputs.GetProperty("video_frame_offset").GetInt32().Should().Be(0);
        animateInputs.GetProperty("batch_size").GetInt32().Should().Be(1);
        InputRef(animateInputs, "reference_image").Should().Equal("resize_reference_image", "0");
        InputRef(animateInputs, "face_video").Should().Equal("pose_face_detection", "1");
        InputRef(animateInputs, "pose_video").Should().Equal("draw_vit_pose", "0");
        InputRef(animateInputs, "background_video").Should().Equal("background_image", "0");
        InputRef(animateInputs, "character_mask").Should().Equal("blockify_mask", "0");

        NodeType(json, "sampler").Should().Be("SamplerCustomAdvanced");
        NodeType(json, "trim_video_latent").Should().Be("TrimVideoLatent");
        InputRef(Inputs(json, "trim_video_latent"), "samples").Should().Equal("sampler", "0");
        InputRef(Inputs(json, "trim_video_latent"), "trim_amount").Should().Equal("animate_to_video", "3");
        NodeType(json, "vae_decode").Should().Be("VAEDecode");

        json.RootElement.TryGetProperty("load_external_face_video", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("load_external_background_video", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("load_external_background_image", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("external_background_image_batch", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("masked_external_background_video", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("masked_external_background_image", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("wan_video_enhance", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("detailer_unet_loader", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("detailer_face_detailer", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithExternalFaceVideo_ShouldUseExternalFaceImagesOnly()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(externalFaceVideo: true)).Json);

        NodeType(json, "load_external_face_video").Should().Be("VHS_LoadVideo");
        var loadFaceInputs = Inputs(json, "load_external_face_video");
        loadFaceInputs.GetProperty("video").GetString().Should().Be("external-face.mp4");
        loadFaceInputs.GetProperty("force_rate").GetDouble().Should().Be(16);
        InputRef(loadFaceInputs, "custom_width").Should().Equal("resize_reference_image", "1");
        InputRef(loadFaceInputs, "custom_height").Should().Equal("resize_reference_image", "2");
        InputRef(loadFaceInputs, "frame_load_cap").Should().Equal("load_video", "1");

        NodeType(json, "external_pose_face_detection").Should().Be("PoseAndFaceDetection");
        var faceDetectionInputs = Inputs(json, "external_pose_face_detection");
        InputRef(faceDetectionInputs, "model").Should().Equal("onnx_detection_loader", "0");
        InputRef(faceDetectionInputs, "images").Should().Equal("load_external_face_video", "0");
        InputRef(faceDetectionInputs, "width").Should().Equal("resize_reference_image", "1");
        InputRef(faceDetectionInputs, "height").Should().Equal("resize_reference_image", "2");

        var animateInputs = Inputs(json, "animate_to_video");
        InputRef(animateInputs, "face_video").Should().Equal("external_pose_face_detection", "1");
        InputRef(animateInputs, "pose_video").Should().Equal("draw_vit_pose", "0");
        InputRef(animateInputs, "background_video").Should().Equal("background_image", "0");
    }

    [Fact]
    public void Build_WithExternalBackgroundVideo_ShouldMaskResizedBackgroundVideo()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(externalBackgroundVideo: true)).Json);

        NodeType(json, "load_external_background_video").Should().Be("VHS_LoadVideo");
        var loadBackgroundInputs = Inputs(json, "load_external_background_video");
        loadBackgroundInputs.GetProperty("video").GetString().Should().Be("external-background.mp4");
        InputRef(loadBackgroundInputs, "custom_width").Should().Equal("resize_reference_image", "1");
        InputRef(loadBackgroundInputs, "custom_height").Should().Equal("resize_reference_image", "2");
        InputRef(loadBackgroundInputs, "frame_load_cap").Should().Equal("load_video", "1");

        NodeType(json, "resize_external_background_video").Should().Be("ImageResizeKJv2");
        var resizeInputs = Inputs(json, "resize_external_background_video");
        resizeInputs.GetProperty("keep_proportion").GetString().Should().Be("crop");
        InputRef(resizeInputs, "image").Should().Equal("load_external_background_video", "0");
        InputRef(resizeInputs, "width").Should().Equal("resize_reference_image", "1");
        InputRef(resizeInputs, "height").Should().Equal("resize_reference_image", "2");

        NodeType(json, "masked_external_background_video").Should().Be("DrawMaskOnImage");
        var maskInputs = Inputs(json, "masked_external_background_video");
        InputRef(maskInputs, "image").Should().Equal("resize_external_background_video", "0");
        InputRef(maskInputs, "mask").Should().Equal("blockify_mask", "0");

        InputRef(Inputs(json, "animate_to_video"), "background_video").Should().Equal("masked_external_background_video", "0");
        json.RootElement.TryGetProperty("external_background_image_batch", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithExternalBackgroundImage_ShouldRepeatAndMaskImageToMotionFrameCount()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(externalBackgroundImage: true)).Json);

        NodeType(json, "load_external_background_image").Should().Be("LoadImage");
        Inputs(json, "load_external_background_image").GetProperty("image").GetString().Should().Be("external-background.png");

        NodeType(json, "resize_external_background_image").Should().Be("ImageResizeKJv2");
        var resizeInputs = Inputs(json, "resize_external_background_image");
        resizeInputs.GetProperty("keep_proportion").GetString().Should().Be("crop");
        InputRef(resizeInputs, "width").Should().Equal("resize_reference_image", "1");
        InputRef(resizeInputs, "height").Should().Equal("resize_reference_image", "2");

        NodeType(json, "external_background_image_batch").Should().Be("Batch Make (mtb)");
        var batchInputs = Inputs(json, "external_background_image_batch");
        InputRef(batchInputs, "image").Should().Equal("resize_external_background_image", "0");
        InputRef(batchInputs, "count").Should().Equal("load_video", "1");

        NodeType(json, "masked_external_background_image").Should().Be("DrawMaskOnImage");
        var maskInputs = Inputs(json, "masked_external_background_image");
        InputRef(maskInputs, "image").Should().Equal("external_background_image_batch", "0");
        InputRef(maskInputs, "mask").Should().Equal("blockify_mask", "0");

        InputRef(Inputs(json, "animate_to_video"), "background_video").Should().Equal("masked_external_background_image", "0");
    }

    [Fact]
    public void Build_WithBackgroundVideoAndImage_ShouldPreferVideo()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(externalBackgroundVideo: true, externalBackgroundImage: true)).Json);

        InputRef(Inputs(json, "animate_to_video"), "background_video").Should().Equal("masked_external_background_video", "0");
        json.RootElement.TryGetProperty("load_external_background_image", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("external_background_image_batch", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("masked_external_background_image", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithAdditionalLoras_ShouldChainAfterFixedLoras()
    {
        var parameters = CreateParameters();
        parameters.Loras.Add(new Lora
        {
            Name = "extra_wan_lora.safetensors",
            Path = "Wan/extra_wan_lora.safetensors",
            Strength = 0.7f,
            IsEnabled = true
        });

        using var json = JsonDocument.Parse(_workflow.Build(parameters).Json);

        NodeType(json, "fixed_relight_lora").Should().Be("LoraLoaderModelOnly");
        Inputs(json, "fixed_relight_lora").GetProperty("lora_name").GetString().Should().Be("Wan/WanAnimate_relight_lora_fp16.safetensors");
        Inputs(json, "fixed_relight_lora").GetProperty("strength_model").GetDouble().Should().Be(1.0);
        InputRef(Inputs(json, "fixed_relight_lora"), "model").Should().Equal("model_loader", "0");

        Inputs(json, "fixed_speed_lora").GetProperty("lora_name").GetString().Should().Be("Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors");
        Inputs(json, "fixed_speed_lora").GetProperty("strength_model").GetDouble().Should().BeApproximately(1.2, 0.0001);
        InputRef(Inputs(json, "fixed_speed_lora"), "model").Should().Equal("fixed_relight_lora", "0");

        Inputs(json, "user_lora_0").GetProperty("lora_name").GetString().Should().Be("Wan/extra_wan_lora.safetensors");
        Inputs(json, "user_lora_0").GetProperty("strength_model").GetDouble().Should().BeApproximately(0.7, 0.0001);
        InputRef(Inputs(json, "user_lora_0"), "model").Should().Equal("fixed_speed_lora", "0");
        InputRef(Inputs(json, "compiled_model"), "model").Should().Equal("user_lora_0", "0");
    }

    [Fact]
    public void Build_DefaultOutput_ShouldSaveGeneratedVideoOnly()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        var videoNodes = json.RootElement.EnumerateObject()
            .Where(node => node.Value.GetProperty("class_type").GetString() == "VHS_VideoCombine")
            .Select(node => node.Name)
            .ToList();

        videoNodes.Should().Equal("video_output");
        var outputInputs = Inputs(json, "video_output");
        InputRef(outputInputs, "images").Should().Equal("vae_decode", "0");
        InputRef(outputInputs, "audio").Should().Equal("load_video", "2");
        outputInputs.GetProperty("frame_rate").GetDouble().Should().Be(16);
        outputInputs.GetProperty("filename_prefix").GetString().Should().Be("WanAnimate");
        outputInputs.GetProperty("trim_to_audio").GetBoolean().Should().BeTrue();
        outputInputs.GetProperty("save_output").GetBoolean().Should().BeTrue();
        json.RootElement.TryGetProperty("diagnostic_video_output", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithSeedVr2Upscale_ShouldSaveUpscaledVideoFrames()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(seedVr2Active: true, seedVr2BatchSize: 0, frameLoadCap: 77)).Json);

        NodeType(json, "seedvr2_load_dit").Should().Be("SeedVR2LoadDiTModel");
        NodeType(json, "seedvr2_load_vae").Should().Be("SeedVR2LoadVAEModel");
        NodeType(json, "seedvr2_upscaler").Should().Be("SeedVR2VideoUpscaler");
        InputRef(Inputs(json, "seedvr2_upscaler"), "image").Should().Equal("vae_decode", "0");
        Inputs(json, "seedvr2_upscaler").GetProperty("batch_size").GetInt32().Should().Be(77);
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("seedvr2_upscaler", "0");
    }

    [Fact]
    public void Build_WithWanVideoEnhanceActive_ShouldPatchSamplerModel()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(wanVideoEnhanceActive: true, wanVideoEnhanceWeight: 1.75)).Json);

        NodeType(json, "wan_video_enhance").Should().Be("WanVideoEnhanceAVideoKJ");
        var enhanceInputs = Inputs(json, "wan_video_enhance");
        InputRef(enhanceInputs, "model").Should().Equal("compiled_model", "0");
        InputRef(enhanceInputs, "latent").Should().Equal("animate_to_video", "2");
        enhanceInputs.GetProperty("weight").GetDouble().Should().BeApproximately(1.75, 0.0001);

        InputRef(Inputs(json, "sampler_scheduler"), "model").Should().Equal("wan_video_enhance", "0");
        InputRef(Inputs(json, "sampler_guider"), "model").Should().Equal("wan_video_enhance", "0");
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("vae_decode", "0");
    }

    [Fact]
    public void Build_WithDetailerActive_ShouldRunDetailerOnDecodedVideoFrames()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(detailerActive: true)).Json);

        NodeType(json, "detailer_unet_loader").Should().Be("UNETLoader");
        Inputs(json, "detailer_unet_loader").GetProperty("unet_name").GetString().Should().Be("anima-preview3-base.safetensors");

        NodeType(json, "detailer_clip_loader").Should().Be("CLIPLoader");
        var clipInputs = Inputs(json, "detailer_clip_loader");
        clipInputs.GetProperty("clip_name").GetString().Should().Be("qwen_3_06b_base.safetensors");
        clipInputs.GetProperty("type").GetString().Should().Be("stable_diffusion");

        NodeType(json, "detailer_vae_loader").Should().Be("VAELoader");
        Inputs(json, "detailer_vae_loader").GetProperty("vae_name").GetString().Should().Be("qwen_image_vae.safetensors");
        NodeType(json, "detailer_bbox_provider").Should().Be("UltralyticsDetectorProvider");
        Inputs(json, "detailer_bbox_provider").GetProperty("model_name").GetString().Should().Be("bbox/face_yolov8m.pt");

        NodeType(json, "detailer_face_detailer").Should().Be("FaceDetailer");
        var detailerInputs = Inputs(json, "detailer_face_detailer");
        InputRef(detailerInputs, "image").Should().Equal("vae_decode", "0");
        InputRef(detailerInputs, "model").Should().Equal("detailer_lora_positive", "0");
        InputRef(detailerInputs, "clip").Should().Equal("detailer_lora_positive", "1");
        InputRef(detailerInputs, "vae").Should().Equal("detailer_vae_loader", "0");
        InputRef(detailerInputs, "positive").Should().Equal("detailer_encode_positive", "0");
        InputRef(detailerInputs, "negative").Should().Equal("detailer_encode_negative", "0");
        InputRef(detailerInputs, "bbox_detector").Should().Equal("detailer_bbox_provider", "0");
        detailerInputs.GetProperty("sampler_name").GetString().Should().Be("dpmpp_2m");
        detailerInputs.GetProperty("scheduler").GetString().Should().Be("simple");

        InputRef(Inputs(json, "video_output"), "images").Should().Equal("detailer_face_detailer", "0");
    }

    [Fact]
    public void Build_WithDetailerAndSeedVr2_ShouldUpscaleDetailedFrames()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(detailerActive: true, seedVr2Active: true, frameLoadCap: 77)).Json);

        InputRef(Inputs(json, "detailer_face_detailer"), "image").Should().Equal("vae_decode", "0");
        InputRef(Inputs(json, "seedvr2_upscaler"), "image").Should().Equal("detailer_face_detailer", "0");
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("seedvr2_upscaler", "0");
    }

    [Fact]
    public void Build_WithFrameInterpolation_ShouldInterpolateFramesAndAdjustFrameRate()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(frameRate: 18, frameInterpolationActive: true, frameMultiplier: 3)).Json);

        NodeType(json, "upscale_frames").Should().Be("ImageScaleBy");
        NodeType(json, "clean_upscale").Should().Be("easy cleanGpuUsed");
        NodeType(json, "frame_interpolation").Should().Be("RIFE VFI");
        InputRef(Inputs(json, "upscale_frames"), "image").Should().Equal("vae_decode", "0");
        Inputs(json, "frame_interpolation").GetProperty("multiplier").GetInt32().Should().Be(3);
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(54);
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("frame_interpolation", "0");
    }

    [Fact]
    public void Build_WithSeedVr2AndFrameInterpolation_ShouldChainUpscaleIntoInterpolation()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(seedVr2Active: true, frameInterpolationActive: true, frameMultiplier: 2)).Json);

        InputRef(Inputs(json, "seedvr2_upscaler"), "image").Should().Equal("vae_decode", "0");
        InputRef(Inputs(json, "upscale_frames"), "image").Should().Equal("seedvr2_upscaler", "0");
        InputRef(Inputs(json, "video_output"), "images").Should().Equal("frame_interpolation", "0");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(32);
    }

    [Fact]
    public void Build_WithDiagnosticsEnabled_ShouldAddDiagnosticCollageOutput()
    {
        var parameters = CreateParameters(diagnosticsActive: true);

        using var json = JsonDocument.Parse(_workflow.Build(parameters).Json);

        NodeType(json, "diagnostic_inputs").Should().Be("ImageConcatMulti");
        NodeType(json, "diagnostic_collage").Should().Be("ImageConcatMulti");
        NodeType(json, "diagnostic_video_output").Should().Be("VHS_VideoCombine");
        Inputs(json, "diagnostic_video_output").GetProperty("filename_prefix").GetString().Should().Be("WanAnimate_Diagnostic");
        Inputs(json, "diagnostic_video_output").GetProperty("save_output").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Build_WhenVideoOptionsChanged_ShouldWireLoadVideoAndFrameRate()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(frameRate: 24, customWidth: 960, customHeight: 544, frameLoadCap: 77, skipFirstFrames: 3, selectEveryNth: 2)).Json);

        var loadVideoInputs = Inputs(json, "load_video");
        loadVideoInputs.GetProperty("force_rate").GetDouble().Should().Be(24);
        loadVideoInputs.GetProperty("custom_width").GetInt32().Should().Be(960);
        loadVideoInputs.GetProperty("custom_height").GetInt32().Should().Be(544);
        loadVideoInputs.GetProperty("frame_load_cap").GetInt32().Should().Be(77);
        loadVideoInputs.GetProperty("skip_first_frames").GetInt32().Should().Be(3);
        loadVideoInputs.GetProperty("select_every_nth").GetInt32().Should().Be(2);
        loadVideoInputs.GetProperty("format").GetString().Should().Be("AnimateDiff");
        Inputs(json, "video_output").GetProperty("frame_rate").GetDouble().Should().Be(24);
    }

    private static GenerationParameters CreateParameters(
        int widthCeiling = 832,
        int heightCeiling = 480,
        int frameRate = 16,
        int customWidth = 0,
        int customHeight = 0,
        int frameLoadCap = 0,
        int skipFirstFrames = 0,
        int selectEveryNth = 1,
        bool diagnosticsActive = false,
        bool seedVr2Active = false,
        int seedVr2BatchSize = 1,
        bool wanVideoEnhanceActive = false,
        double wanVideoEnhanceWeight = 2.0,
        bool frameInterpolationActive = false,
        int frameMultiplier = 2,
        bool detailerActive = false,
        bool externalFaceVideo = false,
        bool externalBackgroundVideo = false,
        bool externalBackgroundImage = false)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["Model"] = "Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors",
                ["Clip"] = "umt5_xxl_fp8_e4m3fn_scaled.safetensors",
                ["Vae"] = "wan_2.1_vae.safetensors",
                ["RelightLora"] = "Wan/WanAnimate_relight_lora_fp16.safetensors",
                ["SpeedLora"] = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors",
                ["DetailerModel"] = "anima-preview3-base.safetensors",
                ["DetailerClip"] = "qwen_3_06b_base.safetensors",
                ["DetailerVae"] = "qwen_image_vae.safetensors"
            },
            Sources = new Dictionary<string, SourceAsset>
            {
                ["reference_image"] = new() { Filename = "reference.png", Width = 1024, Height = 768 },
                ["motion_video"] = new()
                {
                    Filename = "motion.mp4",
                    Width = 1920,
                    Height = 1080,
                    VideoOptions = new VideoSourceOptions
                    {
                        ForceRate = frameRate,
                        CustomWidth = customWidth,
                        CustomHeight = customHeight,
                        FrameLoadCap = frameLoadCap,
                        SkipFirstFrames = skipFirstFrames,
                        SelectEveryNth = selectEveryNth,
                        Format = "AnimateDiff"
                    }
                }
            }
        };

        if (externalFaceVideo)
        {
            parameters.Sources["external_face_video"] = new SourceAsset
            {
                Filename = "external-face.mp4",
                Width = 960,
                Height = 544,
                VideoOptions = CreateVideoOptions(frameRate)
            };
        }

        if (externalBackgroundVideo)
        {
            parameters.Sources["external_background_video"] = new SourceAsset
            {
                Filename = "external-background.mp4",
                Width = 1280,
                Height = 720,
                VideoOptions = CreateVideoOptions(frameRate)
            };
        }

        if (externalBackgroundImage)
        {
            parameters.Sources["external_background_image"] = new SourceAsset
            {
                Filename = "external-background.png",
                Width = 1920,
                Height = 1080
            };
        }

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "woman dancing");
        prompts.SetValue("negative", "blurry face");

        var resolution = parameters.GetOrCreateFragment(WanAnimateResolutionFragment.FragmentId);
        resolution.SetValue("width", widthCeiling);
        resolution.SetValue("height", heightCeiling);
        resolution.SetValue("batch_size", 1);

        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", "lcm");
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("steps", 4);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("seed", 42L);

        var diagnostics = parameters.GetOrCreateFragment(WanAnimateDiagnosticsFragment.FragmentId);
        diagnostics.IsActive = diagnosticsActive;

        var wanVideoEnhance = parameters.GetOrCreateFragment(WanVideoEnhanceFragment.FragmentId);
        wanVideoEnhance.IsActive = wanVideoEnhanceActive;
        wanVideoEnhance.SetValue("weight", wanVideoEnhanceWeight);

        var seedVr2 = parameters.GetOrCreateFragment("seed_vr2");
        seedVr2.IsActive = seedVr2Active;
        seedVr2.SetValue("seedvr2_model", "seedvr2_ema_7b-Q4_K_M.gguf");
        seedVr2.SetValue("seedvr2_vae_model", "ema_vae_fp16.safetensors");
        seedVr2.SetValue("seedvr2_seed", 42L);
        seedVr2.SetValue("seedvr2_resolution", 2048);
        seedVr2.SetValue("seedvr2_batch_size", seedVr2BatchSize);

        var frameInterpolation = parameters.GetOrCreateFragment("frame_interpolation");
        frameInterpolation.IsActive = frameInterpolationActive;
        frameInterpolation.SetValue("rife_model", "rife49.pth");
        frameInterpolation.SetValue("frame_multiplier", frameMultiplier);
        frameInterpolation.SetValue("scale_by", 2.0);

        if (detailerActive)
        {
            var detailer = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Detailer);
            detailer.IsActive = true;
            detailer.SetValue("detailer_detection_model", "bbox/face_yolov8m.pt");
            detailer.SetValue("detailer_sampler", "dpmpp_2m");
            detailer.SetValue("detailer_scheduler", "simple");
            detailer.SetValue("detailer_seed", 42L);
            detailer.SetValue("detailer_steps", 20);
            detailer.SetValue("detailer_cfg", 4.0);
            detailer.SetValue("detailer_denoise", 0.65);
            detailer.SetValue("detailer_feather", 5);
            detailer.SetValue("detailer_bbox_threshold", 0.7);
            detailer.SetValue("detailer_bbox_dilation", 10);
            detailer.SetValue("detailer_bbox_crop_factor", 3.0);
            detailer.SetValue("detailer_drop_size", 70);
            detailer.SetValue("detailer_guide_size", 512);
            detailer.SetValue("detailer_max_size", 1024);
            detailer.SetValue("detailer_cycle", 1);
        }

        return parameters;
    }

    private static VideoSourceOptions CreateVideoOptions(int frameRate)
    {
        return new VideoSourceOptions
        {
            ForceRate = frameRate,
            CustomWidth = 0,
            CustomHeight = 0,
            FrameLoadCap = 0,
            SkipFirstFrames = 0,
            SelectEveryNth = 1,
            Format = "AnimateDiff"
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

    private static string[] InputRef(JsonElement inputs, string inputName)
    {
        return inputs.GetProperty(inputName).EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Number ? value.GetInt32().ToString() : value.GetString()!)
            .ToArray();
    }
}