using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Wan;

/// <summary>
/// SteadyDancer Pose2Vid workflow using WanVideo native nodes.
/// Generates video from a source video (pose detection) and reference image.
/// Pipeline: Load Video -> Size -> Load Image -> Resize -> Load Model/VAE/CLIP -> Text Encode ->
///           Pose Detection -> I2V Encode -> SteadyDancer Embeds -> Context Options ->
///           Sampler -> Decode -> [Concat Preview] -> Save Video
/// </summary>
public class WanSteadyDancerWorkflow : IWorkflowBuilder
{
    private readonly LoadVideoFragment _loadVideoFragment = new();
    private readonly GetImageSizeFragment _getImageSizeFragment = new();
    private readonly LoadImageFragment _loadImageFragment = new();
    private readonly ResizeImageFragment _resizeImageFragment = new();
    private readonly LoadWanModelFragment _loadWanModelFragment = new();
    private readonly LoadWanVaeFragment _loadWanVaeFragment = new();
    private readonly LoadClipVisionFragment _loadClipVisionFragment = new();
    private readonly TextEncodeWanFragment _textEncodeWanFragment = new();
    private readonly PoseDetectionFragment _poseDetectionFragment = new();
    private readonly I2VEncodeFragment _i2vEncodeFragment = new();
    private readonly SteadyDancerEmbedsFragment _steadyDancerEmbedsFragment = new();
    private readonly ContextOptionsFragment _contextOptionsFragment = new();
    private readonly SamplerWanFragment _samplerWanFragment = new();
    private readonly DecodeWanFragment _decodeWanFragment = new();
    private readonly ConcatPreviewFragment _concatPreviewFragment = new();
    private readonly SaveVideoFragment _saveVideoFragment = new();
    private readonly PromptsFragment _promptsFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Id = Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"),
        Title = "SteadyDancer",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "Wan21_SteadyDancer_fp8_e4m3fn_scaled_KJ.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "wan_2.1_vae.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "ClipVision",
                Label = "CLIP Vision",
                Type = AssetType.ClipVision,
                DefaultValue = "clip_vision_h.safetensors",
                Order = 3,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "TextEncoder",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = "umt5_xxl_fp16.safetensors",
                Order = 4,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "SpeedLora",
                Label = "Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors",
                Order = 5,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "source_video",
                Label = "Source Video",
                Type = SourceType.Video,
                Required = true
            },
            new WorkflowSource
            {
                Id = "reference_image",
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = false
            }
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _loadVideoFragment;
        yield return _samplerWanFragment;
        yield return _contextOptionsFragment;
        yield return _steadyDancerEmbedsFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var samplerFragment = parameters.GetFragment(_samplerWanFragment.Metadata.Id);
        var contextFragment = parameters.GetFragment(_contextOptionsFragment.Metadata.Id);
        var embedsFragment = parameters.GetFragment(_steadyDancerEmbedsFragment.Metadata.Id);
        var videoFragment = parameters.GetFragment(_loadVideoFragment.Metadata.Id);
        var promptsFragment = parameters.GetFragment("prompts");

        // Extract parameters
        var steps = samplerFragment?.GetInt("steps", 4) ?? 4;
        var cfg = samplerFragment?.GetDouble("cfg", 1.0) ?? 1.0;
        var shift = samplerFragment?.GetInt("shift", 5) ?? 5;
        var seed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var scheduler = samplerFragment?.GetString("scheduler", "dpm++_sde") ?? "dpm++_sde";
        var loraStrength = samplerFragment?.GetDouble("lora_strength", 1.0) ?? 1.0;
        var outputFrameRate = samplerFragment?.GetInt("output_frame_rate", 24) ?? 24;
        var appendPreview = samplerFragment?.GetBool("append_preview", false) ?? false;

        var contextFrames = contextFragment?.GetInt("context_frames", 81) ?? 81;
        var contextOverlap = contextFragment?.GetInt("context_overlap", 16) ?? 16;

        var poseStrengthSpatial = embedsFragment?.GetDouble("pose_strength_spatial", 1.0) ?? 1.0;
        var poseStrengthTemporal = embedsFragment?.GetDouble("pose_strength_temporal", 1.0) ?? 1.0;

        // Resolve source assets
        var videoSource = parameters.Sources?.GetValueOrDefault("source_video");
        var imageSource = parameters.Sources?.GetValueOrDefault("reference_image");

        // 1. Load Video
        _loadVideoFragment.Build(builder, registry, new LoadVideoFragment.Parameters
        {
            Video = videoSource?.Filename ?? "",
            ForceRate = videoFragment?.GetInt("force_rate", 16) ?? 16,
            CustomWidth = videoFragment?.GetInt("custom_width", 480) ?? 480,
            CustomHeight = videoFragment?.GetInt("custom_height", 832) ?? 832,
            FrameLoadCap = videoFragment?.GetInt("frame_load_cap", 176) ?? 176,
            SkipFirstFrames = videoFragment?.GetInt("skip_first_frames", 0) ?? 0,
            SelectEveryNth = videoFragment?.GetInt("select_every_nth", 1) ?? 1
        });

        // 2. Get Image Size (from video frames)
        _getImageSizeFragment.Build(builder, registry, new GetImageSizeFragment.Parameters
        {
            ImageRefName = "video_frames"
        });

        // 3. Load Reference Image
        _loadImageFragment.Build(builder, registry, new LoadImageFragment.Parameters
        {
            Image = imageSource?.Filename ?? ""
        });

        // 4. Resize Subject Image
        _resizeImageFragment.Build(builder, registry, new ResizeImageFragment.Parameters
        {
            NodeId = "resize_subject",
            ImageRefName = "image_input",
            WidthRefName = "width",
            HeightRefName = "height",
            OutputName = "resized_image",
            Title = "Resize Subject Image"
        });

        // 5. Load Wan Model (with LoRA)
        _loadWanModelFragment.Build(builder, registry, new LoadWanModelFragment.Parameters
        {
            ModelName = parameters.Assets?.GetValueOrDefault("Model") ?? "Wan21_SteadyDancer_fp8_e4m3fn_scaled_KJ.safetensors",
            LoraName = parameters.Assets?.GetValueOrDefault("SpeedLora") ?? "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors",
            LoraStrength = loraStrength
        });

        // 6. Load Wan VAE
        _loadWanVaeFragment.Build(builder, registry, new LoadWanVaeFragment.Parameters
        {
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "wan_2.1_vae.safetensors"
        });

        // 7. Load CLIP Vision
        _loadClipVisionFragment.Build(builder, registry, new LoadClipVisionFragment.Parameters
        {
            ClipVisionName = parameters.Assets?.GetValueOrDefault("ClipVision") ?? "clip_vision_h.safetensors"
        });

        // 8. Text Encode
        _textEncodeWanFragment.Build(builder, registry, new TextEncodeWanFragment.Parameters
        {
            TextEncoderName = parameters.Assets?.GetValueOrDefault("TextEncoder") ?? "umt5_xxl_fp16.safetensors",
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 9. Pose Detection
        _poseDetectionFragment.Build(builder, registry, new PoseDetectionFragment.Parameters());

        // 10. I2V Encode
        _i2vEncodeFragment.Build(builder, registry, new I2VEncodeFragment.Parameters());

        // 11. SteadyDancer Embeds
        _steadyDancerEmbedsFragment.Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters
        {
            PoseStrengthSpatial = poseStrengthSpatial,
            PoseStrengthTemporal = poseStrengthTemporal
        });

        // 12. Context Options
        _contextOptionsFragment.Build(builder, registry, new ContextOptionsFragment.Parameters
        {
            ContextFrames = contextFrames,
            ContextOverlap = contextOverlap
        });

        // 13. Wan Sampler
        _samplerWanFragment.Build(builder, registry, new SamplerWanFragment.Parameters
        {
            Steps = steps,
            Cfg = cfg,
            Shift = shift,
            Seed = seed,
            Scheduler = scheduler
        });

        // 14. Decode
        _decodeWanFragment.Build(builder, registry, new DecodeWanFragment.Parameters());

        // 15-16. Conditional Preview + Save Video
        if (appendPreview)
        {
            _concatPreviewFragment.Build(builder, registry);

            _saveVideoFragment.Build(builder, registry, new SaveVideoFragment.Parameters
            {
                NodeId = "video_output",
                FrameRate = outputFrameRate,
                ImageInputName = "preview_concat"
            });
        }
        else
        {
            _saveVideoFragment.Build(builder, registry, new SaveVideoFragment.Parameters
            {
                NodeId = "video_output",
                FrameRate = outputFrameRate,
                ImageInputName = "image_output"
            });
        }

        return builder.ToComfyWorkflow(registry);
    }
}
