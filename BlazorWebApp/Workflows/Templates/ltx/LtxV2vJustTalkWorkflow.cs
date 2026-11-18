using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using VideoSourceOptions = BlazorWebApp.Models.VideoSourceOptions;

namespace BlazorWebApp.Workflows.Templates.Ltx;

/// <summary>
/// LTX 2.3 distilled V2V Just-Talk (custom audio lip-sync to any video).
/// Mirrors the upstream
/// <c>LTX-2.3 - V2V_Just_Talk_custom_audio_lip-synced_to_any_video.json</c>.
///
/// Pipeline:
///   AV split-loader -&gt; [optional NAG / Sage] -&gt; LoRAs
///   -&gt; load + resize source video
///   -&gt; VAEEncode video latent + last-frame latent
///   -&gt; load + trim audio -&gt; [optional Mel separation]
///       -&gt; <c>LTXVAudioVAEEncode</c> (registers <c>audio_latent</c>)
///   -&gt; prompts -&gt; <c>LTXVConditioning</c>
///   -&gt; <c>LTXVAddLatentGuide</c> (last frame, strength 0.7)
///   -&gt; <c>LtxFaceMaskFragment</c> (FaceSegment / BlockifyMask /
///       LTXVPreprocessMasks / LTXVSetVideoLatentNoiseMasks)
///   -&gt; <c>LTXVAudioVideoMask</c> (time-range AV mask)
///   -&gt; <c>LTXVConcatAVLatent</c>
///   -&gt; scheduler -&gt; sampling pass 1
///   -&gt; [optional refinement pass]
///   -&gt; decode.
///
/// Sources: <c>source_video</c> (Video, required),
/// <c>audio_track</c> (Audio, required).
/// </summary>
public class LtxV2vJustTalkWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadSplitAvFragment _loadFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxNagEnhancementFragment _nagFragment = new();
    private readonly LtxSageAttentionEnhancementFragment _sageFragment = new();
    private readonly LtxRefinementPassEnhancementFragment _refinementFragment = new();
    private readonly LtxThirdPassEnhancementFragment _thirdPassFragment = new();
    private readonly LtxMelSeparationEnhancementFragment _melFragment = new();
    private readonly LtxLoadVideoFragment _loadVideoFragment = new();
    private readonly LtxVaeEncodeVideoFragment _vaeEncodeVideoFragment = new();
    private readonly LtxLoadAudioFragment _loadAudioFragment = new();
    private readonly LtxAudioVaeEncodeFragment _audioVaeEncodeFragment = new();
    private readonly LtxAddLatentGuideFragment _addLatentGuideFragment = new();
    private readonly LtxFaceMaskFragment _faceMaskFragment = new();
    private readonly LtxAudioVideoMaskFragment _audioVideoMaskFragment = new();
    private readonly LtxConcatAVLatentFragment _concatAVFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LtxConditioningFragment _conditioningFragment = new();
    private readonly LtxSchedulerFragment _schedulerFragment = new();
    private readonly LtxSamplingPassFragment _samplingPassFragment = new();
    private readonly LtxUpsampleLatentFragment _upsampleLatentFragment = new();
    private readonly LtxDecodeFragment _decodeFragment = new();
    private readonly LtxVideoSettingsFragment _videoSettingsFragment = new();
    private readonly LtxSamplerFragment _samplerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "LTX 2.3 V2V Just Talk",
        Description = "Drives lip-sync on a source video using a separately uploaded voice track. " +
                      "The video is conditioned in-place while the audio replaces the original speech, " +
                      "keeping body and camera motion intact. Vocal isolation is recommended for clean dialogue.",
        Base = Data.Enums.ModelBase.LTX,
        Mode = ModeType.Vid2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "UNet",
                Label = "Diffusion Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "ltx-2.3-22b-distilled-bf16.safetensors",
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "Text Encoder (Gemma)",
                Type = AssetType.Clip,
                DefaultValue = "gemma_3_12B_it_fpmixed.safetensors",
                Order = 2,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                // LTXAVTextEncoderLoader.ckpt_name reads /models/checkpoints,
                // verified via /object_info — distinct from the diffusion UNet.
                Parameter = "AvProjectionCkpt",
                Label = "AV Text Projection Ckpt",
                Type = AssetType.CheckpointModel,
                DefaultValue = "ltx-2.3_text_projection_bf16.safetensors",
                Order = 3,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "Video VAE",
                Type = AssetType.Vae,
                DefaultValue = "ltx-2.3_video_vae.safetensors",
                Order = 4,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "AudioVae",
                Label = "Audio VAE",
                // VAELoaderKJ (default audio VAE loader) reads /models/vae.
                Type = AssetType.Vae,
                DefaultValue = "LTX23_audio_vae_bf16_KJ.safetensors",
                Order = 5,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "UpscaleModel",
                Label = "Spatial Upscaler",
                Type = AssetType.LatentUpscaleModel,
                DefaultValue = "ltx-2-spatial-upscaler-x2-1.0.safetensors",
                Order = 6,
                ColumnSize = 12
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "source_video",
                Label = "Source Video",
                Type = SourceType.Video,
                Required = true,
                Parameter = "source_video",
                DefaultVideoOptions = new VideoSourceOptions
                {
                    ForceRate = 0,
                    FrameLoadCap = 0,
                    SkipFirstFrames = 0,
                    SelectEveryNth = 1,
                    Format = "LTXV"
                }
            },
            new WorkflowSource
            {
                Id = "audio_track",
                Label = "Audio Track",
                Type = SourceType.Audio,
                Required = true,
                Parameter = "audio"
            }
        ],
        CompatibleResourceBaseModels = ["LTXV2", "LTXV 2.3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _videoSettingsFragment;
        yield return _samplerFragment;
        yield return _schedulerFragment;
        yield return _nagFragment;
        yield return _sageFragment;
        yield return _melFragment;
        yield return _refinementFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // --- read user-facing settings ---
        var videoSettings = parameters.GetFragment(_videoSettingsFragment.Metadata.Id);
        var duration = videoSettings?.GetInt("duration", 5) ?? 5;
        var frameRate = videoSettings?.GetInt("frame_rate", 24) ?? 24;

        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var seed = samplerSettings?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var cfg = samplerSettings?.GetDouble("cfg", 1.0) ?? 1.0;
        var samplerName = samplerSettings?.GetString("sampler_name", "euler_ancestral_cfg_pp") ?? "euler_ancestral_cfg_pp";

        var refinementFrag = parameters.GetFragment(_refinementFragment.Metadata.Id);
        var refinementActive = refinementFrag?.IsActive == true;
        var nagActive = parameters.GetFragment(_nagFragment.Metadata.Id)?.IsActive == true;
        var sageActive = parameters.GetFragment(_sageFragment.Metadata.Id)?.IsActive == true;
        var melActive = parameters.GetFragment(_melFragment.Metadata.Id)?.IsActive == true;

        // --- 1. loader (AV variant) ---
        _loadFragment.Build(builder, parameters, registry);

        // --- 2. enhancement model patches ---
        if (nagActive) _nagFragment.BuildPatch(builder, registry, parameters);
        if (sageActive) _sageFragment.BuildPatch(builder, registry);

        // --- 3. LoRAs ---
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // --- 4. load source video ---
        _loadVideoFragment.Build(builder, parameters, registry);

        // --- 5. encode source video to latent (registers video_latent) ---
        _vaeEncodeVideoFragment.Build(builder, registry, new LtxVaeEncodeVideoFragment.Parameters());

        // --- 5b. encode last frame separately for the latent guide ---
        _vaeEncodeVideoFragment.Build(builder, registry, new LtxVaeEncodeVideoFragment.Parameters
        {
            ImagesInputName = "loaded_video_last_frame",
            OutputName = "loaded_video_last_frame_latent"
        }, scope: "last_");

        // --- 6. audio path ---
        _loadAudioFragment.Build(builder, parameters, registry);
        if (melActive) _melFragment.BuildPatch(builder, registry);
        _audioVaeEncodeFragment.Build(builder, registry, new LtxAudioVaeEncodeFragment.Parameters());

        // --- 7. prompts -> conditioning ---
        var promptsData = parameters.GetFragment("prompts");
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsData?.GetString("positive", "") ?? "",
            Negative = promptsData?.GetString("negative", "") ?? ""
        });
        _conditioningFragment.Build(builder, registry, new LtxConditioningFragment.Parameters
        {
            FrameRate = frameRate
        });

        // --- 8. inject last-frame latent guide (re-registers conditioning + video_latent) ---
        _addLatentGuideFragment.Build(builder, registry, new LtxAddLatentGuideFragment.Parameters
        {
            FrameIdx = -1,
            Strength = 0.7,
            GuidingLatentInputName = "last_loaded_video_last_frame_latent"
        });

        // --- 9. face mask -> LTXVSetVideoLatentNoiseMasks (overrides video_latent) ---
        _faceMaskFragment.Build(builder, registry, new LtxFaceMaskFragment.Parameters());

        // --- 10. AV mask (overrides video_latent + audio_latent with time-range mask) ---
        _audioVideoMaskFragment.Build(builder, registry, new LtxAudioVideoMaskFragment.Parameters
        {
            VideoFps = frameRate,
            VideoStartTime = 0.0,
            VideoEndTime = duration,
            AudioStartTime = 0.0,
            AudioEndTime = duration
        });

        // --- 11. concat AV latent for the sampler ---
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

        // --- 12. scheduler ---
        _schedulerFragment.Build(builder, parameters, registry);

        // --- 13. sampling pass 1 ---
        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "pass1",
            Seed = seed,
            Cfg = cfg,
            SamplerName = samplerName,
            UseRegistrySigmas = true,
            PositiveInputName = "ltx_positive_output",
            NegativeInputName = "ltx_negative_output",
            Title = "SamplerCustomAdvanced (Pass 1)"
        });

        if (refinementActive)
        {
            _upsampleLatentFragment.Build(builder, registry, new LtxUpsampleLatentFragment.Parameters());
            _conditioningFragment.BuildCropped(builder, registry, new LtxConditioningFragment.CropParameters());

            _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
            {
                NodeId = "ltx_concat_av_pass2",
                Title = "LTXVConcatAVLatent (Pass 2)"
            });
            // Also register under the pass2-scoped key so the scoped scheduler can find it.
            var pass2AvRef = registry.GetRef("av_latent_output");
            registry.Register("pass2_av_latent_output", pass2AvRef.nodeId, pass2AvRef.outputIndex);

            var refSigmasMode = refinementFrag?.GetString("sigmas_mode", "auto") ?? "auto";
            var refManualSigmas = refinementFrag?.GetString("manual_sigmas", "0.85, 0.7250, 0.4219, 0.0")
                ?? "0.85, 0.7250, 0.4219, 0.0";

            if (string.Equals(refSigmasMode, "auto", StringComparison.OrdinalIgnoreCase))
            {
                var schedFrag = parameters.GetFragment(_schedulerFragment.Metadata.Id);
                _schedulerFragment.Build(builder, registry, new LtxSchedulerFragment.Parameters
                {
                    Mode = "ltxv",
                    Steps = schedFrag?.GetInt("steps", 8) ?? 8,
                    MaxShift = schedFrag?.GetDouble("max_shift", 2.05) ?? 2.05,
                    BaseShift = schedFrag?.GetDouble("base_shift", 0.95) ?? 0.95,
                    Stretch = schedFrag?.GetBool("stretch", true) ?? true,
                    Terminal = schedFrag?.GetDouble("terminal", 0.1) ?? 0.1,
                    LatentInputName = "av_latent_output"
                }, scope: "pass2_");
            }
            else
            {
                _schedulerFragment.Build(builder, registry, new LtxSchedulerFragment.Parameters
                {
                    Mode = "manual",
                    ManualSigmas = refManualSigmas
                }, scope: "pass2_");
            }

            var refSampler = refinementFrag?.GetString("sampler_name", "euler_cfg_pp") ?? "euler_cfg_pp";
            var refCfg = refinementFrag?.GetDouble("cfg", 1.0) ?? 1.0;
            var refSeed = refinementFrag?.GetLong("seed", 42L) ?? 42L;
            _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
            {
                PassId = "pass2",
                Seed = refSeed,
                Cfg = refCfg,
                SamplerName = refSampler,
                UseRegistrySigmas = true,
                SigmasInputName = "pass2_sigmas_output",
                PositiveInputName = "ltx_cropped_positive_output",
                NegativeInputName = "ltx_cropped_negative_output",
                Title = "SamplerCustomAdvanced (Pass 2)"
            });
        }

        _decodeFragment.Build(builder, registry, new LtxDecodeFragment.Parameters
        {
            FrameRate = frameRate
        });

        return builder.ToComfyWorkflow(registry);
    }
}
