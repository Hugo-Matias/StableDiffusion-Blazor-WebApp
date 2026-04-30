using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;
using FragmentParameters = BlazorWebApp.Models.FragmentParameters;

namespace BlazorWebApp.Workflows.Templates.Ltx;

/// <summary>
/// LTX 2.3 distilled Txt2Vid driven by a user-supplied audio track (custom audio).
/// Mirrors the upstream <c>LTX-2.3 - I2V_T2V_Basic_Custom_Audio.json</c>.
///
/// Pipeline (relative to <see cref="LtxTxt2VidWorkflow"/>):
///   loader -&gt; [optional NAG / Sage patches] -&gt; LoRA
///   -&gt; empty video latent (no empty audio latent emitted)
///   -&gt; LtxLoadAudioFragment -&gt; [optional Mel separation patch]
///   -&gt; LtxAudioVaeEncodeFragment (registers <c>audio_latent</c>)
///   -&gt; concat AV -&gt; prompts -&gt; conditioning -&gt; scheduler
///   -&gt; sampling pass 1 -&gt; [optional refinement pass]
///   -&gt; decode.
///
/// The user supplies the audio file via the <c>audio_track</c> source slot. The video
/// length comes from the standard <c>ltx_video_settings</c> fragment and the audio is
/// trimmed to match.
/// </summary>
public class LtxTxt2VidCustomAudioWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadSplitFragment _loadFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxNagEnhancementFragment _nagFragment = new();
    private readonly LtxSageAttentionEnhancementFragment _sageFragment = new();
    private readonly LtxRefinementPassEnhancementFragment _refinementFragment = new();
    private readonly LtxThirdPassEnhancementFragment _thirdPassFragment = new();
    private readonly LtxMelSeparationEnhancementFragment _melFragment = new();
    private readonly LtxEmptyLatentFragment _emptyLatentFragment = new();
    private readonly LtxLoadAudioFragment _loadAudioFragment = new();
    private readonly LtxAudioVaeEncodeFragment _audioVaeEncodeFragment = new();
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
        Title = "LTX 2.3 T2V Custom Audio",
        Description = "Text-to-video paired with a user-supplied audio track that drives motion timing and pacing. " +
                      "Useful for music-video shorts or scenes where the soundtrack should dictate beat and rhythm " +
                      "while the prompt describes the scene.",
        Base = Data.Enums.ModelBase.LTX,
        Mode = ModeType.Txt2Vid,
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
                Parameter = "Clip2",
                Label = "Text Encoder (LTX projection)",
                Type = AssetType.Clip,
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
        yield return _thirdPassFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // --- read user-facing settings ---
        var videoSettings = parameters.GetFragment(_videoSettingsFragment.Metadata.Id);
        var width = videoSettings?.GetInt("width", 1280) ?? 1280;
        var height = videoSettings?.GetInt("height", 720) ?? 720;
        var duration = videoSettings?.GetInt("duration", 5) ?? 5;
        var frameRate = videoSettings?.GetInt("frame_rate", 25) ?? 25;
        var frameCount = duration * frameRate + 1;

        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var seed = samplerSettings?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var cfg = samplerSettings?.GetDouble("cfg", 1.0) ?? 1.0;

        var refinementFrag = parameters.GetFragment(_refinementFragment.Metadata.Id);
        var refinementActive = refinementFrag?.IsActive == true;
        var thirdPassFrag = parameters.GetFragment(_thirdPassFragment.Metadata.Id);
        var thirdPassActive = thirdPassFrag?.IsActive == true;
        if (thirdPassActive && !refinementActive) refinementActive = true;
        var nagActive = parameters.GetFragment(_nagFragment.Metadata.Id)?.IsActive == true;
        var sageActive = parameters.GetFragment(_sageFragment.Metadata.Id)?.IsActive == true;
        var melActive = parameters.GetFragment(_melFragment.Metadata.Id)?.IsActive == true;

        var pass1Width = refinementActive ? width / 2 : width;
        var pass1Height = refinementActive ? height / 2 : height;

        // --- 1. loader ---
        _loadFragment.Build(builder, parameters, registry);

        // --- 2. enhancement model patches ---
        if (nagActive) _nagFragment.BuildPatch(builder, registry);
        if (sageActive) _sageFragment.BuildPatch(builder, registry);

        // --- 3. LoRAs ---
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // --- 4. empty video latent (no empty audio latent; we encode a real one below) ---
        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = pass1Width,
            Height = pass1Height,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = frameRate,
            SkipAudio = true
        });

        // --- 5. audio path: load + trim -> [Mel separation] -> VAE encode + mask ---
        _loadAudioFragment.Build(builder, parameters, registry);
        if (melActive) _melFragment.BuildPatch(builder, registry);
        _audioVaeEncodeFragment.Build(builder, registry, new LtxAudioVaeEncodeFragment.Parameters());

        // --- 6. concat AV pass 1 ---
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

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

        // --- 8. scheduler ---
        _schedulerFragment.Build(builder, parameters, registry);

        // --- 9. sampling pass 1 ---
        var samplerName = samplerSettings?.GetString("sampler_name", "euler_ancestral_cfg_pp") ?? "euler_ancestral_cfg_pp";
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

            if (thirdPassActive)
            {
                BuildThirdPass(builder, registry, parameters, thirdPassFrag);
            }
        }

        _decodeFragment.Build(builder, registry, new LtxDecodeFragment.Parameters
        {
            FrameRate = frameRate
        });

        return builder.ToComfyWorkflow(registry);
    }

    private void BuildThirdPass(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        FragmentParameters? thirdPassFrag)
    {
        var sigmasMode = thirdPassFrag?.GetString("sigmas_mode", "auto") ?? "auto";
        var manualSigmas = thirdPassFrag?.GetString("manual_sigmas", "0.85, 0.7250, 0.4219, 0.0")
            ?? "0.85, 0.7250, 0.4219, 0.0";

        if (string.Equals(sigmasMode, "auto", StringComparison.OrdinalIgnoreCase))
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
            }, scope: "pass3_");
        }
        else
        {
            _schedulerFragment.Build(builder, registry, new LtxSchedulerFragment.Parameters
            {
                Mode = "manual",
                ManualSigmas = manualSigmas
            }, scope: "pass3_");
        }

        var sampler = thirdPassFrag?.GetString("sampler_name", "euler_cfg_pp") ?? "euler_cfg_pp";
        var cfg = thirdPassFrag?.GetDouble("cfg", 1.0) ?? 1.0;
        var seed = thirdPassFrag?.GetLong("seed", 43L) ?? 43L;

        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "pass3",
            Seed = seed,
            Cfg = cfg,
            SamplerName = sampler,
            UseRegistrySigmas = true,
            SigmasInputName = "pass3_sigmas_output",
            PositiveInputName = "ltx_cropped_positive_output",
            NegativeInputName = "ltx_cropped_negative_output",
            Title = "SamplerCustomAdvanced (Pass 3)"
        });
    }
}
