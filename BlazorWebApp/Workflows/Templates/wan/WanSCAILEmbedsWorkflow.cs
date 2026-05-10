using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using VideoSourceOptions = BlazorWebApp.Models.VideoSourceOptions;

namespace BlazorWebApp.Workflows.Templates.Wan;

/// <summary>
/// Wan SCAIL Video Multi-Character Motion Transfer workflow.
/// Transfers motion from a source video to characters in a reference image using SCAIL reference and pose embeds.
/// Pipeline: Load Models -> Load Video + Reference Image -> VitPose Detection -> NLF Pose Rendering ->
///           CLIP Vision Encode -> Cached Text Encode -> Embeds Chain (Empty->Ref->Pose) -> Sampler -> Decode -> Save
/// </summary>
public class WanSCAILEmbedsWorkflow : IWorkflowBuilder
{
    private const string DefaultModel = "Wan21-14B-SCAIL-preview_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultTextEncoder = "umt5_xxl_fp16.safetensors";
    private const string DefaultClipVision = "clip_vision_h.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultSpeedLora = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors";
    private const string DefaultPositivePrompt = "Woman dancing";
    private const string DefaultNegativePrompt = "色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，杂乱的背景，三条腿，背景人很多，倒着走";
    private const int DefaultTargetWidth = 512;
    private const int DefaultTargetHeight = 896;
    private const double DefaultOutputFrameRate = 16;

    private readonly LoadClipVisionFragment _loadClipVisionFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();
    private readonly FrameInterpolationFragment _frameInterpolationFragment = new();

    // SCAIL-specific fragments
    private readonly SCAILPoseDetectionFragment _scailPoseDetectionFragment = new();
    private readonly SCAILPoseRenderingFragment _scailPoseRenderingFragment = new();
    private readonly SCAILEmbedsFragment _scailEmbedsFragment = new();
    private readonly SCAILSamplerFragment _scailSamplerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "SCAIL Motion Transfer",
        Description = "Transfers motion from a driving video onto a reference image using SCAIL reference and pose embeds. Pick this for Wan 2.1 14B pose-control motion transfer when you have both a reference image and a motion video.",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "SCAIL Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultModel,
                Order = 1,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "TextEncoder",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = DefaultTextEncoder,
                Order = 2,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "ClipVision",
                Label = "CLIP Vision",
                Type = AssetType.ClipVision,
                DefaultValue = DefaultClipVision,
                Order = 3,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
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
                ColumnSize = 3
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "ref_image",
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = true
            },
            new WorkflowSource
            {
                Id = "motion_video",
                Label = "Motion Video",
                Type = SourceType.Video,
                Required = true,
                DefaultVideoOptions = new VideoSourceOptions
                {
                    ForceRate = 0,
                    FrameLoadCap = 0,
                    SkipFirstFrames = 0,
                    SelectEveryNth = 1,
                    Format = "AnimateDiff"
                }
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 14B i2v 480p"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _scailSamplerFragment;
        yield return _scailEmbedsFragment;
        yield return _scailPoseDetectionFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _frameInterpolationFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var samplerFragment = parameters.GetFragment(_scailSamplerFragment.Metadata.Id);
        var poseDetectionFragment = parameters.GetFragment(_scailPoseDetectionFragment.Metadata.Id);
        var promptsFragment = parameters.GetFragment(_promptsFragment.Metadata.Id);

        var videoSource = parameters.Sources?.GetValueOrDefault("motion_video");
        var refImageSource = parameters.Sources?.GetValueOrDefault("ref_image");

        var seed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        if (samplerFragment != null)
        {
            samplerFragment.Values["seed"] = seed;
        }

        var positivePrompt = GetPromptOrDefault(promptsFragment, "positive", DefaultPositivePrompt);
        var negativePrompt = GetPromptOrDefault(promptsFragment, "negative", DefaultNegativePrompt);
        var videoOptions = videoSource?.VideoOptions;
        var loadVideoForceRate = videoOptions?.ForceRate ?? 0;
        var outputFrameRate = loadVideoForceRate > 0
            ? loadVideoForceRate
            : samplerFragment?.GetFloat("frame_rate", (float)DefaultOutputFrameRate) ?? DefaultOutputFrameRate;
        var customWidth = videoOptions?.CustomWidth ?? 0;
        var customHeight = videoOptions?.CustomHeight ?? 0;
        var frameLoadCap = videoOptions?.FrameLoadCap ?? poseDetectionFragment?.GetInt("frame_load_cap", 0) ?? 0;
        var skipFirstFrames = videoOptions?.SkipFirstFrames ?? poseDetectionFragment?.GetInt("skip_first_frames", 0) ?? 0;
        var selectEveryNth = videoOptions?.SelectEveryNth ?? poseDetectionFragment?.GetInt("select_every_nth", 1) ?? 1;
        var videoFormat = string.IsNullOrWhiteSpace(videoOptions?.Format) ? "AnimateDiff" : videoOptions.Format;
        var targetWidth = poseDetectionFragment?.GetInt("target_width", DefaultTargetWidth) ?? DefaultTargetWidth;
        var targetHeight = poseDetectionFragment?.GetInt("target_height", DefaultTargetHeight) ?? DefaultTargetHeight;

        builder.AddNode("target_width", node => node
            .Type("INTConstant")
            .Title("width")
            .Input("value", targetWidth));

        builder.AddNode("target_height", node => node
            .Type("INTConstant")
            .Title("height")
            .Input("value", targetHeight));

        builder.AddNode("load_video", node => node
            .Type("VHS_LoadVideo")
            .Title("Load Video")
            .Input("video", videoSource?.Filename ?? "")
            .Input("force_rate", loadVideoForceRate)
            .Input("custom_width", customWidth)
            .Input("custom_height", customHeight)
            .Input("frame_load_cap", frameLoadCap)
            .Input("skip_first_frames", skipFirstFrames)
            .Input("select_every_nth", selectEveryNth)
            .Input("format", videoFormat));

        registry.Register("source_video_frames", "load_video", 0);
        registry.Register("source_frame_count", "load_video", 1);
        registry.Register("audio", "load_video", 2);

        builder.AddNode("resize_video", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Motion Video")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 32)
            .Input("device", "cpu")
            .InputFromNode("image", "load_video", 0)
            .InputFromNode("width", "target_width", 0)
            .InputFromNode("height", "target_height", 0));

        builder.AddNode("video_size", node => node
            .Type("GetImageSizeAndCount")
            .Title("Motion Video Size And Frame Count")
            .InputFromNode("image", "resize_video", 0));

        registry.Register("video_frames", "resize_video", 0);
        registry.Register("num_frames", "load_video", 1);

        builder.AddNode("render_width", node => node
            .Type("SimpleCalculatorKJ")
            .Title("Render Width")
            .Input("expression", "a / 2")
            .InputFromNode("a", "target_width", 0));

        builder.AddNode("render_height", node => node
            .Type("SimpleCalculatorKJ")
            .Title("Render Height")
            .Input("expression", "a / 2")
            .InputFromNode("a", "target_height", 0));

        registry.Register("render_width", "render_width", 1);
        registry.Register("render_height", "render_height", 1);

        builder.AddNode("load_ref_image", node => node
            .Type("LoadImage")
            .Title("Load Reference Image")
            .Input("image", refImageSource?.Filename ?? ""));

        builder.AddNode("resize_ref_image", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Reference Image")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "255,255,255")
            .Input("crop_position", "center")
            .Input("divisible_by", 32)
            .Input("device", "cpu")
            .InputFromNode("image", "load_ref_image", 0)
            .InputFromNode("width", "target_width", 0)
            .InputFromNode("height", "target_height", 0));

        registry.Register("ref_image", "resize_ref_image", 0);
        registry.Register("width", "resize_ref_image", 1);
        registry.Register("height", "resize_ref_image", 2);

        builder.AddNode("compile_settings", node => node
            .Type("WanVideoTorchCompileSettings")
            .Title("Torch Compile Settings")
            .Input("backend", "inductor")
            .Input("fullgraph", false)
            .Input("mode", "default")
            .Input("dynamic", false)
            .Input("dynamo_cache_size_limit", 64)
            .Input("compile_transformer_blocks_only", true)
            .Input("dynamo_recompile_limit", 128)
            .Input("force_parameter_static_shapes", false)
            .Input("allow_unmerged_lora_compile", false));

        builder.AddNode("block_swap", node => node
            .Type("WanVideoBlockSwap")
            .Title("Block Swap")
            .Input("blocks_to_swap", 23)
            .Input("offload_img_emb", false)
            .Input("offload_txt_emb", false)
            .Input("use_non_blocking", false)
            .Input("vace_blocks_to_swap", 1)
            .Input("prefetch_blocks", 1)
            .Input("block_swap_debug", false));

        builder.AddNode("model_loader", node => node
            .Type("WanVideoModelLoader")
            .Title("Load SCAIL Model")
            .Input("model", parameters.Assets?.GetValueOrDefault("Model") ?? DefaultModel)
            .Input("base_precision", "fp16_fast")
            .Input("quantization", "disabled")
            .Input("load_device", "offload_device")
            .Input("attention_mode", "sageattn")
            .Input("rms_norm_function", "default")
            .InputFromNode("compile_args", "compile_settings", 0)
            .InputFromNode("block_swap_args", "block_swap", 0));

        builder.AddNode("lora_select", node => node
            .Type("WanVideoLoraSelect")
            .Title("Speed LoRA")
            .Input("lora", parameters.Assets?.GetValueOrDefault("SpeedLora") ?? DefaultSpeedLora)
            .Input("strength", 1.0)
            .Input("low_mem_load", false)
            .Input("merge_loras", false));

        builder.AddNode("set_loras", node => node
            .Type("WanVideoSetLoRAs")
            .Title("Set Speed LoRA")
            .InputFromNode("model", "model_loader", 0)
            .InputFromNode("lora", "lora_select", 0));

        registry.Register("model", "set_loras", 0);

        builder.AddNode("text_encode", node => node
            .Type("WanVideoTextEncodeCached")
            .Title("Text Encode Cached")
            .Input("model_name", parameters.Assets?.GetValueOrDefault("TextEncoder") ?? DefaultTextEncoder)
            .Input("precision", "bf16")
            .Input("positive_prompt", positivePrompt)
            .Input("negative_prompt", negativePrompt)
            .Input("quantization", "disabled")
            .Input("use_disk_cache", false)
            .Input("device", "gpu"));

        registry.Register("text_embeds", "text_encode", 0);

        builder.AddNode("vae_loader", node => node
            .Type("WanVideoVAELoader")
            .Title("Load Wan VAE")
            .Input("model_name", parameters.Assets?.GetValueOrDefault("Vae") ?? DefaultVae)
            .Input("precision", "bf16")
            .Input("use_cpu_cache", false)
            .Input("verbose", false));

        registry.Register("vae", "vae_loader", 0);

        _loadClipVisionFragment.Build(builder, registry, new LoadClipVisionFragment.Parameters
        {
            ClipVisionName = parameters.Assets?.GetValueOrDefault("ClipVision") ?? DefaultClipVision
        });

        builder.AddNode("clip_vision_encode", node => node
            .Type("WanVideoClipVisionEncode")
            .Title("CLIP Vision Encode")
            .Input("strength_1", 1.0)
            .Input("strength_2", 1.0)
            .Input("crop", "center")
            .Input("combine_embeds", "average")
            .Input("force_offload", true)
            .Input("tiles", 0)
            .Input("ratio", 0.5)
            .InputRef("clip_vision", registry.GetRef("clip_vision_output"))
            .InputRef("image_1", registry.GetRef("ref_image")));

        registry.Register("clip_embeds", "clip_vision_encode", 0);

        // ============================================================
        // 8. SCAIL Pose Detection (Video + reference -> DW poses)
        //    Registers: dw_poses, ref_dw_pose, num_frames
        // ============================================================
        _scailPoseDetectionFragment.Build(builder, parameters, registry);

        // ============================================================
        // 9. SCAIL Pose Rendering (NLF prediction + pose image rendering)
        //    Requires: video_frames, dw_poses, ref_dw_pose, width, height
        //    Registers: pose_images
        // ============================================================
        _scailPoseRenderingFragment.Build(builder, parameters, registry);

        // ============================================================
        // 10. SCAIL Embeds Chain (Empty -> Ref -> Pose)
        //     Requires: vae, ref_image, clip_embeds, pose_images, width, height, num_frames
        //     Registers: image_embeds
        // ============================================================
        _scailEmbedsFragment.Build(builder, parameters, registry);

        // ============================================================
        // 11. SCAIL Sampler (Scheduler -> ExtraArgs -> ContextOptions -> Sample -> Decode)
        //     Requires: model, image_embeds, text_embeds, vae
        //     Registers: latent_output
        // ============================================================
        _scailSamplerFragment.Build(builder, parameters, registry);

        _seedVR2UpscaleFragment.Build(builder, parameters, registry, frameLoadCap);

        var finalImageOutputName = "image_output";
        var finalOutputFrameRate = outputFrameRate;
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

            finalImageOutputName = "frames_output";
            finalOutputFrameRate *= frameMultiplier;
        }

        builder.AddNode("output_size", node => node
            .Type("GetImageSizeAndCount")
            .Title("Decoded Image Size And Count")
            .InputRef("image", registry.GetRef(finalImageOutputName)));

        registry.Register("image_output", "output_size", 0);

        // ============================================================
        // 12. Save Video (VHS_VideoCombine)
        // ============================================================
        builder.AddNode("video_output", node => node
            .Type("VHS_VideoCombine")
            .Title("Save Video")
            .Input("frame_rate", finalOutputFrameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", "WanVideo_SCAIL")
            .Input("format", "video/h264-mp4")
            .Input("pix_fmt", "yuv420p")
            .Input("crf", 19)
            .Input("save_metadata", true)
            .Input("trim_to_audio", false)
            .Input("pingpong", false)
            .Input("save_output", true)
            .InputRef("images", registry.GetRef("image_output")));

        return builder.ToComfyWorkflow(registry);
    }

    private static string GetPromptOrDefault(BlazorWebApp.Models.FragmentParameters? fragment, string key, string defaultValue)
    {
        var value = fragment?.GetString(key, defaultValue);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
