using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using SourceAsset = BlazorWebApp.Models.SourceAsset;

namespace BlazorWebApp.Workflows.Templates.Wan;

public class WanVaceClipJoinerWorkflow : IWorkflowBuilder
{
    private const string DefaultHighModel = "Wan2_2-T2V-A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultLowModel = "Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultHighVaceModule = "Wan2_2_Fun_VACE_module_A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultLowVaceModule = "Wan2_2_Fun_VACE_module_A14B_LOW_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultClip = "umt5_xxl_fp8_e4m3fn_scaled.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultHighSpeedLora = "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_high_noise.safetensors";
    private const string DefaultLowSpeedLora = "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors";
    private const string HighSpeedLoraAsset = "HighSpeedLora";
    private const string LowSpeedLoraAsset = "LowSpeedLora";
    private const string FirstVideoSourceId = "first_video";
    private const string SecondVideoSourceId = "second_video";

    private readonly PromptsFragment _promptsFragment = new();
    private readonly WanVaceClipJoinerSettingsFragment _settingsFragment = new();
    private readonly WanVaceVideoComponentsFragment _videoComponentsFragment = new();
    private readonly LoadClipVaeFragment _loadClipVaeFragment = new();
    private readonly WanVaceKjModelLoaderFragment _modelLoaderFragment = new();
    private readonly WanModelOnlyLoraFragment _modelOnlyLoraFragment = new();
    private readonly WanVaceToVideoFragment _vaceToVideoFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly WanVaceImageBatchJoinFragment _imageBatchJoinFragment = new();
    private readonly WanCreateSaveVideoFragment _createSaveVideoFragment = new();
    private readonly WanFunInpaintSamplerFragment _samplerFragment = new(
        "sampler_advanced",
        "Video Sampling",
        FragmentType.Sampler,
        order: 40)
    {
        Defaults = new()
        {
            HighNodeId = "sampler_high",
            LowNodeId = "sampler_low",
            Seed = 496342861274979,
            LowSeed = 0,
            Steps = 10,
            Cfg = 1,
            SamplerName = "euler",
            Scheduler = "simple",
            StartAtStep = 0,
            SplitAtStep = 5,
            EndAtStep = 10000,
            LockEndAtSteps = false,
            HighModelInputName = "high_sampled_model_output",
            LowModelInputName = "low_sampled_model_output",
            LatentInputName = "latent_output",
            LatentOutputName = "latent_output",
            HighTitle = "KSampler High Noise",
            LowTitle = "KSampler Low Noise"
        }
    };

    public WorkflowMetadata Metadata => new()
    {
        Title = "VACE Clip Joiner",
        Description = "Joins two Wan video clips by generating the transition frames with the VACE high/low Wan 2.2 stack. Use this when two source clips need a short synthesized bridge while preserving the first clip's output frame rate.",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Vid2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "HighModel",
                Label = "High Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultHighModel,
                Order = 1,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "LowModel",
                Label = "Low Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultLowModel,
                Order = 2,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "HighVaceModule",
                Label = "High VACE Module",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultHighVaceModule,
                Order = 3,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "LowVaceModule",
                Label = "Low VACE Module",
                Type = AssetType.DiffusionModel,
                DefaultValue = DefaultLowVaceModule,
                Order = 4,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = DefaultClip,
                Order = 5,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
                Order = 6,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = HighSpeedLoraAsset,
                Label = "High Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultHighSpeedLora,
                Order = 7,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = LowSpeedLoraAsset,
                Label = "Low Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultLowSpeedLora,
                Order = 8,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = FirstVideoSourceId,
                Label = "First Video",
                Type = SourceType.Video,
                Required = true
            },
            new WorkflowSource
            {
                Id = SecondVideoSourceId,
                Label = "Second Video",
                Type = SourceType.Video,
                Required = true
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 2.2 T2V-A14B"],
        UsesDualModelLoras = false
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
        var settings = parameters.GetFragment(_settingsFragment.Metadata.Id);
        var highSpeedStrength = settings?.GetDouble(
            WanVaceClipJoinerSettingsFragment.HighSpeedLoraStrengthParameter,
            _settingsFragment.Defaults.HighSpeedLoraStrength) ?? _settingsFragment.Defaults.HighSpeedLoraStrength;
        var lowSpeedStrength = settings?.GetDouble(
            WanVaceClipJoinerSettingsFragment.LowSpeedLoraStrengthParameter,
            _settingsFragment.Defaults.LowSpeedLoraStrength) ?? _settingsFragment.Defaults.LowSpeedLoraStrength;

        _videoComponentsFragment.Build(builder, registry, new WanVaceVideoComponentsFragment.Parameters
        {
            FirstVideoPath = GetSourcePath(parameters.Sources?.GetValueOrDefault(FirstVideoSourceId)),
            SecondVideoPath = GetSourcePath(parameters.Sources?.GetValueOrDefault(SecondVideoSourceId))
        });

        _loadClipVaeFragment.Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? DefaultClip,
            ClipType = "wan",
            ClipDevice = "default",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? DefaultVae
        });

        _modelLoaderFragment.Build(builder, registry, new WanVaceKjModelLoaderFragment.Parameters
        {
            Branch = "high",
            ModelName = parameters.Assets?.GetValueOrDefault("HighModel") ?? DefaultHighModel,
            VaceModuleName = parameters.Assets?.GetValueOrDefault("HighVaceModule") ?? DefaultHighVaceModule,
            ModelOutputName = "high_model_output"
        });
        _modelLoaderFragment.Build(builder, registry, new WanVaceKjModelLoaderFragment.Parameters
        {
            Branch = "low",
            ModelName = parameters.Assets?.GetValueOrDefault("LowModel") ?? DefaultLowModel,
            VaceModuleName = parameters.Assets?.GetValueOrDefault("LowVaceModule") ?? DefaultLowVaceModule,
            ModelOutputName = "low_model_output"
        });

        var currentHighModel = ApplyFixedLora(
            builder,
            registry,
            "high",
            "high_model_output",
            parameters.Assets?.GetValueOrDefault(HighSpeedLoraAsset) ?? DefaultHighSpeedLora,
            highSpeedStrength,
            "High Speed LoRA");
        var currentLowModel = ApplyFixedLora(
            builder,
            registry,
            "low",
            "low_model_output",
            parameters.Assets?.GetValueOrDefault(LowSpeedLoraAsset) ?? DefaultLowSpeedLora,
            lowSpeedStrength,
            "Low Speed LoRA");
        AliasOutput(registry, currentHighModel, "high_sampled_model_output");
        AliasOutput(registry, currentLowModel, "low_sampled_model_output");

        _promptsFragment.Build(builder, parameters, registry);
        _settingsFragment.Build(builder, parameters, registry);
        _vaceToVideoFragment.Build(builder, registry, new WanVaceToVideoFragment.Parameters
        {
            BatchSize = 1,
            Strength = 1
        });
        _samplerFragment.Build(builder, parameters, registry);
        _vaeDecodeFragment.Build(builder, registry);
        _imageBatchJoinFragment.Build(builder, registry);
        _createSaveVideoFragment.Build(builder, registry, new WanCreateSaveVideoFragment.Parameters
        {
            CreateVideoNodeId = "create_video",
            SaveVideoNodeId = "save_video",
            ImageInputName = "image_output",
            FpsInputName = "video_1_fps",
            FilenamePrefix = "VACE joined",
            Format = "auto",
            Codec = "auto"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private string ApplyFixedLora(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string branch,
        string inputModelName,
        string loraName,
        double strength,
        string title)
    {
        if (strength <= 0 || string.IsNullOrWhiteSpace(loraName))
        {
            return inputModelName;
        }

        var outputName = $"{branch}_fixed_lora_model_output";
        _modelOnlyLoraFragment.Build(builder, registry, new WanModelOnlyLoraFragment.Parameters
        {
            NodeId = $"{branch}_fixed_speed_lora",
            LoraName = loraName,
            Strength = strength,
            ModelInputName = inputModelName,
            ModelOutputName = outputName,
            Title = title
        });

        return outputName;
    }

    private static void AliasOutput(NodeRegistry registry, string sourceOutputName, string aliasOutputName)
    {
        var (nodeId, outputIndex) = registry.GetRef(sourceOutputName);
        registry.Register(aliasOutputName, nodeId, outputIndex);
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