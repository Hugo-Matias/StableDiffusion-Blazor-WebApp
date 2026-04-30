using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Ltx;

/// <summary>
/// LTX 2.3 Talking Avatar workflow (OmniVoice TTS lip-sync from a still
/// avatar image). Mirrors the upstream
/// <c>LTX-2.3_-_I2V_T2V_Talking_Avatar_(voice_clone_with_OmniVoice-TTS).json</c>.
///
/// Pipeline:
///   AV split-loader -&gt; [optional NAG / Sage] -&gt; LoRAs
///   -&gt; load + preprocess avatar image
///   -&gt; prompts -&gt; <c>LTXVConditioning</c>
///   -&gt; empty video latent (audio latent skipped - real audio comes from
///       OmniVoice)
///   -&gt; <c>LTXVImgToVideoInplace</c> (re-registers conditioning + video
///       latent)
///   -&gt; <c>LtxOmniVoiceFragment</c> (LoadAudio reference + OmniVoice
///       Whisper + VoiceCloneTTS + Trim) -&gt; registers
///       <c>{scope}audio_input</c>
///   -&gt; [optional Mel separation patch]
///   -&gt; <c>LtxAudioVaeEncodeFragment</c> -&gt; registers
///       <c>{scope}audio_latent</c>
///   -&gt; <c>LTXVConcatAVLatent</c> -&gt; scheduler -&gt; sampler pass 1
///   -&gt; [optional refinement pass]
///   -&gt; decode.
///
/// Sources: <c>source_image</c> (Image, required),
/// <c>reference_audio</c> (Audio, required - the speaker voice sample
/// the cloned voice is derived from).
///
/// TTS text is read from the standard <c>prompts.positive</c> field for
/// v1; a dedicated TTS-text fragment is documented under Phase 7.
/// </summary>
public class LtxTalkingAvatarWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadSplitAvFragment _loadFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxNagEnhancementFragment _nagFragment = new();
    private readonly LtxSageAttentionEnhancementFragment _sageFragment = new();
    private readonly LtxRefinementPassEnhancementFragment _refinementFragment = new();
    private readonly LtxMelSeparationEnhancementFragment _melFragment = new();
    private readonly LtxLoadImageFragment _loadImageFragment = new();
    private readonly LtxOmniVoiceFragment _omniVoiceFragment = new();
    private readonly LtxAudioVaeEncodeFragment _audioVaeEncodeFragment = new();
    private readonly LtxEmptyLatentFragment _emptyLatentFragment = new();
    private readonly LtxImgToVideoFragment _imgToVideoFragment = new();
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
        Title = "LTX 2.3 Talking Avatar (OmniVoice)",
        Description = "Animates a portrait into a talking avatar using OmniVoice TTS driven by the prompt. " +
                      "Provide a clean front-facing portrait; the model synthesizes voice and lip-sync from the text alone, " +
                      "with optional reference audio for voice cloning.",
        Base = Data.Enums.ModelBase.LTX,
        Mode = ModeType.Img2Vid,
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
                Id = "source_image",
                Label = "Avatar Image",
                Type = SourceType.Image,
                Required = true,
                Parameter = "source_image"
            },
            new WorkflowSource
            {
                Id = "reference_audio",
                Label = "Reference Voice Sample",
                Type = SourceType.Audio,
                Required = true,
                Parameter = "reference_audio"
            }
        ],
        CompatibleResourceBaseModels = ["LTXV2", "LTXV 2.3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _loadImageFragment;
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
        var imgCompression = videoSettings?.GetInt("img_compression", 18) ?? 18;
        var i2vStrength = videoSettings?.GetDouble("i2v_strength", 0.7) ?? 0.7;

        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var seed = samplerSettings?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var cfg = samplerSettings?.GetDouble("cfg", 1.0) ?? 1.0;
        var samplerName = samplerSettings?.GetString("sampler_name", "euler_ancestral_cfg_pp") ?? "euler_ancestral_cfg_pp";

        var imageFragment = parameters.GetFragment(_loadImageFragment.Metadata.Id);
        var width = imageFragment?.GetInt("width", 1280) ?? 1280;
        var height = imageFragment?.GetInt("height", 720) ?? 720;
        var frameCount = duration * frameRate + 1;

        var refinementFrag = parameters.GetFragment(_refinementFragment.Metadata.Id);
        var refinementActive = refinementFrag?.IsActive == true;
        var nagActive = parameters.GetFragment(_nagFragment.Metadata.Id)?.IsActive == true;
        var sageActive = parameters.GetFragment(_sageFragment.Metadata.Id)?.IsActive == true;
        var melActive = parameters.GetFragment(_melFragment.Metadata.Id)?.IsActive == true;

        // --- 1. AV split loader ---
        _loadFragment.Build(builder, parameters, registry);

        // --- 2. enhancement model patches ---
        if (nagActive) _nagFragment.BuildPatch(builder, registry);
        if (sageActive) _sageFragment.BuildPatch(builder, registry);

        // --- 3. LoRAs ---
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // --- 4. Load avatar image ---
        var imageSource = parameters.Sources?.GetValueOrDefault("source_image");
        var imagePath = imageSource?.Filename ?? imageSource?.FilePath ?? "";
        _loadImageFragment.Build(builder, registry, new LtxLoadImageFragment.Parameters
        {
            ImagePath = imagePath,
            Width = width,
            Height = height,
            ImgCompression = imgCompression
        });

        // --- 5. prompts -> conditioning ---
        var promptsData = parameters.GetFragment("prompts");
        var ttsText = promptsData?.GetString("positive", "") ?? "";
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = ttsText,
            Negative = promptsData?.GetString("negative", "") ?? ""
        });
        _conditioningFragment.Build(builder, registry, new LtxConditioningFragment.Parameters
        {
            FrameRate = frameRate
        });

        // --- 6. empty video latent (skip empty audio - OmniVoice supplies real audio) ---
        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = width,
            Height = height,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = frameRate,
            SkipAudio = true
        });

        // --- 7. I2V Inplace (re-registers conditioning + video_latent) ---
        _imgToVideoFragment.Build(builder, registry, new LtxImgToVideoFragment.Parameters
        {
            NodeId = "ltx_i2v_pass1",
            Strength = i2vStrength,
            Bypass = false,
            Title = "LTXVImgToVideoInplace (Pass 1)"
        });

        // --- 8. OmniVoice TTS (registers audio_input) ---
        _omniVoiceFragment.Build(builder, parameters, registry);

        // --- 9. optional mel separation patch on the audio VAE ---
        if (melActive) _melFragment.BuildPatch(builder, registry);

        // --- 10. encode cloned audio to latent (registers audio_latent) ---
        _audioVaeEncodeFragment.Build(builder, registry, new LtxAudioVaeEncodeFragment.Parameters());

        // --- 11. concat AV latent ---
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

        // --- 14. optional refinement pass ---
        if (refinementActive)
        {
            _upsampleLatentFragment.Build(builder, registry, new LtxUpsampleLatentFragment.Parameters());
            _conditioningFragment.BuildCropped(builder, registry, new LtxConditioningFragment.CropParameters());

            _imgToVideoFragment.Build(builder, registry, new LtxImgToVideoFragment.Parameters
            {
                NodeId = "ltx_i2v_pass2",
                Strength = 1.0,
                Bypass = false,
                Title = "LTXVImgToVideoInplace (Pass 2)"
            });

            _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
            {
                NodeId = "ltx_concat_av_pass2",
                Title = "LTXVConcatAVLatent (Pass 2)"
            });

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

        // --- 15. decode + save ---
        _decodeFragment.Build(builder, registry, new LtxDecodeFragment.Parameters
        {
            FrameRate = frameRate
        });

        return builder.ToComfyWorkflow(registry);
    }
}
