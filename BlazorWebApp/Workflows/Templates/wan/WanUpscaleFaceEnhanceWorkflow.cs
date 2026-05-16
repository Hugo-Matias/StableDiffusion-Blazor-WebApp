using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using SourceAsset = BlazorWebApp.Models.SourceAsset;
using VideoSourceOptions = BlazorWebApp.Models.VideoSourceOptions;

namespace BlazorWebApp.Workflows.Templates.Wan;

public class WanUpscaleFaceEnhanceWorkflow : IWorkflowBuilder
{
    private const string DefaultLowModel = "Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultSpeedLora = "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors";
    private const string DefaultTextEncoder = "umt5_xxl_fp16.safetensors";
    private const string DefaultUpscaleModel = "Remacri (foolhardy).pth";
    private const string SourceVideoId = "source_video";

    private readonly LoadVideoFragment _loadVideoFragment = new();
    private readonly WanUpscaleFaceEnhanceSettingsFragment _settingsFragment = new();
    private readonly WanLowModelLoaderFragment _modelLoaderFragment = new();
    private readonly WanVideoUpscalePassFragment _upscalePassFragment = new();
    private readonly WanFaceEnhancePassFragment _faceEnhancePassFragment = new();
    private readonly SaveVideoFragment _saveVideoFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Upscale + Face Enhance",
        Description = "Upscales an existing video with the Wan 2.2 low T2V model, then optionally runs a CLIPSeg-guided face refinement pass. Use this as a utility workflow when a finished clip needs sharper detail rather than new motion generation.",
        Base = BlazorWebApp.Data.Enums.ModelBase.Wan,
        Mode = ModeType.Vid2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "LowModel",
                Label = "Low Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultLowModel,
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
                Order = 2,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "SpeedLora",
                Label = "Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultSpeedLora,
                Order = 3,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "TextEncoder",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = DefaultTextEncoder,
                Order = 4,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "UpscaleModel",
                Label = "Upscale Model",
                Type = AssetType.UpscaleModel,
                DefaultValue = DefaultUpscaleModel,
                Order = 5,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = SourceVideoId,
                Label = "Source Video",
                Type = SourceType.Video,
                Required = true,
                DefaultVideoOptions = new VideoSourceOptions
                {
                    ForceRate = 0,
                    CustomWidth = 0,
                    CustomHeight = 0,
                    FrameLoadCap = 81,
                    SkipFirstFrames = 0,
                    SelectEveryNth = 1,
                    Format = "None"
                }
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 2.2 T2V-A14B"],
        UsesDualModelLoras = false
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _settingsFragment;
        yield return _faceEnhancePassFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        var settings = _settingsFragment.Read(parameters);

        _loadVideoFragment.Build(builder, registry, new LoadVideoFragment.Parameters
        {
            Video = GetSourcePath(parameters.Sources.GetValueOrDefault(SourceVideoId)),
            ForceRate = 0,
            CustomWidth = 0,
            CustomHeight = 0,
            FrameLoadCap = settings.FrameLoadCap,
            SkipFirstFrames = settings.SkipFirstFrames,
            SelectEveryNth = settings.SelectEveryNth,
            Format = "None"
        });

        _modelLoaderFragment.Build(builder, registry, new WanLowModelLoaderFragment.Parameters
        {
            ModelName = parameters.Assets.GetValueOrDefault("LowModel") ?? DefaultLowModel,
            VaeName = parameters.Assets.GetValueOrDefault("Vae") ?? DefaultVae,
            LoraName = parameters.Assets.GetValueOrDefault("SpeedLora") ?? DefaultSpeedLora,
            BasePrecision = settings.BasePrecision,
            Quantization = settings.Quantization,
            LoadDevice = settings.LoadDevice,
            AttentionMode = settings.AttentionMode,
            VaePrecision = settings.VaePrecision,
            BlocksToSwap = settings.BlocksToSwap,
            UseNonBlocking = settings.UseNonBlocking,
            LoraStrength = settings.SpeedLoraStrength
        });

        _upscalePassFragment.Build(builder, registry, new WanVideoUpscalePassFragment.Parameters
        {
            UpscaleModelName = parameters.Assets.GetValueOrDefault("UpscaleModel") ?? DefaultUpscaleModel,
            TextEncoderName = parameters.Assets.GetValueOrDefault("TextEncoder") ?? DefaultTextEncoder,
            Settings = settings
        });

        _faceEnhancePassFragment.Build(builder, parameters, registry, scope: "face_", scopeTitle: "Face ");

        _saveVideoFragment.Build(builder, registry, new SaveVideoFragment.Parameters
        {
            NodeId = "final_video_output",
            FrameRate = settings.FrameRate,
            ImageInputName = "image_output",
            FilenamePrefix = "tmp/video",
            Format = "video/h264-mp4",
            PixFmt = "yuv420p",
            Crf = settings.Crf,
            Title = "Upscale + FaceEnhance Output"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private static string GetSourcePath(SourceAsset? source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(source.Filename)
            ? source.Filename
            : source.FilePath ?? string.Empty;
    }
}