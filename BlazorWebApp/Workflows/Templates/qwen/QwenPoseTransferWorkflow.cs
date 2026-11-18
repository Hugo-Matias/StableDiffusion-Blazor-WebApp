using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;
using AssetType = BlazorWebApp.Workflows.Models.AssetType;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using WorkflowAsset = BlazorWebApp.Workflows.Models.WorkflowAsset;
using WorkflowSource = BlazorWebApp.Workflows.Models.WorkflowSource;

namespace BlazorWebApp.Workflows.Templates.Qwen;

public class QwenPoseTransferWorkflow : IWorkflowBuilder
{
    private const string TargetImageSourceId = "target_image";
    private const string PoseReferenceSourceId = "pose_reference_image";
    private const string ModelAsset = "Model";
    private const string ClipAsset = "Clip";
    private const string VaeAsset = "Vae";
    private const string PoseCheckpointAsset = "PoseCheckpoint";
    private const string LightningLoraAsset = "LightningLora";
    private const string ConsistencyLoraAsset = "ConsistencyLora";

    private readonly PromptsFragment _promptsFragment = new();
    private readonly QwenPoseTransferSettingsFragment _settingsFragment = new();
    private readonly LoadQwenPoseTransferFragment _loadQwenPoseTransferFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly QwenPoseTransferImageFragment _imageFragment = new();
    private readonly QwenPoseTransferConditioningFragment _conditioningFragment = new();
    private readonly SamplerStandardFragment _samplerFragment = new()
    {
        Defaults = new()
        {
            SamplerName = "euler",
            Scheduler = "simple",
            Steps = 8,
            Cfg = 1.0,
            Denoise = 1.0
        }
    };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Pose Transfer",
        Description = "Transfers the pose from a reference image onto a target image using SDPose keypoints and Qwen image-edit conditioning. Use it when you want a two-image edit where the second image provides body pose rather than visual identity.",
        Base = Data.Enums.ModelBase.Qwen,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = ModelAsset,
                Label = "Qwen Edit Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "qwen_image_edit_2511_fp8mixed.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = ClipAsset,
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_2.5_vl_7b_fp8_scaled.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = VaeAsset,
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "qwen_image_vae.safetensors",
                Order = 3,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = PoseCheckpointAsset,
                Label = "SDPose Checkpoint",
                Type = AssetType.CheckpointModel,
                DefaultValue = "SDPose/sdpose_wholebody_fp16.safetensors",
                Order = 4,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = LightningLoraAsset,
                Label = "Lightning LoRA",
                Type = AssetType.Lora,
                DefaultValue = "Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors",
                Order = 5,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = ConsistencyLoraAsset,
                Label = "Consistency LoRA",
                Type = AssetType.Lora,
                DefaultValue = "Util/qe2511_consis_alpha_patched.safetensors",
                Order = 6,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = TargetImageSourceId,
                Label = "Target Image",
                Type = SourceType.Image,
                Required = true
            },
            new WorkflowSource
            {
                Id = PoseReferenceSourceId,
                Label = "Pose Reference",
                Type = SourceType.Image,
                Required = true
            }
        ],
        CompatibleResourceBaseModels = ["Qwen", "Qwen 2"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _settingsFragment;
        yield return _samplerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        var settings = ResolveSettings(parameters);
        var prompts = parameters.GetFragment(_promptsFragment.Metadata.Id);
        var sampler = parameters.GetFragment(_samplerFragment.Metadata.Id);

        _loadQwenPoseTransferFragment.Build(builder, registry, new LoadQwenPoseTransferFragment.Parameters
        {
            UnetName = parameters.Assets.GetValueOrDefault(ModelAsset) ?? "qwen_image_edit_2511_fp8mixed.safetensors",
            ClipName = parameters.Assets.GetValueOrDefault(ClipAsset) ?? "qwen_2.5_vl_7b_fp8_scaled.safetensors",
            VaeName = parameters.Assets.GetValueOrDefault(VaeAsset) ?? "qwen_image_vae.safetensors",
            LightningLoraName = parameters.Assets.GetValueOrDefault(LightningLoraAsset) ?? "Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors",
            ConsistencyLoraName = parameters.Assets.GetValueOrDefault(ConsistencyLoraAsset) ?? "Util/qe2511_consis_alpha_patched.safetensors",
            LightningLoraStrength = settings.LightningLoraStrength,
            ConsistencyLoraStrength = settings.ConsistencyLoraStrength
        });

        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        _imageFragment.Build(builder, registry, new QwenPoseTransferImageFragment.Parameters
        {
            TargetImage = GetSourcePath(parameters, TargetImageSourceId),
            PoseReferenceImage = GetSourcePath(parameters, PoseReferenceSourceId),
            PoseCheckpoint = parameters.Assets.GetValueOrDefault(PoseCheckpointAsset) ?? "SDPose/sdpose_wholebody_fp16.safetensors",
            ScaleLength = settings.ScaleLength,
            PoseBatchSize = settings.PoseBatchSize,
            DrawBody = settings.DrawBody,
            DrawHands = settings.DrawHands,
            DrawFace = settings.DrawFace,
            DrawFeet = settings.DrawFeet,
            StickWidth = settings.StickWidth,
            FacePointSize = settings.FacePointSize,
            ScoreThreshold = settings.ScoreThreshold
        });

        _conditioningFragment.Build(builder, registry, new QwenPoseTransferConditioningFragment.Parameters
        {
            Prompt = prompts?.GetString("positive", _promptsFragment.Defaults.Positive) ?? _promptsFragment.Defaults.Positive,
            LatentBatchSize = 1
        });

        var resolvedSeed = sampler?.GetLong("seed", -1L) ?? -1L;
        if (resolvedSeed < 0)
        {
            resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        _samplerFragment.Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "KSampler",
            SamplerName = sampler?.GetString("sampler_name", _samplerFragment.Defaults.SamplerName) ?? _samplerFragment.Defaults.SamplerName,
            Scheduler = sampler?.GetString("scheduler", _samplerFragment.Defaults.Scheduler) ?? _samplerFragment.Defaults.Scheduler,
            Steps = sampler?.GetInt("steps", _samplerFragment.Defaults.Steps) ?? _samplerFragment.Defaults.Steps,
            Cfg = sampler?.GetDouble("cfg", _samplerFragment.Defaults.Cfg) ?? _samplerFragment.Defaults.Cfg,
            Denoise = sampler?.GetDouble("denoise", _samplerFragment.Defaults.Denoise) ?? _samplerFragment.Defaults.Denoise,
            Seed = resolvedSeed
        });

        _vaeDecodeFragment.Build(builder, registry);
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters());

        return builder.ToComfyWorkflow(registry);
    }

    private QwenPoseTransferSettingsFragment.Parameters ResolveSettings(GenerationParameters parameters)
    {
        var defaults = _settingsFragment.Defaults;
        var fragment = parameters.GetFragment(_settingsFragment.Metadata.Id);

        return new QwenPoseTransferSettingsFragment.Parameters
        {
            ScaleLength = fragment?.GetInt("scale_length", defaults.ScaleLength) ?? defaults.ScaleLength,
            PoseBatchSize = fragment?.GetInt("pose_batch_size", defaults.PoseBatchSize) ?? defaults.PoseBatchSize,
            DrawBody = fragment?.GetBool("draw_body", defaults.DrawBody) ?? defaults.DrawBody,
            DrawHands = fragment?.GetBool("draw_hands", defaults.DrawHands) ?? defaults.DrawHands,
            DrawFace = fragment?.GetBool("draw_face", defaults.DrawFace) ?? defaults.DrawFace,
            DrawFeet = fragment?.GetBool("draw_feet", defaults.DrawFeet) ?? defaults.DrawFeet,
            StickWidth = fragment?.GetInt("stick_width", defaults.StickWidth) ?? defaults.StickWidth,
            FacePointSize = fragment?.GetInt("face_point_size", defaults.FacePointSize) ?? defaults.FacePointSize,
            ScoreThreshold = fragment?.GetDouble("score_threshold", defaults.ScoreThreshold) ?? defaults.ScoreThreshold,
            LightningLoraStrength = fragment?.GetDouble("lightning_lora_strength", defaults.LightningLoraStrength) ?? defaults.LightningLoraStrength,
            ConsistencyLoraStrength = fragment?.GetDouble("consistency_lora_strength", defaults.ConsistencyLoraStrength) ?? defaults.ConsistencyLoraStrength
        };
    }

    private static string GetSourcePath(GenerationParameters parameters, string sourceId)
    {
        var source = parameters.Sources.GetValueOrDefault(sourceId);
        return source?.Filename ?? source?.FilePath ?? string.Empty;
    }
}
