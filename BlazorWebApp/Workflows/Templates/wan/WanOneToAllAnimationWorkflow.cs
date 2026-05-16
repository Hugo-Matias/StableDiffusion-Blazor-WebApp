using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using SourceAsset = BlazorWebApp.Models.SourceAsset;
using VideoSourceOptions = BlazorWebApp.Models.VideoSourceOptions;

namespace BlazorWebApp.Workflows.Templates.Wan;

public class WanOneToAllAnimationWorkflow : IWorkflowBuilder
{
    private const string DefaultModel = "Wan21-OneToAllAnimation_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultTextEncoder = "umt5_xxl_fp8_e4m3fn_scaled.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultSpeedLora = "Speed/lightx2v_T2V_14B_cfg_step_distill_v2_lora_rank128_bf16.safetensors";
    private const string DefaultPositivePrompt = "A young white woman with long light-brown hair, wearing a blue denim jacket over a white T-shirt and dark pants is dancing.";
    private const string DefaultNegativePrompt = "色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，杂乱的背景，三条腿，背景人很多，倒着走, watermark, text, logo";
    private const int DefaultWidthCeiling = 480;
    private const int DefaultHeightCeiling = 832;
    private const int DefaultWindow = 81;
    private const int DefaultOverlap = 5;
    private const int DefaultFps = 24;
    private const int DefaultBlocksToSwap = 25;
    private const string ReferenceImageSourceId = "reference_image";
    private const string MotionVideoSourceId = "motion_video";

    private readonly PromptsFragment _promptsFragment = new();
    private readonly WanOneToAllControlsFragment _controlsFragment = new();
    private readonly WanOneToAllPoseFragment _poseFragment = new();
    private readonly WanOneToAllEmbedsFragment _embedsFragment = new();
    private readonly WanOneToAllSamplerFragment _samplerFragment = new();
    private readonly FrameInterpolationFragment _frameInterpolationFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "One-To-All Animation",
        Description = "Transfers motion from a driving video onto a reference image using the Wan 2.1 One-To-All animation model. Pick this for Wan pose-guided character animation when you want source audio preserved in the final video.",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "One-To-All Model",
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
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
                Order = 3,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "SpeedLora",
                Label = "Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultSpeedLora,
                Order = 4,
                ColumnSize = 3
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
                    ForceRate = DefaultFps,
                    CustomWidth = 0,
                    CustomHeight = 0,
                    FrameLoadCap = 0,
                    SkipFirstFrames = 0,
                    SelectEveryNth = 1,
                    Format = "Wan"
                }
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 14B i2v 720p"],
        UsesDualModelLoras = false
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _controlsFragment;
        yield return _poseFragment;
        yield return _embedsFragment;
        yield return _samplerFragment;
        yield return _frameInterpolationFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var controls = parameters.GetFragment(_controlsFragment.Metadata.Id);
        var pose = parameters.GetFragment(_poseFragment.Metadata.Id);
        var embeds = parameters.GetFragment(_embedsFragment.Metadata.Id);
        var sampler = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var prompts = parameters.GetFragment(_promptsFragment.Metadata.Id);
        var referenceSource = parameters.Sources?.GetValueOrDefault(ReferenceImageSourceId);
        var videoSource = parameters.Sources?.GetValueOrDefault(MotionVideoSourceId);
        var videoOptions = videoSource?.VideoOptions;

        var widthCeiling = controls.GetInt("width", DefaultWidthCeiling);
        var heightCeiling = controls.GetInt("height", DefaultHeightCeiling);
        var maxLongerEdge = Math.Max(widthCeiling, heightCeiling);
        var window = controls.GetInt("window", DefaultWindow);
        var overlap = controls.GetInt("overlap", DefaultOverlap);
        var fps = controls.GetInt("fps", DefaultFps);
        var blocksToSwap = controls.GetInt("blocks_to_swap", DefaultBlocksToSwap);
        var seed = sampler.GetLong("seed", 0L);
        if (seed < 0)
        {
            seed = Random.Shared.NextInt64(0, int.MaxValue);
            if (sampler is not null)
            {
                sampler.Values["seed"] = seed;
            }
        }

        var positivePrompt = GetPromptOrDefault(prompts, "positive", DefaultPositivePrompt);
        var negativePrompt = GetPromptOrDefault(prompts, "negative", DefaultNegativePrompt);

        BuildSources(builder, registry, GetSourcePath(referenceSource), GetSourcePath(videoSource), videoOptions, maxLongerEdge, fps);
        BuildControlConstants(builder, registry, window, overlap);
        BuildModel(builder, registry, parameters, blocksToSwap);
        BuildTextEncode(builder, registry, positivePrompt, negativePrompt);
        BuildPoseDetection(builder, registry, pose);
        BuildInitialEmbeds(builder, registry, embeds);
        BuildLoopSampler(builder, registry, sampler, embeds, seed);
        var finalFrameRate = ApplyEnhancements(builder, registry, parameters, fps);
        BuildOutput(builder, registry, finalFrameRate);

        return builder.ToComfyWorkflow(registry);
    }

    private static void BuildSources(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string referenceImage,
        string motionVideo,
        VideoSourceOptions? videoOptions,
        int maxLongerEdge,
        int fps)
    {
        builder.AddNode("max_longer_edge", node => node
            .Type("INTConstant")
            .Title("Max Longer Edge")
            .Input("value", maxLongerEdge));

        builder.AddNode("load_reference_image", node => node
            .Type("LoadImage")
            .Title("Reference Image")
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
            .Title("Reference Video")
            .Input("video", motionVideo)
            .Input("force_rate", videoOptions?.ForceRate ?? fps)
            .Input("custom_width", videoOptions?.CustomWidth ?? 0)
            .Input("custom_height", videoOptions?.CustomHeight ?? 0)
            .Input("frame_load_cap", videoOptions?.FrameLoadCap ?? 0)
            .Input("skip_first_frames", videoOptions?.SkipFirstFrames ?? 0)
            .Input("select_every_nth", videoOptions?.SelectEveryNth ?? 1)
            .Input("format", string.IsNullOrWhiteSpace(videoOptions?.Format) ? "Wan" : videoOptions.Format));

        builder.AddNode("resize_video", node => node
            .Type("ImageResizeKJv2")
            .Title("Resize Video")
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
            .Title("Video Size And Count")
            .InputFromNode("image", "resize_video", 0));

        registry.Register("source_image", "resize_reference_image", 0);
        registry.Register("input_video", "resize_video", 0);
        registry.Register("width", "resize_reference_image", 1);
        registry.Register("height", "resize_reference_image", 2);
        registry.Register("length", "load_video", 1);
        registry.Register("audio", "load_video", 2);
    }

    private static void BuildControlConstants(ComfyWorkflowBuilder builder, NodeRegistry registry, int window, int overlap)
    {
        builder.AddNode("window", node => node
            .Type("INTConstant")
            .Title("Window Size")
            .Input("value", window));

        builder.AddNode("overlap", node => node
            .Type("INTConstant")
            .Title("Overlap")
            .Input("value", overlap));

        builder.AddNode("length_within_window", node => node
            .Type("easy compare")
            .Title("Length Within Window")
            .Input("comparison", "a <= b")
            .InputRef("a", registry.GetRef("length"))
            .InputFromNode("b", "window", 0));

        builder.AddNode("final_window", node => node
            .Type("easy ifElse")
            .Title("Final Window")
            .InputRef("on_true", registry.GetRef("length"))
            .InputFromNode("on_false", "window", 0)
            .InputFromNode("boolean", "length_within_window", 0));

        registry.Register("window", "window", 0);
        registry.Register("overlap", "overlap", 0);
        registry.Register("final_window", "final_window", 0);
    }

    private static void BuildModel(ComfyWorkflowBuilder builder, NodeRegistry registry, GenerationParameters parameters, int blocksToSwap)
    {
        builder.AddNode("vae_loader", node => node
            .Type("WanVideoVAELoader")
            .Title("Load Wan VAE")
            .Input("model_name", parameters.Assets?.GetValueOrDefault("Vae") ?? DefaultVae)
            .Input("precision", "bf16")
            .Input("use_cpu_cache", false)
            .Input("verbose", false));

        builder.AddNode("t5_loader", node => node
            .Type("LoadWanVideoT5TextEncoder")
            .Title("Load Wan T5 Text Encoder")
            .Input("model_name", parameters.Assets?.GetValueOrDefault("TextEncoder") ?? DefaultTextEncoder)
            .Input("precision", "bf16")
            .Input("load_device", "offload_device")
            .Input("quantization", "disabled"));

        builder.AddNode("block_swap", node => node
            .Type("WanVideoBlockSwap")
            .Title("Block Swap")
            .Input("blocks_to_swap", blocksToSwap)
            .Input("offload_img_emb", false)
            .Input("offload_txt_emb", false)
            .Input("use_non_blocking", true)
            .Input("vace_blocks_to_swap", 1)
            .Input("prefetch_blocks", 1)
            .Input("block_swap_debug", false));

        builder.AddNode("model_loader", node => node
            .Type("WanVideoModelLoader")
            .Title("Load One-To-All Model")
            .Input("model", parameters.Assets?.GetValueOrDefault("Model") ?? DefaultModel)
            .Input("base_precision", "fp16_fast")
            .Input("quantization", "disabled")
            .Input("load_device", "offload_device")
            .Input("attention_mode", "sageattn")
            .Input("rms_norm_function", "default"));

        builder.AddNode("set_block_swap", node => node
            .Type("WanVideoSetBlockSwap")
            .Title("Set Block Swap")
            .InputFromNode("model", "model_loader", 0)
            .InputFromNode("block_swap_args", "block_swap", 0));

        var finalLoraNodeId = AddWanLoraChain(builder, parameters.Loras, parameters.Assets?.GetValueOrDefault("SpeedLora") ?? DefaultSpeedLora);

        builder.AddNode("set_loras", node => node
            .Type("WanVideoSetLoRAs")
            .Title("Set LoRAs")
            .InputFromNode("model", "set_block_swap", 0)
            .InputFromNode("lora", finalLoraNodeId, 0));

        registry.Register("vae", "vae_loader", 0);
        registry.Register("t5", "t5_loader", 0);
        registry.Register("model", "set_loras", 0);
    }

    private static void BuildTextEncode(ComfyWorkflowBuilder builder, NodeRegistry registry, string positivePrompt, string negativePrompt)
    {
        builder.AddNode("text_encode", node => node
            .Type("WanVideoTextEncode")
            .Title("Text Encode")
            .Input("positive_prompt", positivePrompt)
            .Input("negative_prompt", negativePrompt)
            .Input("force_offload", true)
            .Input("use_disk_cache", true)
            .Input("device", "gpu")
            .InputRef("t5", registry.GetRef("t5")));

        registry.Register("text_embeds", "text_encode", 0);
    }

    private static void BuildPoseDetection(ComfyWorkflowBuilder builder, NodeRegistry registry, BlazorWebApp.Models.FragmentParameters? pose)
    {
        builder.AddNode("onnx_detection_loader", node => node
            .Type("OnnxDetectionModelLoader")
            .Title("Load ONNX Detection Models")
            .Input("vitpose_model", pose.GetString("vitpose_model", "vitpose-l-wholebody.onnx"))
            .Input("yolo_model", pose.GetString("yolo_model", "yolov10m.onnx"))
            .Input("onnx_device", pose.GetString("onnx_device", "CUDAExecutionProvider")));

        registry.Register("onnx_model", "onnx_detection_loader", 0);

        builder.AddNode("pose_detection", node => node
            .Type("PoseDetectionOneToAllAnimation")
            .Title("Pose Detection One-To-All")
            .InputRef("model", registry.GetRef("onnx_model"))
            .InputRef("images", registry.GetRef("input_video"))
            .InputRef("ref_image", registry.GetRef("source_image"))
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height"))
            .Input("align_to", pose.GetString("align_to", "ref"))
            .Input("draw_face_points", pose.GetString("draw_face_points", "full"))
            .Input("draw_head", pose.GetString("draw_head", "full")));

        registry.Register("pose_images", "pose_detection", 0);
        registry.Register("ref_pose", "pose_detection", 1);
        registry.Register("ref_image", "pose_detection", 2);
        registry.Register("ref_mask", "pose_detection", 3);
    }

    private static void BuildInitialEmbeds(ComfyWorkflowBuilder builder, NodeRegistry registry, BlazorWebApp.Models.FragmentParameters? embeds)
    {
        builder.AddNode("empty_embeds", node => node
            .Type("WanVideoEmptyEmbeds")
            .Title("Empty Embeds")
            .InputRef("width", registry.GetRef("width"))
            .InputRef("height", registry.GetRef("height"))
            .InputRef("num_frames", registry.GetRef("final_window")));

        builder.AddNode("initial_pose_range", node => node
            .Type("GetImageRangeFromBatch")
            .Title("Initial Pose Range")
            .Input("start_index", 0)
            .InputRef("num_frames", registry.GetRef("final_window"))
            .InputRef("images", registry.GetRef("pose_images")));

        builder.AddNode("reference_embeds", node => node
            .Type("WanVideoAddOneToAllReferenceEmbeds")
            .Title("Reference Embeds")
            .Input("strength", embeds.GetDouble("ref_strength", 1.0))
            .Input("start_percent", embeds.GetDouble("ref_start_percent", 0.0))
            .Input("end_percent", embeds.GetDouble("ref_end_percent", 1.0))
            .InputFromNode("embeds", "empty_embeds", 0)
            .InputRef("vae", registry.GetRef("vae"))
            .InputRef("ref_image", registry.GetRef("ref_image"))
            .InputRef("ref_mask", registry.GetRef("ref_mask")));

        builder.AddNode("initial_pose_embeds", node => node
            .Type("WanVideoAddOneToAllPoseEmbeds")
            .Title("Initial Pose Embeds")
            .Input("strength", embeds.GetDouble("init_pose_strength", 1.0))
            .Input("start_percent", embeds.GetDouble("pose_start_percent", 0.0))
            .Input("end_percent", embeds.GetDouble("pose_end_percent", 1.0))
            .Input("pose_cfg_scale", embeds.GetDouble("init_pose_cfg_scale", 1.96))
            .InputFromNode("embeds", "reference_embeds", 0)
            .InputFromNode("pose_images", "initial_pose_range", 0)
            .InputRef("pose_prefix_image", registry.GetRef("ref_pose")));

        registry.Register("ref_embeds", "reference_embeds", 0);
        registry.Register("init_embeds", "initial_pose_embeds", 0);
        registry.Register("dummy", "initial_pose_range", 0);
    }

    private static void BuildLoopSampler(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        BlazorWebApp.Models.FragmentParameters? sampler,
        BlazorWebApp.Models.FragmentParameters? embeds,
        long seed)
    {
        builder.AddNode("loop_count", node => node
            .Type("MathExpression|pysssss")
            .Title("Loop Count")
            .Input("expression", "ceil(a/b)")
            .InputRef("a", registry.GetRef("length"))
            .InputRef("b", registry.GetRef("final_window")));

        builder.AddNode("loop_start", node => node
            .Type("easy forLoopStart")
            .Title("Loop Start")
            .InputRef("initial_value1", registry.GetRef("dummy"))
            .InputFromNode("total", "loop_count", 0));

        builder.AddNode("in_loop", node => node
            .Type("easy compare")
            .Title("In Loop")
            .Input("comparison", "a > 0")
            .InputFromNode("a", "loop_start", 1));

        builder.AddNode("previous_size", node => node
            .Type("GetImageSizeAndCount")
            .Title("Previous Images Size And Count")
            .InputFromNode("image", "loop_start", 2));

        builder.AddNode("previous_overlap_start", node => node
            .Type("ImageBatchExtendWithOverlap")
            .Title("Previous Overlap Start")
            .InputFromNode("source_images", "previous_size", 0)
            .InputRef("overlap", registry.GetRef("overlap"))
            .Input("overlap_side", "source")
            .Input("overlap_mode", "linear_blend"));

        builder.AddNode("previous_latents", node => node
            .Type("WanVideoEncode")
            .Title("Encode Previous Latents")
            .Input("enable_vae_tiling", false)
            .Input("tile_x", 272)
            .Input("tile_y", 272)
            .Input("tile_stride_x", 144)
            .Input("tile_stride_y", 128)
            .Input("noise_aug_strength", 0)
            .Input("latent_strength", 1)
            .InputRef("vae", registry.GetRef("vae"))
            .InputFromNode("image", "previous_overlap_start", 1));

        builder.AddNode("extend_embeds", node => node
            .Type("WanVideoAddOneToAllExtendEmbeds")
            .Title("Extend Embeds")
            .Input("if_not_enough_frames", "pad_with_last")
            .InputRef("embeds", registry.GetRef("ref_embeds"))
            .InputFromNode("prev_latents", "previous_latents", 0)
            .InputRef("pose_images", registry.GetRef("pose_images"))
            .InputRef("window_size", registry.GetRef("final_window"))
            .InputRef("overlap", registry.GetRef("overlap"))
            .InputFromNode("frames_processed", "previous_size", 3));

        builder.AddNode("loop_pose_embeds", node => node
            .Type("WanVideoAddOneToAllPoseEmbeds")
            .Title("Loop Pose Embeds")
            .Input("strength", embeds.GetDouble("loop_pose_strength", 1.0))
            .Input("start_percent", embeds.GetDouble("pose_start_percent", 0.0))
            .Input("end_percent", embeds.GetDouble("pose_end_percent", 1.0))
            .Input("pose_cfg_scale", embeds.GetDouble("loop_pose_cfg_scale", 1.5))
            .InputFromNode("embeds", "extend_embeds", 0)
            .InputFromNode("pose_images", "extend_embeds", 1)
            .InputRef("pose_prefix_image", registry.GetRef("ref_pose")));

        builder.AddNode("select_image_embeds", node => node
            .Type("easy ifElse")
            .Title("Select Input Image Embeds")
            .InputFromNode("on_true", "loop_pose_embeds", 0)
            .InputRef("on_false", registry.GetRef("init_embeds"))
            .InputFromNode("boolean", "in_loop", 0));

        builder.AddNode("scheduler", node => node
            .Type("WanVideoScheduler")
            .Title("WanVideo Scheduler")
            .Input("scheduler", sampler.GetString("scheduler", "unipc"))
            .Input("steps", sampler.GetInt("scheduler_steps", 2))
            .Input("shift", sampler.GetDouble("shift", 7.0))
            .Input("start_step", 0)
            .Input("end_step", -1));

        builder.AddNode("sampler", node => node
            .Type("WanVideoSampler")
            .Title("WanVideo Sampler")
            .Input("steps", sampler.GetInt("sampler_steps", 8))
            .Input("cfg", sampler.GetDouble("cfg", 1.0))
            .Input("shift", sampler.GetDouble("shift", 7.0))
            .Input("seed", seed)
            .Input("force_offload", sampler.GetBool("force_offload", true))
            .InputFromNode("scheduler", "scheduler", 3)
            .Input("riflex_freq_index", 0)
            .Input("denoise_strength", 1.0)
            .Input("batched_cfg", false)
            .Input("rope_function", "comfy")
            .Input("start_step", 0)
            .Input("end_step", -1)
            .Input("add_noise_to_samples", false)
            .InputRef("model", registry.GetRef("model"))
            .InputFromNode("image_embeds", "select_image_embeds", 0)
            .InputRef("text_embeds", registry.GetRef("text_embeds")));

        builder.AddNode("decode", node => node
            .Type("WanVideoDecode")
            .Title("WanVideo Decode")
            .Input("enable_vae_tiling", false)
            .Input("tile_x", 272)
            .Input("tile_y", 272)
            .Input("tile_stride_x", 144)
            .Input("tile_stride_y", 128)
            .Input("normalization", "default")
            .InputRef("vae", registry.GetRef("vae"))
            .InputFromNode("samples", "sampler", 0));

        builder.AddNode("extend_generated_images", node => node
            .Type("ImageBatchExtendWithOverlap")
            .Title("Extend Generated Images")
            .InputFromNode("source_images", "loop_start", 2)
            .InputFromNode("new_images", "decode", 0)
            .InputRef("overlap", registry.GetRef("overlap"))
            .Input("overlap_side", "source")
            .Input("overlap_mode", "linear_blend"));

        builder.AddNode("select_output_images", node => node
            .Type("easy ifElse")
            .Title("Set Output Images")
            .InputFromNode("on_true", "extend_generated_images", 2)
            .InputFromNode("on_false", "decode", 0)
            .InputFromNode("boolean", "in_loop", 0));

        builder.AddNode("loop_end", node => node
            .Type("easy forLoopEnd")
            .Title("Loop End")
            .InputFromNode("flow", "loop_start", 0)
            .InputFromNode("initial_value1", "select_output_images", 0));

        builder.AddNode("final_image_range", node => node
            .Type("GetImageRangeFromBatch")
            .Title("Final Image Range")
            .Input("start_index", 0)
            .InputRef("num_frames", registry.GetRef("length"))
            .InputFromNode("images", "loop_end", 0));

        registry.Register("image_output", "final_image_range", 0);
    }

    private double ApplyEnhancements(ComfyWorkflowBuilder builder, NodeRegistry registry, GenerationParameters parameters, double outputFrameRate)
    {
        var finalFrameRate = outputFrameRate;
        var interpolation = parameters.GetFragment(_frameInterpolationFragment.Metadata.Id);
        if (interpolation?.IsActive == true)
        {
            var frameMultiplier = interpolation.GetInt("frame_multiplier", 2);
            _frameInterpolationFragment.Build(builder, registry, new FrameInterpolationFragment.Parameters
            {
                RifeModel = interpolation.GetString("rife_model", "rife49.pth"),
                FrameMultiplier = frameMultiplier,
                ScaleBy = interpolation.GetDouble("scale_by", 2.0)
            });

            var framesOutput = registry.GetRef("frames_output");
            registry.Register("image_output", framesOutput.nodeId, framesOutput.outputIndex);
            finalFrameRate *= frameMultiplier;
        }

        return finalFrameRate;
    }

    private static void BuildOutput(ComfyWorkflowBuilder builder, NodeRegistry registry, double frameRate)
    {
        builder.AddNode("video_output", node => node
            .Type("VHS_VideoCombine")
            .Title("Final Output")
            .InputRef("images", registry.GetRef("image_output"))
            .Input("frame_rate", frameRate)
            .Input("loop_count", 0)
            .Input("filename_prefix", "tmp/video")
            .Input("format", "video/h264-mp4")
            .Input("pix_fmt", "yuv420p")
            .Input("crf", 19)
            .Input("save_metadata", true)
            .Input("trim_to_audio", false)
            .Input("pingpong", false)
            .Input("save_output", true)
            .InputRef("audio", registry.GetRef("audio")));
    }

    private static string AddWanLoraChain(
        ComfyWorkflowBuilder builder,
        IList<BlazorWebApp.Models.Lora>? loras,
        string speedLora)
    {
        var currentLoraNodeId = "speed_lora";
        AddWanLoraSelect(builder, currentLoraNodeId, "Speed LoRA", speedLora, 1.0);

        if (loras == null || loras.Count == 0)
        {
            return currentLoraNodeId;
        }

        var loraIndex = 0;
        foreach (var lora in loras.Where(lora => lora.IsEnabled))
        {
            var loraFile = GetLoraFile(lora);
            if (string.IsNullOrWhiteSpace(loraFile))
            {
                continue;
            }

            var nodeId = $"user_lora_{loraIndex}";
            AddWanLoraSelect(builder, nodeId, $"User LoRA {loraIndex + 1}", loraFile, lora.Strength, currentLoraNodeId);
            currentLoraNodeId = nodeId;
            loraIndex++;
        }

        return currentLoraNodeId;
    }

    private static void AddWanLoraSelect(ComfyWorkflowBuilder builder, string nodeId, string title, string loraFile, double strength, string? previousLoraNodeId = null)
    {
        builder.AddNode(nodeId, node =>
        {
            node.Type("WanVideoLoraSelect")
                .Title(title)
                .Input("lora", loraFile)
                .Input("strength", strength)
                .Input("low_mem_load", true)
                .Input("merge_loras", false);

            if (!string.IsNullOrWhiteSpace(previousLoraNodeId))
            {
                node.InputFromNode("prev_lora", previousLoraNodeId, 0);
            }
        });
    }

    private static string GetLoraFile(BlazorWebApp.Models.Lora lora)
    {
        return !string.IsNullOrWhiteSpace(lora.Path) ? lora.Path : lora.Name ?? string.Empty;
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