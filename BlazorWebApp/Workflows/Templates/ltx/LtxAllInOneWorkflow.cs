using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Ltx;

public class LtxAllInOneWorkflow : IWorkflowBuilder
{
    private readonly LtxAllInOneLoadFragment _loadFragment = new();
    private readonly LtxAllInOneLoaderSettingsFragment _loaderSettingsFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxSourceSagePatchFragment _sagePatchFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LtxAllInOneVideoSettingsFragment _videoSettingsFragment = new();
    private readonly LtxFrameGuidesFragment _frameGuidesFragment = new();
    private readonly LtxAllInOneSamplerFragment _samplerFragment = new();
    private readonly LtxLoadOptionalImageFragment _loadImageFragment = new();
    private readonly LtxEmptyLatentFragment _emptyLatentFragment = new();
    private readonly LtxLoadAudioFragment _loadAudioFragment = new();
    private readonly LtxAudioVaeEncodeFragment _audioVaeEncodeFragment = new();
    private readonly LtxConditioningFragment _conditioningFragment = new();
    private readonly LtxAddGuideFragment _addGuideFragment = new();
    private readonly LtxConcatAVLatentFragment _concatAVFragment = new();
    private readonly LtxVideoFlowSigmasFragment _videoFlowSigmasFragment = new();
    private readonly LtxSamplingPassFragment _samplingPassFragment = new();
    private readonly LtxUpsampleLatentFragment _upsampleLatentFragment = new();
    private readonly LtxFrameInterpolationFragment _frameInterpolationFragment = new();
    private readonly LtxVhsDecodeFragment _decodeFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "LTX 2.3 VideoFlow",
        Description = "Creates LTX 2.3 video from text, optional first and last frame guides, and an optional audio track. " +
                      "Use it when you want one flexible LTX workflow that falls back to text-to-video when no images are supplied.",
        Base = Data.Enums.ModelBase.LTX,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset { Parameter = "Checkpoint", Label = "Checkpoint", Type = AssetType.CheckpointModel, DefaultValue = "Photography/sulphur_dev_fp8mixed.safetensors", Order = 1, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "UNet", Label = "Diffusion Model", Type = AssetType.DiffusionModel, DefaultValue = "ltx-2.3-22b-distilled-1.1_transformer_only_mxfp8_block32.safetensors", Order = 2, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "Clip", Label = "Text Encoder (Gemma)", Type = AssetType.Clip, DefaultValue = "gemma_3_12B_it_fp4_mixed.safetensors", Order = 3, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "Clip2", Label = "Text Encoder (LTX projection)", Type = AssetType.Clip, DefaultValue = "ltx-2.3_text_projection_bf16.safetensors", Order = 4, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "VideoVae", Label = "Video VAE", Type = AssetType.Vae, DefaultValue = "LTX23_video_vae_bf16.safetensors", Order = 5, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "AudioVae", Label = "Audio VAE", Type = AssetType.Vae, DefaultValue = "LTX23_audio_vae_bf16.safetensors", Order = 6, ColumnSize = 6 },
            new WorkflowAsset { Parameter = "UpscaleModel", Label = "Spatial Upscaler", Type = AssetType.LatentUpscaleModel, DefaultValue = "ltx-2.3-spatial-upscaler-x2-1.1.safetensors", Order = 7, ColumnSize = 6 }
        ],
        Sources =
        [
            new WorkflowSource { Id = "first_frame", Label = "First Frame", Type = SourceType.Image, Required = false, Parameter = "first_frame" },
            new WorkflowSource { Id = "last_frame", Label = "Last Frame", Type = SourceType.Image, Required = false, Parameter = "last_frame" },
            new WorkflowSource { Id = "audio_track", Label = "Audio Track", Type = SourceType.Audio, Required = false, Parameter = "audio" }
        ],
        CompatibleResourceBaseModels = ["LTXV2", "LTXV 2.3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _loaderSettingsFragment;
        yield return _videoSettingsFragment;
        yield return _frameGuidesFragment;
        yield return _samplerFragment;
        yield return _sagePatchFragment;
        yield return _frameInterpolationFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var videoSettings = parameters.GetFragment(_videoSettingsFragment.Metadata.Id);
        var videoDefaults = _videoSettingsFragment.Defaults;
        var width = videoSettings?.GetInt("width", videoDefaults.Width) ?? videoDefaults.Width;
        var height = videoSettings?.GetInt("height", videoDefaults.Height) ?? videoDefaults.Height;
        var duration = videoSettings?.GetInt("duration", videoDefaults.Duration) ?? videoDefaults.Duration;
        var frameRate = videoSettings?.GetInt("frame_rate", videoDefaults.FrameRate) ?? videoDefaults.FrameRate;
        var imgCompression = videoSettings?.GetInt("img_compression", videoDefaults.ImgCompression) ?? videoDefaults.ImgCompression;
        var frameCount = duration * frameRate + 1;
        var pass1Width = width / 2;
        var pass1Height = height / 2;

        var loaderSettings = parameters.GetFragment(_loaderSettingsFragment.Metadata.Id);
        var loaderDefaults = _loaderSettingsFragment.Defaults;
        _loadFragment.Build(builder, registry, new LtxAllInOneLoadFragment.Parameters
        {
            LoaderMode = loaderSettings?.GetString("loader_mode", loaderDefaults.LoaderMode) ?? loaderDefaults.LoaderMode,
            CheckpointName = parameters.Assets?.GetValueOrDefault("Checkpoint") ?? "Photography/sulphur_dev_fp8mixed.safetensors",
            UnetName = parameters.Assets?.GetValueOrDefault("UNet") ?? "ltx-2.3-22b-distilled-1.1_transformer_only_mxfp8_block32.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "gemma_3_12B_it_fp4_mixed.safetensors",
            ClipName2 = parameters.Assets?.GetValueOrDefault("Clip2") ?? "ltx-2.3_text_projection_bf16.safetensors",
            VideoVaeName = parameters.Assets?.GetValueOrDefault("VideoVae") ?? "LTX23_video_vae_bf16.safetensors",
            AudioVaeName = parameters.Assets?.GetValueOrDefault("AudioVae") ?? "LTX23_audio_vae_bf16.safetensors",
            UpscaleModelName = parameters.Assets?.GetValueOrDefault("UpscaleModel") ?? "ltx-2.3-spatial-upscaler-x2-1.1.safetensors",
            UnetWeightDtype = loaderSettings?.GetString("unet_weight_dtype", loaderDefaults.UnetWeightDtype) ?? loaderDefaults.UnetWeightDtype,
            VaeWeightDtype = loaderSettings?.GetString("vae_weight_dtype", loaderDefaults.VaeWeightDtype) ?? loaderDefaults.VaeWeightDtype,
            UseSeparateVideoVae = loaderSettings?.GetBool("use_separate_video_vae", loaderDefaults.UseSeparateVideoVae) ?? loaderDefaults.UseSeparateVideoVae
        });

        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        if (parameters.GetFragment(_sagePatchFragment.Metadata.Id)?.IsActive != false)
        {
            _sagePatchFragment.BuildPatch(builder, registry);
        }

        var hasFirstFrame = TryGetSourcePath(parameters, "first_frame", out var firstFramePath);
        var hasLastFrame = TryGetSourcePath(parameters, "last_frame", out var lastFramePath);
        var hasAudio = TryGetSourcePath(parameters, "audio_track", out var audioPath);

        if (hasFirstFrame)
        {
            _loadImageFragment.Build(builder, registry, new LtxLoadOptionalImageFragment.Parameters
            {
                NodePrefix = "ltx_first_frame",
                Title = "First Frame",
                ImagePath = firstFramePath,
                OutputName = "first_frame_image",
                Width = width,
                Height = height,
                ImgCompression = imgCompression
            });
        }

        if (hasLastFrame)
        {
            _loadImageFragment.Build(builder, registry, new LtxLoadOptionalImageFragment.Parameters
            {
                NodePrefix = "ltx_last_frame",
                Title = "Last Frame",
                ImagePath = lastFramePath,
                OutputName = "last_frame_image",
                Width = width,
                Height = height,
                ImgCompression = imgCompression
            });
        }

        var promptsData = parameters.GetFragment(_promptsFragment.Metadata.Id);
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsData?.GetString("positive", "") ?? "",
            Negative = promptsData?.GetString("negative", "") ?? ""
        });

        _conditioningFragment.Build(builder, registry, new LtxConditioningFragment.Parameters
        {
            FrameRate = frameRate
        });

        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = pass1Width,
            Height = pass1Height,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = 24,
            SkipAudio = hasAudio
        });

        if (hasAudio)
        {
            _loadAudioFragment.Build(builder, registry, new LtxLoadAudioFragment.Parameters
            {
                AudioPath = audioPath,
                DurationSeconds = duration,
                StartTime = 0.0
            });

            _audioVaeEncodeFragment.Build(builder, registry, new LtxAudioVaeEncodeFragment.Parameters
            {
                MaskWidth = 427,
                MaskHeight = 512,
                MaskValue = 0.0
            });
        }

        var guideSettings = parameters.GetFragment(_frameGuidesFragment.Metadata.Id);
        var guideDefaults = _frameGuidesFragment.Defaults;
        var firstStrength = ResolveGuideStrength(guideSettings, "first_strength", "stage1_first_strength", guideDefaults.FirstStrength);
        var lastStrength = ResolveGuideStrength(guideSettings, "last_strength", "stage1_last_strength", guideDefaults.LastStrength);

        if (hasFirstFrame && firstStrength > 0)
        {
            AddFrameGuide(builder, registry, "stage1_first_frame_guide", "First Frame Guide (Stage 1)", "first_frame_image", 0, firstStrength);
        }

        if (hasLastFrame && lastStrength > 0)
        {
            AddFrameGuide(builder, registry, "stage1_last_frame_guide", "Last Frame Guide (Stage 1)", "last_frame_image", -1, lastStrength);
        }

        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var samplerDefaults = _samplerFragment.Defaults;
        var isImageToVideo = hasFirstFrame || hasLastFrame;
        var modeSamplerDefaults = isImageToVideo
            ? LtxAllInOneSamplerFragment.CreateImageToVideoDefaults()
            : LtxAllInOneSamplerFragment.CreateTextToVideoDefaults();
        var cfg = samplerSettings?.GetDouble("cfg", samplerDefaults.Cfg) ?? samplerDefaults.Cfg;
        var stage1Seed = ResolveSeed(samplerSettings?.GetLong("stage1_seed", samplerDefaults.Stage1Seed) ?? samplerDefaults.Stage1Seed);
        var stage2Seed = ResolveSeed(samplerSettings?.GetLong("stage2_seed", samplerDefaults.Stage2Seed) ?? samplerDefaults.Stage2Seed);
        var stage1Sampler = samplerSettings?.GetString("stage1_sampler", modeSamplerDefaults.Stage1Sampler) ?? modeSamplerDefaults.Stage1Sampler;
        var stage2Sampler = samplerSettings?.GetString("stage2_sampler", modeSamplerDefaults.Stage2Sampler) ?? modeSamplerDefaults.Stage2Sampler;
        var useManualSigmas = samplerSettings?.GetBool("use_manual_sigmas", samplerDefaults.UseManualSigmas) ?? samplerDefaults.UseManualSigmas;
        var sigmaMode = samplerSettings?.GetString("sigma_mode", modeSamplerDefaults.SigmaMode) ?? modeSamplerDefaults.SigmaMode;
        if (useManualSigmas)
        {
            sigmaMode = "manual";
        }

        var scheduler = samplerSettings?.GetString("scheduler", modeSamplerDefaults.Scheduler) ?? modeSamplerDefaults.Scheduler;
        var stage1VideoFlowTerminalDefault = modeSamplerDefaults.Stage1VideoFlowTerminal;
        var stage2VideoFlowStartDefault = modeSamplerDefaults.Stage2VideoFlowStart;
        var stage2VideoFlowTerminalDefault = modeSamplerDefaults.Stage2VideoFlowTerminal;
        var legacyVideoFlowTerminal = samplerSettings?.Values.ContainsKey("videoflow_terminal") == true
            ? samplerSettings.GetDouble("videoflow_terminal", stage1VideoFlowTerminalDefault)
            : stage1VideoFlowTerminalDefault;
        var stage1VideoFlowTerminal = samplerSettings?.GetDouble("stage1_videoflow_terminal", legacyVideoFlowTerminal) ?? legacyVideoFlowTerminal;
        var stage2VideoFlowStart = samplerSettings?.GetDouble("stage2_videoflow_start", stage2VideoFlowStartDefault) ?? stage2VideoFlowStartDefault;
        var stage2VideoFlowTerminal = samplerSettings?.GetDouble("stage2_videoflow_terminal", stage2VideoFlowTerminalDefault) ?? stage2VideoFlowTerminalDefault;
        var videoFlowMidpoint = samplerSettings?.GetDouble("videoflow_midpoint", samplerDefaults.VideoFlowMidpoint) ?? samplerDefaults.VideoFlowMidpoint;
        var legacyVideoFlowShift = samplerSettings?.GetDouble("videoflow_flatness", samplerDefaults.Stage1VideoFlowShift) ?? samplerDefaults.Stage1VideoFlowShift;
        var stage1VideoFlowShift = samplerSettings?.GetDouble("stage1_videoflow_shift", legacyVideoFlowShift) ?? legacyVideoFlowShift;
        var stage2VideoFlowShift = samplerSettings?.GetDouble("stage2_videoflow_shift", samplerDefaults.Stage2VideoFlowShift) ?? samplerDefaults.Stage2VideoFlowShift;
        var useBasicSigmas = string.Equals(sigmaMode, "basic", StringComparison.OrdinalIgnoreCase);
        var useVideoFlowSigmas = string.Equals(sigmaMode, "videoflow", StringComparison.OrdinalIgnoreCase);
        var useManualSigmaMode = string.Equals(sigmaMode, "manual", StringComparison.OrdinalIgnoreCase);
        var stage1BasicSteps = samplerSettings?.GetInt("stage1_basic_steps", modeSamplerDefaults.Stage1BasicSteps) ?? modeSamplerDefaults.Stage1BasicSteps;
        var stage2BasicSteps = samplerSettings?.GetInt("stage2_basic_steps", modeSamplerDefaults.Stage2BasicSteps) ?? modeSamplerDefaults.Stage2BasicSteps;
        var stage1VideoFlowSteps = samplerSettings?.GetInt("stage1_steps", modeSamplerDefaults.Stage1Steps) ?? modeSamplerDefaults.Stage1Steps;
        var stage2VideoFlowSteps = samplerSettings?.GetInt("stage2_steps", modeSamplerDefaults.Stage2Steps) ?? modeSamplerDefaults.Stage2Steps;
        var stage1Denoise = samplerSettings?.GetDouble("stage1_denoise", modeSamplerDefaults.Stage1Denoise) ?? modeSamplerDefaults.Stage1Denoise;
        var stage2Denoise = samplerSettings?.GetDouble("stage2_denoise", modeSamplerDefaults.Stage2Denoise) ?? modeSamplerDefaults.Stage2Denoise;

        if (useBasicSigmas)
        {
            BuildBasicScheduler(builder, registry, "stage1_basic_scheduler", "BasicScheduler (Stage 1)", scheduler,
                stage1BasicSteps,
                stage1Denoise,
                "stage1_sigmas_output");
        }
        else if (useVideoFlowSigmas)
        {
            _videoFlowSigmasFragment.Build(builder, registry, new LtxVideoFlowSigmasFragment.Parameters
            {
                PassId = "stage1",
                OutputName = "stage1_sigmas_output",
                Title = "Stage 1 VideoFlow",
                Steps = stage1VideoFlowSteps,
                Start = 1.0,
                Terminal = stage1VideoFlowTerminal,
                Midpoint = videoFlowMidpoint,
                Flatness = stage1VideoFlowShift
            });
        }

        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "stage1",
            Seed = stage1Seed,
            Cfg = cfg,
            SamplerName = stage1Sampler,
            UseRegistrySigmas = !useManualSigmaMode,
            SigmasInputName = "stage1_sigmas_output",
            Sigmas = samplerSettings?.GetString("stage1_sigmas", samplerDefaults.Stage1Sigmas) ?? samplerDefaults.Stage1Sigmas,
            PositiveInputName = "ltx_positive_output",
            NegativeInputName = "ltx_negative_output",
            Title = "SamplerCustomAdvanced (Stage 1)"
        });

        _upsampleLatentFragment.Build(builder, registry, new LtxUpsampleLatentFragment.Parameters
        {
            // For I2V / A2V the stage-1 LTXVAddGuide(s) injected conditioning frames that
            // must be cropped before upsampling, otherwise the guide image bleeds into
            // the late frames of the decoded video. Cond refs are re-registered in place
            // so the stage-2 frame guide downstream pulls the cropped cond.
            PositiveInputName = isImageToVideo ? "ltx_positive_output" : null,
            NegativeInputName = isImageToVideo ? "ltx_negative_output" : null
        });

        const string stage2PositiveInputName = "ltx_positive_output";
        const string stage2NegativeInputName = "ltx_stage2_negative_output";
        _conditioningFragment.BuildZeroNegative(builder, registry, stage2PositiveInputName, stage2NegativeInputName, "ltx_stage2_zero_negative");

        if (hasFirstFrame && firstStrength > 0)
        {
            AddFrameGuide(builder, registry, "stage2_first_frame_guide", "First Frame Guide (Stage 2)", "first_frame_image", 0, firstStrength,
            positiveInputName: stage2PositiveInputName, negativeInputName: stage2NegativeInputName);
        }

        if (hasLastFrame && lastStrength > 0)
        {
            AddFrameGuide(builder, registry, "stage2_last_frame_guide", "Last Frame Guide (Stage 2)", "last_frame_image", -1, lastStrength,
            positiveInputName: stage2PositiveInputName, negativeInputName: stage2NegativeInputName);
        }

        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass2",
            Title = "LTXVConcatAVLatent (Pass 2)"
        });

        if (useBasicSigmas)
        {
            BuildBasicScheduler(builder, registry, "stage2_basic_scheduler", "BasicScheduler (Stage 2)", scheduler,
                stage2BasicSteps,
                stage2Denoise,
                "stage2_sigmas_output");
        }
        else if (useVideoFlowSigmas)
        {
            _videoFlowSigmasFragment.Build(builder, registry, new LtxVideoFlowSigmasFragment.Parameters
            {
                PassId = "stage2",
                OutputName = "stage2_sigmas_output",
                Title = "Stage 2 VideoFlow",
                Steps = stage2VideoFlowSteps,
                Start = stage2VideoFlowStart,
                Terminal = stage2VideoFlowTerminal,
                Midpoint = videoFlowMidpoint,
                Flatness = stage2VideoFlowShift,
                Variant = LtxVideoFlowSigmasFragment.LtxVideoFlowVariant.Stage2
            });
        }

        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "stage2",
            Seed = stage2Seed,
            Cfg = cfg,
            SamplerName = stage2Sampler,
            UseRegistrySigmas = !useManualSigmaMode,
            SigmasInputName = "stage2_sigmas_output",
            Sigmas = samplerSettings?.GetString("stage2_sigmas", samplerDefaults.Stage2Sigmas) ?? samplerDefaults.Stage2Sigmas,
            PositiveInputName = stage2PositiveInputName,
            NegativeInputName = stage2NegativeInputName,
            Title = "SamplerCustomAdvanced (Stage 2)"
        });

        _decodeFragment.Build(builder, registry, new LtxVhsDecodeFragment.Parameters
        {
            FrameRate = frameRate,
            SaveVideo = false,
            // I2V / A2V: strip the conditioning-frame padding added by the stage-2 LTXVAddGuide
            // before decoding, matching the reference Comfy graph (node 1194:1179).
            PositiveInputName = isImageToVideo ? stage2PositiveInputName : null,
            NegativeInputName = isImageToVideo ? stage2NegativeInputName : null
        });

        var finalFrameRate = frameRate;
        var interpolationFragment = parameters.GetFragment(_frameInterpolationFragment.Metadata.Id);
        if (interpolationFragment?.IsActive != false)
        {
            _frameInterpolationFragment.Build(builder, parameters, registry);
            finalFrameRate *= interpolationFragment?.GetInt("frame_multiplier", _frameInterpolationFragment.Defaults.FrameMultiplier)
                ?? _frameInterpolationFragment.Defaults.FrameMultiplier;
        }

        _decodeFragment.BuildSave(builder, registry, new LtxVhsDecodeFragment.Parameters
        {
            FrameRate = finalFrameRate,
            FilenamePrefix = "tmp/video"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private void AddFrameGuide(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string nodeId,
        string title,
        string imageInputName,
        int frameIndex,
        double strength,
        string positiveInputName = "ltx_positive_output",
        string negativeInputName = "ltx_negative_output")
    {
        _addGuideFragment.Build(builder, registry, new LtxAddGuideFragment.Parameters
        {
            NodeId = nodeId,
            Title = title,
            ImageInputName = imageInputName,
            FrameIndex = frameIndex,
            Strength = strength,
            PositiveInputName = positiveInputName,
            NegativeInputName = negativeInputName,
            PositiveOutputName = positiveInputName,
            NegativeOutputName = negativeInputName,
            LatentOutputName = "video_latent"
        });
    }

    private static void BuildBasicScheduler(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string nodeId,
        string title,
        string scheduler,
        int steps,
        double denoise,
        string outputName)
    {
        builder.AddNode(nodeId, node => node
            .Type("BasicScheduler")
            .Title(title)
            .InputRef("model", registry.GetRef("model_output"))
            .Input("scheduler", scheduler)
            .Input("steps", steps)
            .Input("denoise", denoise));

        registry.Register(outputName, nodeId, 0);
    }

    private static bool TryGetSourcePath(GenerationParameters parameters, string key, out string path)
    {
        path = "";
        if (parameters.Sources == null || !parameters.Sources.TryGetValue(key, out var source) || source == null)
        {
            return false;
        }

        path = source.Filename ?? source.FilePath ?? "";
        return !string.IsNullOrWhiteSpace(path);
    }

    private static double ResolveGuideStrength(BlazorWebApp.Models.FragmentParameters? guideSettings, string key, string legacyKey, double defaultValue)
    {
        if (guideSettings?.Values.ContainsKey(key) == true)
        {
            return guideSettings.GetDouble(key, defaultValue);
        }

        return guideSettings?.GetDouble(legacyKey, defaultValue) ?? defaultValue;
    }

    private static long ResolveSeed(long seed)
    {
        return seed < 0 ? Random.Shared.NextInt64(0, int.MaxValue) : seed;
    }
}