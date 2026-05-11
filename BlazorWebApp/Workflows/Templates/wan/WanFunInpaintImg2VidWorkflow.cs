using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using SourceAsset = BlazorWebApp.Models.SourceAsset;

namespace BlazorWebApp.Workflows.Templates.Wan;

public class WanFunInpaintImg2VidWorkflow : IWorkflowBuilder
{
    private const string DefaultHighModel = "Wan2_2-Fun-InP-A14B-HIGH_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultLowModel = "Wan2_2-Fun-InP-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors";
    private const string DefaultClip = "umt5_xxl_fp8_e4m3fn_scaled.safetensors";
    private const string DefaultVae = "wan_2.1_vae.safetensors";
    private const string DefaultHighSpeedLora = "Speed/Wan_2_2_I2V_A14B_HIGH_lightx2v_4step_lora_v1030_rank_64_bf16.safetensors";
    private const string DefaultLowSpeedLora = "Speed/wan2.2_i2v_A14b_low_noise_lora_rank64_lightx2v_4step_1022.safetensors";
    private const string StartImageSourceId = "start_image";
    private const string EndImageSourceId = "end_image";
    private const int DefaultFrameRate = 16;
    private const string DefaultNegativePrompt = "色调艳丽，过曝，静态，细节模糊不清，字幕，风格，作品，画作，画面，静止，整体发灰，最差质量，低质量，JPEG压缩残留，丑陋的，残缺的，多余的手指，画得不好的手部，画得不好的脸部，畸形的，毁容的，形态畸形的肢体，手指融合，静止不动的画面，杂乱的背景，三条腿，背景人很多，倒着走";

    private readonly PromptsFragment _promptsFragment = new()
    {
        Defaults = new() { Negative = DefaultNegativePrompt }
    };
    private readonly WanFunInpaintResolutionFragment _resolutionFragment = new();
    private readonly WanFunInpaintSettingsFragment _settingsFragment = new();
    private readonly LoadClipVaeFragment _loadClipVaeFragment = new();
    private readonly WanModelOnlyLoraFragment _modelOnlyLoraFragment = new();
    private readonly ModelSamplingSD3Fragment _modelSamplingSD3Fragment = new();
    private readonly WanFunInpaintImagePairFragment _imagePairFragment = new();
    private readonly WanFunInpaintToVideoFragment _funInpaintToVideoFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();
    private readonly FrameInterpolationFragment _frameInterpolationFragment = new();
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
            Seed = 221824495232956,
            LowSeed = 0,
            Steps = 4,
            Cfg = 1,
            SamplerName = "euler",
            Scheduler = "simple",
            StartAtStep = 0,
            SplitAtStep = 2,
            EndAtStep = 4,
            LockEndAtSteps = true,
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
        Title = "Fun Inpaint Img2Vid",
        Description = "Generates a Wan 2.2 video transition from a start image to an end image with the Fun Inpaint dual high/low model stack. Pick this when you want direct control over both sampler stages and optional SeedVR2 or frame interpolation passes.",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
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
                Parameter = "Clip",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = DefaultClip,
                Order = 3,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = DefaultVae,
                Order = 4,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "HighSpeedLora",
                Label = "High Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultHighSpeedLora,
                Order = 5,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "LowSpeedLora",
                Label = "Low Speed LoRA",
                Type = AssetType.Lora,
                DefaultValue = DefaultLowSpeedLora,
                Order = 6,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = StartImageSourceId,
                Label = "Start Image",
                Type = SourceType.Image,
                Required = true
            },
            new WorkflowSource
            {
                Id = EndImageSourceId,
                Label = "End Image",
                Type = SourceType.Image,
                Required = true
            }
        ],
        CompatibleResourceBaseModels = ["Wan Video 2.2 I2V-A14B"],
        UsesDualModelLoras = true
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _resolutionFragment;
        yield return _settingsFragment;
        yield return _samplerFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _frameInterpolationFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var resolutionFragment = parameters.GetFragment(_resolutionFragment.Metadata.Id);
        var settingsFragment = parameters.GetFragment(_settingsFragment.Metadata.Id);
        var widthCeiling = resolutionFragment?.GetInt("width", _resolutionFragment.Defaults.Width) ?? _resolutionFragment.Defaults.Width;
        var heightCeiling = resolutionFragment?.GetInt("height", _resolutionFragment.Defaults.Height) ?? _resolutionFragment.Defaults.Height;
        var batchSize = resolutionFragment?.GetInt("batch_size", _resolutionFragment.Defaults.BatchSize) ?? _resolutionFragment.Defaults.BatchSize;
        var length = settingsFragment?.GetInt("length", _settingsFragment.Defaults.Length) ?? _settingsFragment.Defaults.Length;
        var shift = settingsFragment?.GetDouble("shift", _settingsFragment.Defaults.Shift) ?? _settingsFragment.Defaults.Shift;
        var highSpeedStrength = settingsFragment?.GetDouble("high_speed_lora_strength", _settingsFragment.Defaults.HighSpeedLoraStrength) ?? _settingsFragment.Defaults.HighSpeedLoraStrength;
        var lowSpeedStrength = settingsFragment?.GetDouble("low_speed_lora_strength", _settingsFragment.Defaults.LowSpeedLoraStrength) ?? _settingsFragment.Defaults.LowSpeedLoraStrength;

        BuildUnet(builder, registry, "high", parameters.Assets?.GetValueOrDefault("HighModel") ?? DefaultHighModel, "Load High Noise Model");
        BuildUnet(builder, registry, "low", parameters.Assets?.GetValueOrDefault("LowModel") ?? DefaultLowModel, "Load Low Noise Model");

        _loadClipVaeFragment.Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? DefaultClip,
            ClipType = "wan",
            ClipDevice = "default",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? DefaultVae
        });

        var currentHighModel = ApplyFixedLora(
            builder,
            registry,
            "high",
            "high_model_output",
            parameters.Assets?.GetValueOrDefault("HighSpeedLora") ?? DefaultHighSpeedLora,
            highSpeedStrength,
            "High Speed LoRA");
        var currentLowModel = ApplyFixedLora(
            builder,
            registry,
            "low",
            "low_model_output",
            parameters.Assets?.GetValueOrDefault("LowSpeedLora") ?? DefaultLowSpeedLora,
            lowSpeedStrength,
            "Low Speed LoRA");

        currentHighModel = ApplyUserLoras(builder, registry, parameters, currentHighModel, "high");
        currentLowModel = ApplyUserLoras(builder, registry, parameters, currentLowModel, "low");

        _modelSamplingSD3Fragment.Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_model_sampling",
            Shift = shift,
            ModelInputName = currentHighModel,
            ModelOutputName = "high_sampled_model_output",
            Title = "ModelSamplingSD3 High"
        });
        _modelSamplingSD3Fragment.Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "low_model_sampling",
            Shift = shift,
            ModelInputName = currentLowModel,
            ModelOutputName = "low_sampled_model_output",
            Title = "ModelSamplingSD3 Low"
        });

        _promptsFragment.Build(builder, parameters, registry);

        _imagePairFragment.Build(builder, registry, new WanFunInpaintImagePairFragment.Parameters
        {
            StartImagePath = GetSourcePath(parameters.Sources?.GetValueOrDefault(StartImageSourceId)),
            EndImagePath = GetSourcePath(parameters.Sources?.GetValueOrDefault(EndImageSourceId)),
            MaxLongerEdge = Math.Max(widthCeiling, heightCeiling)
        });

        _funInpaintToVideoFragment.Build(builder, registry, new WanFunInpaintToVideoFragment.Parameters
        {
            Length = length,
            BatchSize = batchSize
        });

        _samplerFragment.Build(builder, parameters, registry);

        _vaeDecodeFragment.Build(builder, registry);

        _seedVR2UpscaleFragment.Build(builder, parameters, registry, length);

        var frameRate = (double)DefaultFrameRate;
        var imageInputName = "image_output";
        var interpolationFragment = parameters.GetFragment(_frameInterpolationFragment.Metadata.Id);
        if (interpolationFragment?.IsActive == true)
        {
            var multiplier = interpolationFragment.GetInt("frame_multiplier", 2);
            _frameInterpolationFragment.Build(builder, registry, new FrameInterpolationFragment.Parameters
            {
                RifeModel = interpolationFragment.GetString("rife_model", "rife49.pth") ?? "rife49.pth",
                FrameMultiplier = multiplier,
                ScaleBy = interpolationFragment.GetDouble("scale_by", 2.0),
                ImageInputName = "image_output"
            });
            frameRate *= multiplier;
            imageInputName = "frames_output";
        }

        _createSaveVideoFragment.Build(builder, registry, new WanCreateSaveVideoFragment.Parameters
        {
            CreateVideoNodeId = "create_video",
            SaveVideoNodeId = "save_video",
            ImageInputName = imageInputName,
            Fps = frameRate,
            FilenamePrefix = "video/ComfyUI",
            Format = "auto",
            Codec = "auto"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private static void BuildUnet(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string branch,
        string modelName,
        string title)
    {
        var nodeId = $"{branch}_unet_loader";
        builder.AddNode(nodeId, node => node
            .Type("UNETLoader")
            .Title(title)
            .Input("unet_name", modelName)
            .Input("weight_dtype", "default"));

        registry.Register($"{branch}_model_output", nodeId, 0);
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

    private string ApplyUserLoras(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        string inputModelName,
        string branch)
    {
        var currentModel = inputModelName;
        if (parameters.Loras is null)
        {
            return currentModel;
        }

        var index = 0;
        foreach (var lora in parameters.Loras.Where(lora => lora.IsEnabled))
        {
            var loraPath = branch == "high" ? lora.HighPath : lora.LowPath;
            if (string.IsNullOrWhiteSpace(loraPath))
            {
                continue;
            }

            var outputName = $"{branch}_user_lora_model_output_{index}";
            _modelOnlyLoraFragment.Build(builder, registry, new WanModelOnlyLoraFragment.Parameters
            {
                NodeId = $"{branch}_user_lora_{index}",
                LoraName = loraPath,
                Strength = lora.Strength,
                ModelInputName = currentModel,
                ModelOutputName = outputName,
                Title = $"{(branch == "high" ? "High" : "Low")} User LoRA {index + 1}"
            });
            currentModel = outputName;
            index++;
        }

        return currentModel;
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
