using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using SourceAsset = BlazorWebApp.Models.SourceAsset;
using VideoSourceOptions = BlazorWebApp.Models.VideoSourceOptions;

namespace BlazorWebApp.Workflows.Templates.Wan;

/// <summary>
/// Native Wan Animate motion transfer workflow.
/// Transfers motion from a driving video onto a reference image using Wan Animate preprocessing.
/// </summary>
public class WanAnimateWorkflow : IWorkflowBuilder
{
    private const string DefaultModel = "Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors";
    private const string DefaultClip = "umt5_xxl_fp8_e4m3fn_scaled.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultRelightLora = "Wan/WanAnimate_relight_lora_fp16.safetensors";
    private const string DefaultSpeedLora = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors";
    private const string DefaultDetailerModel = "anima-preview3-base.safetensors";
    private const string DefaultDetailerClip = "qwen_3_06b_base.safetensors";
    private const string DefaultDetailerVae = "qwen_image_vae.safetensors";
    private const string DefaultSam2Model = "sam2.1_hiera_base_plus.safetensors";
    private const string DefaultVitPoseModel = "vitpose-l-wholebody.onnx";
    private const string DefaultYoloModel = "yolov10m.onnx";
    private const string DefaultPositivePrompt = "woman dancing";
    private const int DefaultWidthCeiling = 832;
    private const int DefaultHeightCeiling = 480;
    private const int DefaultFrameRate = 16;
    private const string ReferenceImageSourceId = "reference_image";
    private const string MotionVideoSourceId = "motion_video";
    private const string ExternalFaceVideoSourceId = "external_face_video";
    private const string ExternalBackgroundVideoSourceId = "external_background_video";
    private const string ExternalBackgroundImageSourceId = "external_background_image";

    private readonly PromptsFragment _promptsFragment = new();
    private readonly WanAnimateResolutionFragment _resolutionFragment = new();
    private readonly LoadDiffusionWithPromptsFragment _detailerModelFragment = new();
    private readonly LoraLoaderFragment _detailerLoraFragment = new();
    private readonly DetailerFragment _detailerFragment = new();
    private readonly WanVideoEnhanceFragment _wanVideoEnhanceFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();
    private readonly FrameInterpolationFragment _frameInterpolationFragment = new();
    private readonly BasicSchedulerSamplerCustomAdvancedFragment _samplerFragment = new()
    {
        Defaults = new()
        {
            SamplerId = "sampler",
            Title = "SamplerCustomAdvanced",
            SamplerName = "lcm",
            Scheduler = "simple",
            Steps = 4,
            Cfg = 1.0,
            Denoise = 1.0,
            Seed = 42
        }
    };
    private readonly WanAnimateDiagnosticsFragment _diagnosticsFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Wan Animate",
        Description = "Transfers motion from a driving video onto a reference image with the native Wan Animate preprocessing stack. Pick this for Wan 2.2 Animate motion transfer when you want a single generated video output by default.",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Animate Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultModel,
                Order = 1,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = DefaultClip,
                Order = 2,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
                Order = 3,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "RelightLora",
                Label = "Relight LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultRelightLora,
                Order = 4,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "SpeedLora",
                Label = "Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultSpeedLora,
                Order = 5,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "DetailerModel",
                Label = "Detailer Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultDetailerModel,
                Order = 6,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "DetailerClip",
                Label = "Detailer CLIP",
                Type = AssetType.Clip,
                DefaultValue = DefaultDetailerClip,
                Order = 7,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "DetailerVae",
                Label = "Detailer VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultDetailerVae,
                Order = 8,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = ReferenceImageSourceId,
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = true
            },
            new WorkflowSource
            {
                Id = MotionVideoSourceId,
                Label = "Motion Video",
                Type = SourceType.Video,
                Required = true,
                DefaultVideoOptions = new VideoSourceOptions
                {
                    ForceRate = DefaultFrameRate,
                    CustomWidth = 0,
                    CustomHeight = 0,
                    FrameLoadCap = 0,
                    SkipFirstFrames = 0,
                    SelectEveryNth = 1,
                    Format = "AnimateDiff"
                }
            },
            new WorkflowSource
            {
                Id = ExternalBackgroundImageSourceId,
                Label = "Background Image",
                Type = SourceType.Image,
                Required = false
            },
            new WorkflowSource
            {
                Id = ExternalBackgroundVideoSourceId,
                Label = "Background Video",
                Type = SourceType.Video,
                Required = false,
                DefaultVideoOptions = CreateDefaultOptionalVideoOptions()
            },
            new WorkflowSource
            {
                Id = ExternalFaceVideoSourceId,
                Label = "Face Video",
                Type = SourceType.Video,
                Required = false,
                DefaultVideoOptions = CreateDefaultOptionalVideoOptions()
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 2.2 I2V-A14B"],
        UsesDualModelLoras = false
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _resolutionFragment;
        yield return _samplerFragment;
        yield return _wanVideoEnhanceFragment;
        yield return _detailerFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _frameInterpolationFragment;
        yield return _diagnosticsFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment(_promptsFragment.Metadata.Id);
        var resolutionFragment = parameters.GetFragment(_resolutionFragment.Metadata.Id);
        var diagnosticsFragment = parameters.GetFragment(_diagnosticsFragment.Metadata.Id);
        var referenceSource = parameters.Sources?.GetValueOrDefault(ReferenceImageSourceId);
        var videoSource = parameters.Sources?.GetValueOrDefault(MotionVideoSourceId);
        var externalFaceVideoSource = parameters.Sources?.GetValueOrDefault(ExternalFaceVideoSourceId);
        var externalBackgroundVideoSource = parameters.Sources?.GetValueOrDefault(ExternalBackgroundVideoSourceId);
        var externalBackgroundImageSource = parameters.Sources?.GetValueOrDefault(ExternalBackgroundImageSourceId);
        var videoOptions = videoSource?.VideoOptions;

        var widthCeiling = resolutionFragment?.GetInt("width", DefaultWidthCeiling) ?? DefaultWidthCeiling;
        var heightCeiling = resolutionFragment?.GetInt("height", DefaultHeightCeiling) ?? DefaultHeightCeiling;
        var maxLongerEdge = Math.Max(widthCeiling, heightCeiling);
        var batchSize = resolutionFragment?.GetInt("batch_size", 1) ?? 1;
        var outputFrameRate = videoOptions?.ForceRate > 0 ? videoOptions.ForceRate : DefaultFrameRate;
        var positivePrompt = GetPromptOrDefault(promptsFragment, "positive", DefaultPositivePrompt);
        var negativePrompt = promptsFragment?.GetString("negative", string.Empty) ?? string.Empty;

        BuildSources(builder, registry, GetSourcePath(referenceSource), GetSourcePath(videoSource), videoOptions, maxLongerEdge);
        BuildPreprocessing(builder, registry);
        BuildOptionalSourceOverrides(builder, registry, externalFaceVideoSource, externalBackgroundVideoSource, externalBackgroundImageSource, videoOptions);
        BuildModel(builder, registry, parameters);
        BuildConditioning(builder, registry, positivePrompt);
        BuildAnimateLatent(builder, registry, batchSize);
        _wanVideoEnhanceFragment.Build(builder, parameters, registry);
        _samplerFragment.Build(builder, parameters, registry);
        BuildDecode(builder, registry);
        ApplyDetailer(builder, registry, parameters, positivePrompt, negativePrompt, widthCeiling, heightCeiling, batchSize);
        var finalFrameRate = ApplyEnhancements(builder, registry, parameters, videoOptions?.FrameLoadCap ?? 0, outputFrameRate);
        BuildMainOutput(builder, registry, finalFrameRate);

        if (diagnosticsFragment?.IsActive == true)
        {
            BuildDiagnosticOutput(builder, registry, finalFrameRate);
        }

        return builder.ToComfyWorkflow(registry);
    }

    private static void BuildSources(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string referenceImage,
        string motionVideo,
        VideoSourceOptions? videoOptions,
        int maxLongerEdge)
    {
        builder.AddNode("max_longer_edge", node => node
            .Type("INTConstant")
            .Title("Max Longer Edge")
            .Input("value", maxLongerEdge));

        builder.AddNode("load_reference_image", node => node
            .Type("LoadImage")
            .Title("Load Reference Image")
            .Input("image", referenceImage));

        builder.AddNode("reference_longer_edge", node => node
            .Type("ResizeImagesByLongerEdge")
            .Title("Reference Longer Edge Ceiling")
            .InputFromNode("images", "load_reference_image", 0)
            .InputFromNode("longer_edge", "max_longer_edge", 0));

        builder.AddNode("reference_ceiling_size", node => node
            .Type("GetImageSizeAndCount")
            .Title("Reference Ceiling Size")
            .InputFromNode("image", "reference_longer_edge", 0));

        builder.AddNode("resize_reference_image", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Reference Image")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "resize")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", "reference_longer_edge", 0)
            .InputFromNode("width", "reference_ceiling_size", 1)
            .InputFromNode("height", "reference_ceiling_size", 2));

        builder.AddNode("load_video", node => node
            .Type("VHS_LoadVideo")
            .Title("Load Motion Video")
            .Input("video", motionVideo)
            .Input("force_rate", videoOptions?.ForceRate ?? DefaultFrameRate)
            .Input("custom_width", videoOptions?.CustomWidth ?? 0)
            .Input("custom_height", videoOptions?.CustomHeight ?? 0)
            .Input("frame_load_cap", videoOptions?.FrameLoadCap ?? 0)
            .Input("skip_first_frames", videoOptions?.SkipFirstFrames ?? 0)
            .Input("select_every_nth", videoOptions?.SelectEveryNth ?? 1)
            .Input("format", string.IsNullOrWhiteSpace(videoOptions?.Format) ? "AnimateDiff" : videoOptions.Format));

        builder.AddNode("resize_video", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Motion Video")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", "load_video", 0)
            .InputFromNode("width", "resize_reference_image", 1)
            .InputFromNode("height", "resize_reference_image", 2));

        builder.AddNode("video_size", node => node
            .Type("GetImageSizeAndCount")
            .Title("Motion Video Size And Count")
            .InputFromNode("image", "resize_video", 0));

        registry.Register("reference_image", "resize_reference_image", 0);
        registry.Register("input_video", "resize_video", 0);
        registry.Register("width", "resize_reference_image", 1);
        registry.Register("height", "resize_reference_image", 2);
        registry.Register("frame_count", "load_video", 1);
        registry.Register("audio", "load_video", 2);
    }

    private static void BuildOptionalSourceOverrides(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        SourceAsset? externalFaceVideoSource,
        SourceAsset? externalBackgroundVideoSource,
        SourceAsset? externalBackgroundImageSource,
        VideoSourceOptions? fallbackVideoOptions)
    {
        if (TryGetSourcePath(externalFaceVideoSource, out var externalFaceVideo))
        {
            BuildExternalFaceVideo(builder, registry, externalFaceVideo, externalFaceVideoSource?.VideoOptions ?? fallbackVideoOptions);
        }

        if (TryGetSourcePath(externalBackgroundVideoSource, out var externalBackgroundVideo))
        {
            BuildExternalBackgroundVideo(builder, registry, externalBackgroundVideo, externalBackgroundVideoSource?.VideoOptions ?? fallbackVideoOptions);
        }
        else if (TryGetSourcePath(externalBackgroundImageSource, out var externalBackgroundImage))
        {
            BuildExternalBackgroundImage(builder, registry, externalBackgroundImage);
        }
    }

    private static void BuildExternalFaceVideo(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string videoPath,
        VideoSourceOptions? videoOptions)
    {
        builder.AddNode("load_external_face_video", node => node
            .Type("VHS_LoadVideo")
            .Title("Load Face Video")
            .Input("video", videoPath)
            .Input("force_rate", videoOptions?.ForceRate ?? DefaultFrameRate)
            .InputRef("custom_width", registry.GetRef("width"))
            .InputRef("custom_height", registry.GetRef("height"))
            .InputRef("frame_load_cap", registry.GetRef("frame_count"))
            .Input("skip_first_frames", videoOptions?.SkipFirstFrames ?? 0)
            .Input("select_every_nth", videoOptions?.SelectEveryNth ?? 1)
            .Input("format", string.IsNullOrWhiteSpace(videoOptions?.Format) ? "AnimateDiff" : videoOptions.Format));

        builder.AddNode("external_pose_face_detection", node => node
            .Type("PoseAndFaceDetection")
            .Title("Face Detection")
            .InputFromNode("model", "onnx_detection_loader", 0)
            .InputFromNode("images", "load_external_face_video", 0)
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height")));

        registry.Register("face_images", "external_pose_face_detection", 1);
    }

    private static void BuildExternalBackgroundVideo(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string videoPath,
        VideoSourceOptions? videoOptions)
    {
        builder.AddNode("load_external_background_video", node => node
            .Type("VHS_LoadVideo")
            .Title("Load Background Video")
            .Input("video", videoPath)
            .Input("force_rate", videoOptions?.ForceRate ?? DefaultFrameRate)
            .InputRef("custom_width", registry.GetRef("width"))
            .InputRef("custom_height", registry.GetRef("height"))
            .InputRef("frame_load_cap", registry.GetRef("frame_count"))
            .Input("skip_first_frames", videoOptions?.SkipFirstFrames ?? 0)
            .Input("select_every_nth", videoOptions?.SelectEveryNth ?? 1)
            .Input("format", string.IsNullOrWhiteSpace(videoOptions?.Format) ? "AnimateDiff" : videoOptions.Format));

        builder.AddNode("resize_external_background_video", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Background Video")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", "load_external_background_video", 0)
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height")));

        builder.AddNode("masked_external_background_video", node => node
            .Type("DrawMaskOnImage")
            .Title("Mask Background Video")
            .InputFromNode("image", "resize_external_background_video", 0)
            .InputRef("mask", registry.GetRef("character_mask"))
            .Input("color", "0, 0, 0")
            .Input("device", "cpu"));

        registry.Register("background_image", "masked_external_background_video", 0);
    }

    private static void BuildExternalBackgroundImage(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string imagePath)
    {
        builder.AddNode("load_external_background_image", node => node
            .Type("LoadImage")
            .Title("Load Background Image")
            .Input("image", imagePath));

        builder.AddNode("resize_external_background_image", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Background Image")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", "load_external_background_image", 0)
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height")));

        builder.AddNode("external_background_image_batch", node => node
            .Type("Batch Make (mtb)")
            .Title("Repeat Background Image")
            .InputFromNode("image", "resize_external_background_image", 0)
            .InputRef("count", registry.GetRef("frame_count")));

        builder.AddNode("masked_external_background_image", node => node
            .Type("DrawMaskOnImage")
            .Title("Mask Background Image")
            .InputFromNode("image", "external_background_image_batch", 0)
            .InputRef("mask", registry.GetRef("character_mask"))
            .Input("color", "0, 0, 0")
            .Input("device", "cpu"));

        registry.Register("background_image", "masked_external_background_image", 0);
    }

    private static void BuildPreprocessing(ComfyWorkflowBuilder builder, NodeRegistry registry)
    {
        builder.AddNode("onnx_detection_loader", node => node
            .Type("OnnxDetectionModelLoader")
            .Title("Load Wan Animate Detection Models")
            .Input("vitpose_model", DefaultVitPoseModel)
            .Input("yolo_model", DefaultYoloModel)
            .Input("onnx_device", "CUDAExecutionProvider"));

        builder.AddNode("pose_face_detection", node => node
            .Type("PoseAndFaceDetection")
            .Title("Pose And Face Detection")
            .InputFromNode("model", "onnx_detection_loader", 0)
            .InputFromNode("images", "video_size", 0)
            .InputFromNode("width", "video_size", 1)
            .InputFromNode("height", "video_size", 2));

        builder.AddNode("draw_vit_pose", node => node
            .Type("DrawViTPose")
            .Title("Draw ViT Pose")
            .InputFromNode("pose_data", "pose_face_detection", 0)
            .InputFromNode("width", "video_size", 1)
            .InputFromNode("height", "video_size", 2)
            .Input("retarget_padding", 16)
            .Input("body_stick_width", -1)
            .Input("hand_stick_width", -1)
            .Input("draw_head", true));

        builder.AddNode("sam2_loader", node => node
            .Type("DownloadAndLoadSAM2Model")
            .Title("Load SAM2 Model")
            .Input("model", DefaultSam2Model)
            .Input("segmentor", "video")
            .Input("device", "cuda")
            .Input("precision", "fp16"));

        builder.AddNode("sam2_segmentation", node => node
            .Type("Sam2Segmentation")
            .Title("SAM2 Segmentation")
            .InputFromNode("sam2_model", "sam2_loader", 0)
            .InputFromNode("image", "video_size", 0)
            .Input("keep_model_loaded", false)
            .Input("individual_objects", false)
            .InputFromNode("bboxes", "pose_face_detection", 3));

        builder.AddNode("grow_mask", node => node
            .Type("GrowMaskWithBlur")
            .Title("Grow Mask With Blur")
            .InputFromNode("mask", "sam2_segmentation", 0)
            .Input("expand", 10)
            .Input("incremental_expandrate", 0.0)
            .Input("tapered_corners", false)
            .Input("flip_input", false)
            .Input("blur_radius", 0)
            .Input("lerp_alpha", 1.0)
            .Input("decay_factor", 1.0)
            .Input("fill_holes", false));

        builder.AddNode("blockify_mask", node => node
            .Type("BlockifyMask")
            .Title("Blockify Mask")
            .InputFromNode("masks", "grow_mask", 0)
            .Input("block_size", 32)
            .Input("device", "cpu"));

        builder.AddNode("background_image", node => node
            .Type("DrawMaskOnImage")
            .Title("Background Image")
            .InputFromNode("image", "resize_video", 0)
            .InputFromNode("mask", "blockify_mask", 0)
            .Input("color", "0, 0, 0")
            .Input("device", "cpu"));

        registry.Register("face_images", "pose_face_detection", 1);
        registry.Register("pose_images", "draw_vit_pose", 0);
        registry.Register("character_mask", "blockify_mask", 0);
        registry.Register("background_image", "background_image", 0);
    }

    private static void BuildModel(ComfyWorkflowBuilder builder, NodeRegistry registry, GenerationParameters parameters)
    {
        builder.AddNode("model_loader", node => node
            .Type("DiffusionModelLoaderKJ")
            .Title("Load Wan Animate Model")
            .Input("model_name", parameters.Assets?.GetValueOrDefault("Model") ?? DefaultModel)
            .Input("weight_dtype", "fp16")
            .Input("compute_dtype", "fp16")
            .Input("patch_cublaslinear", false)
            .Input("sage_attention", "auto")
            .Input("enable_fp16_accumulation", true));

        var relightLora = parameters.Assets?.GetValueOrDefault("RelightLora") ?? DefaultRelightLora;
        var speedLora = parameters.Assets?.GetValueOrDefault("SpeedLora") ?? DefaultSpeedLora;
        var currentModel = AddModelOnlyLora(builder, "fixed_relight_lora", "model_loader", relightLora, 1.0, "Fixed Relight LoRA");
        currentModel = AddModelOnlyLora(builder, "fixed_speed_lora", currentModel, speedLora, 1.2, "Fixed Speed LoRA");

        if (parameters.Loras is not null)
        {
            var index = 0;
            foreach (var lora in parameters.Loras.Where(lora => lora.IsEnabled))
            {
                var loraPath = !string.IsNullOrWhiteSpace(lora.Path) ? lora.Path : lora.Name;
                if (string.IsNullOrWhiteSpace(loraPath)) continue;

                currentModel = AddModelOnlyLora(builder, $"user_lora_{index}", currentModel, loraPath, lora.Strength, $"User LoRA {index + 1}");
                index++;
            }
        }

        builder.AddNode("compiled_model", node => node
            .Type("TorchCompileModelWanVideoV2")
            .Title("Torch Compile Wan Animate Model")
            .InputFromNode("model", currentModel, 0)
            .Input("backend", "inductor")
            .Input("fullgraph", false)
            .Input("mode", "default")
            .Input("dynamic", false)
            .Input("compile_transformer_blocks_only", true)
            .Input("dynamo_cache_size_limit", 64)
            .Input("force_parameter_static_shapes", true));

        builder.AddNode("clip_loader", node => node
            .Type("CLIPLoader")
            .Title("Load Wan Text Encoder")
            .Input("clip_name", parameters.Assets?.GetValueOrDefault("Clip") ?? DefaultClip)
            .Input("type", "wan")
            .Input("device", "default"));

        builder.AddNode("vae_loader", node => node
            .Type("VAELoader")
            .Title("Load Wan VAE")
            .Input("vae_name", parameters.Assets?.GetValueOrDefault("Vae") ?? DefaultVae));

        registry.Register("model_output", "compiled_model", 0);
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
    }

    private static void BuildConditioning(ComfyWorkflowBuilder builder, NodeRegistry registry, string positivePrompt)
    {
        builder.AddNode("positive_encode", node => node
            .Type("CLIPTextEncode")
            .Title("Positive Prompt")
            .Input("text", positivePrompt)
            .InputRef("clip", registry.GetRef("clip_output")));

        builder.AddNode("negative_zero", node => node
            .Type("ConditioningZeroOut")
            .Title("Zero Negative Conditioning")
            .InputFromNode("conditioning", "positive_encode", 0));
    }

    private static void BuildAnimateLatent(ComfyWorkflowBuilder builder, NodeRegistry registry, int batchSize)
    {
        builder.AddNode("animate_to_video", node => node
            .Type("WanAnimateToVideo")
            .Title("Wan Animate To Video")
            .InputFromNode("positive", "positive_encode", 0)
            .InputFromNode("negative", "negative_zero", 0)
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputRef("reference_image", registry.GetRef("reference_image"))
            .InputRef("face_video", registry.GetRef("face_images"))
            .InputRef("pose_video", registry.GetRef("pose_images"))
            .InputRef("background_video", registry.GetRef("background_image"))
            .InputRef("character_mask", registry.GetRef("character_mask"))
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height"))
            .InputRef("length", registry.GetRef("frame_count"))
            .Input("batch_size", batchSize)
            .Input("continue_motion_max_frames", 5)
            .Input("video_frame_offset", 0));

        registry.Register("positive_output", "animate_to_video", 0);
        registry.Register("negative_output", "animate_to_video", 1);
        registry.Register("latent_output", "animate_to_video", 2);
        registry.Register("trim_amount", "animate_to_video", 3);
    }

    private static void BuildDecode(ComfyWorkflowBuilder builder, NodeRegistry registry)
    {
        builder.AddNode("trim_video_latent", node => node
            .Type("TrimVideoLatent")
            .Title("Trim Video Latent")
            .InputRef("samples", registry.GetRef("latent_output"))
            .InputRef("trim_amount", registry.GetRef("trim_amount")));

        builder.AddNode("vae_decode", node => node
            .Type("VAEDecode")
            .Title("VAE Decode")
            .InputFromNode("samples", "trim_video_latent", 0)
            .InputRef("vae", registry.GetRef("vae_output")));

        registry.Register("image_output", "vae_decode", 0);
    }

    private void ApplyDetailer(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        string positivePrompt,
        string negativePrompt,
        int width,
        int height,
        int batchSize)
    {
        var detailerFragment = parameters.GetFragment(_detailerFragment.Metadata.Id);
        if (detailerFragment?.IsActive != true)
        {
            return;
        }

        var samplerFragment = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var samplerSeed = samplerFragment?.GetLong("seed", _samplerFragment.Defaults.Seed) ?? _samplerFragment.Defaults.Seed;
        if (samplerSeed < 0)
        {
            samplerSeed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
        for (var i = 0; i < passCount; i++)
        {
            var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
            var scopeTitle = i == 0 ? "Detailer " : $"Detailer {i + 1} ";
            var keyPrefix = i == 0 ? string.Empty : $"pass_{i}_";
            var passEnabledKey = i == 0 ? "detailer_enabled" : $"pass_{i}_detailer_enabled";
            if (!detailerFragment.GetBool(passEnabledKey, true))
            {
                continue;
            }

            _detailerModelFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
            {
                UnetName = detailerFragment.GetString($"{keyPrefix}detailer_checkpoint")
                           ?? parameters.Assets?.GetValueOrDefault("DetailerModel")
                           ?? DefaultDetailerModel,
                ClipName = parameters.Assets?.GetValueOrDefault("DetailerClip") ?? DefaultDetailerClip,
                ClipType = "stable_diffusion",
                VaeName = parameters.Assets?.GetValueOrDefault("DetailerVae") ?? DefaultDetailerVae,
                Positive = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_prompt", positivePrompt),
                Negative = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_negative_prompt", negativePrompt),
                Width = width,
                Height = height,
                BatchSize = batchSize
            }, scope: scope, scopeTitle: scopeTitle);

            _detailerLoraFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = scope,
                DetectionModel = detailerFragment.GetString($"{keyPrefix}detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragment.GetString($"{keyPrefix}detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragment.GetString($"{keyPrefix}detailer_scheduler", "simple"),
                Seed = detailerFragment.GetLong($"{keyPrefix}detailer_seed", samplerSeed),
                Steps = detailerFragment.GetInt($"{keyPrefix}detailer_steps", 20),
                Cfg = detailerFragment.GetDouble($"{keyPrefix}detailer_cfg", 4.0),
                Denoise = detailerFragment.GetDouble($"{keyPrefix}detailer_denoise", 0.65),
                Feather = detailerFragment.GetInt($"{keyPrefix}detailer_feather", 5),
                BboxThreshold = detailerFragment.GetDouble($"{keyPrefix}detailer_bbox_threshold", 0.7),
                BboxDilation = detailerFragment.GetInt($"{keyPrefix}detailer_bbox_dilation", 10),
                BboxCropFactor = detailerFragment.GetDouble($"{keyPrefix}detailer_bbox_crop_factor", 3.0),
                DropSize = detailerFragment.GetInt($"{keyPrefix}detailer_drop_size", 70),
                GuideSize = detailerFragment.GetInt($"{keyPrefix}detailer_guide_size", 512),
                MaxSize = detailerFragment.GetInt($"{keyPrefix}detailer_max_size", 1024),
                Cycle = detailerFragment.GetInt($"{keyPrefix}detailer_cycle", 1)
            });
        }
    }

    private double ApplyEnhancements(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        int frameLoadCap,
        double outputFrameRate)
    {
        _seedVR2UpscaleFragment.Build(builder, parameters, registry, frameLoadCap);

        var finalFrameRate = outputFrameRate;
        var interpolationFragment = parameters.GetFragment(_frameInterpolationFragment.Metadata.Id);
        if (interpolationFragment?.IsActive == true)
        {
            var frameMultiplier = interpolationFragment.GetInt("frame_multiplier", 2);

            _frameInterpolationFragment.Build(builder, registry, new FrameInterpolationFragment.Parameters
            {
                RifeModel = interpolationFragment.GetString("rife_model", "rife49.pth") ?? "rife49.pth",
                FrameMultiplier = frameMultiplier,
                ScaleBy = interpolationFragment.GetDouble("scale_by", 2.0)
            });

            var framesOutput = registry.GetRef("frames_output");
            registry.Register("image_output", framesOutput.nodeId, framesOutput.outputIndex);
            finalFrameRate *= frameMultiplier;
        }

        return finalFrameRate;
    }

    private static void BuildMainOutput(ComfyWorkflowBuilder builder, NodeRegistry registry, double frameRate)
    {
        builder.AddNode("video_output", node => node
            .Type("VHS_VideoCombine")
            .Title("Save Wan Animate Video")
            .InputRef("images", registry.GetRef("image_output"))
            .Input("frame_rate", frameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", "tmp/video")
            .Input("format", "video/h264-mp4")
            .Input("pix_fmt", "yuv420p")
            .Input("crf", 19)
            .Input("save_metadata", true)
            .Input("trim_to_audio", true)
            .Input("pingpong", false)
            .Input("save_output", true)
            .InputRef("audio", registry.GetRef("audio")));
    }

    private static void BuildDiagnosticOutput(ComfyWorkflowBuilder builder, NodeRegistry registry, double frameRate)
    {
        builder.AddNode("diagnostic_inputs", node => node
            .Type("ImageConcatMulti")
            .Title("Wan Animate Inputs Diagnostic")
            .Input("inputcount", 4)
            .Input("direction", "down")
            .Input("match_image_size", true)
            .InputRef("image_1", registry.GetRef("reference_image"))
            .InputRef("image_2", registry.GetRef("face_images"))
            .InputRef("image_3", registry.GetRef("pose_images"))
            .InputRef("image_4", registry.GetRef("input_video")));

        builder.AddNode("diagnostic_collage", node => node
            .Type("ImageConcatMulti")
            .Title("Wan Animate Diagnostic Collage")
            .Input("inputcount", 2)
            .Input("direction", "left")
            .Input("match_image_size", true)
            .InputRef("image_1", registry.GetRef("image_output"))
            .InputFromNode("image_2", "diagnostic_inputs", 0));

        builder.AddNode("diagnostic_video_output", node => node
            .Type("VHS_VideoCombine")
            .Title("Save Wan Animate Diagnostic Collage")
            .InputFromNode("images", "diagnostic_collage", 0)
            .Input("frame_rate", frameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", "tmp/video")
            .Input("format", "video/h264-mp4")
            .Input("pix_fmt", "yuv420p")
            .Input("crf", 19)
            .Input("save_metadata", true)
            .Input("trim_to_audio", true)
            .Input("pingpong", false)
            .Input("save_output", true)
            .InputRef("audio", registry.GetRef("audio")));
    }

    private static string AddModelOnlyLora(
        ComfyWorkflowBuilder builder,
        string nodeId,
        string modelInputNodeId,
        string loraPath,
        double strength,
        string title)
    {
        builder.AddNode(nodeId, node => node
            .Type("LoraLoaderModelOnly")
            .Title(title)
            .InputFromNode("model", modelInputNodeId, 0)
            .Input("lora_name", loraPath)
            .Input("strength_model", strength));

        return nodeId;
    }

    private static VideoSourceOptions CreateDefaultOptionalVideoOptions()
    {
        return new VideoSourceOptions
        {
            ForceRate = DefaultFrameRate,
            CustomWidth = 0,
            CustomHeight = 0,
            FrameLoadCap = 0,
            SkipFirstFrames = 0,
            SelectEveryNth = 1,
            Format = "AnimateDiff"
        };
    }

    private static string GetSourcePath(SourceAsset? source)
    {
        return TryGetSourcePath(source, out var path) ? path : string.Empty;
    }

    private static bool TryGetSourcePath(SourceAsset? source, out string path)
    {
        path = source?.Filename ?? source?.FilePath ?? string.Empty;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string GetPromptOrDefault(BlazorWebApp.Models.FragmentParameters? fragment, string key, string defaultValue)
    {
        var value = fragment?.GetString(key, defaultValue);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}