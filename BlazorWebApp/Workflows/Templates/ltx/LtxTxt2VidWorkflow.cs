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
/// LTX 2.3 distilled Txt2Vid workflow modeled on the upstream
/// <c>LTX-2.3 - I2V_T2V_Basic.json</c> (T2V branch). Uses the Kijai split-safetensors
/// loader cluster (UNETLoader + DualCLIPLoader + VAELoader + AudioVAE + UpscaleModel)
/// instead of the Comfy-default checkpoint. Pass count, NAG, and SageAttention
/// are exposed as collapsible enhancement fragments.
///
/// Fragment topology:
///   ltx_load_split (loader) -&gt; [optional NAG / Sage patches]
///   -&gt; LoRA -&gt; empty latent -&gt; prompts -&gt; ltx_conditioning
///   -&gt; ltx_scheduler (registers sigmas_output)
///   -&gt; ltx_sampling_pass (pass 1, registry sigmas)
///   -&gt; [if refinement: upsample -&gt; crop guides -&gt; pass-2 sigmas -&gt; pass 2]
///   -&gt; ltx_decode.
///
/// I2V remains served by <see cref="LtxImg2VidWorkflow"/>.
/// </summary>
public class LtxTxt2VidWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadSplitFragment _loadFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxNagEnhancementFragment _nagFragment = new();
    private readonly LtxSageAttentionEnhancementFragment _sageFragment = new();
    private readonly LtxRefinementPassEnhancementFragment _refinementFragment = new();
    private readonly LtxThirdPassEnhancementFragment _thirdPassFragment = new();
    private readonly LtxEmptyLatentFragment _emptyLatentFragment = new();
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
        Title = "LTX 2.3 T2V",
        Description = "Pure text-to-video generation with LTX 2.3. The prompt is the only conditioning input " +
                      "and width / height / duration come from the Video Settings panel. " +
                      "Best starting point for free-form clips when you don't have reference imagery.",
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
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = "gemma_3_12B_it_fp4_mixed.safetensors",
                Order = 2,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "Video VAE",
                Type = AssetType.Vae,
                DefaultValue = "ltx-2.3_video_vae.safetensors",
                Order = 3,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "AudioVae",
                Label = "Audio VAE",
                Type = AssetType.AudioVae,
                DefaultValue = "ltx-2.3_audio_vae.safetensors",
                Order = 4,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "UpscaleModel",
                Label = "Spatial Upscaler",
                Type = AssetType.UpscaleModel,
                DefaultValue = "ltx-2.3-spatial-upscaler-x2-1.1.safetensors",
                Order = 5,
                ColumnSize = 12
            }
        ],
        Sources = [],
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
        // Third pass needs the cropped conditioning + full-res latent the refinement
        // pass produces. Auto-enable refinement when third-pass is on.
        if (thirdPassActive && !refinementActive) refinementActive = true;
        var nagActive = parameters.GetFragment(_nagFragment.Metadata.Id)?.IsActive == true;
        var sageActive = parameters.GetFragment(_sageFragment.Metadata.Id)?.IsActive == true;

        // Refinement halves resolution for pass 1, upsamples 2x to full res before pass 2.
        var pass1Width = refinementActive ? width / 2 : width;
        var pass1Height = refinementActive ? height / 2 : height;

        // --- 1. loader (UNet + CLIP + VAE + AudioVAE + UpscaleModel + always-on patches) ---
        _loadFragment.Build(builder, parameters, registry);

        // --- 2. enhancement model patches (chained on model_output) ---
        if (nagActive)
        {
            _nagFragment.BuildPatch(builder, registry);
        }
        if (sageActive)
        {
            _sageFragment.BuildPatch(builder, registry);
        }

        // --- 3. LoRAs (patches model_output and clip_output) ---
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // --- 4. empty latents (video + audio) and concat into av_latent_output ---
        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = pass1Width,
            Height = pass1Height,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = frameRate
        });
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

        // --- 5. prompts -> conditioning ---
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

        // --- 6. scheduler (registers sigmas_output keyed off av_latent_output) ---
        _schedulerFragment.Build(builder, parameters, registry);

        // --- 7. sampling pass 1 (consumes registry sigmas) ---
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
            // 8. upsample latent (separates AV, upsamples video 2x, re-registers video_latent + audio_latent)
            _upsampleLatentFragment.Build(builder, registry, new LtxUpsampleLatentFragment.Parameters());

            // 9. crop guides
            _conditioningFragment.BuildCropped(builder, registry, new LtxConditioningFragment.CropParameters());

            // 10. concat upscaled video + audio into pass2 av_latent_output
            _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
            {
                NodeId = "ltx_concat_av_pass2",
                Title = "LTXVConcatAVLatent (Pass 2)"
            });

            // 11. pass-2 sigmas (auto: rebuild LTXVScheduler at full-res; manual: ManualSigmas curve)
            var refSigmasMode = refinementFrag?.GetString("sigmas_mode", "auto") ?? "auto";
            var refManualSigmas = refinementFrag?.GetString("manual_sigmas", "0.85, 0.7250, 0.4219, 0.0")
                ?? "0.85, 0.7250, 0.4219, 0.0";

            if (string.Equals(refSigmasMode, "auto", StringComparison.OrdinalIgnoreCase))
            {
                // Rebuild scheduler scoped to "pass2_" reading the new av_latent_output ref.
                // Schedule fragment looks up "{scope}{LatentInputName}" so we pass an empty scope
                // and aim it at the (already-final) av_latent_output by constructing an explicit node.
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

            // 12. pass-2 sampling
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

        // --- decode + save ---
        _decodeFragment.Build(builder, registry, new LtxDecodeFragment.Parameters
        {
            FrameRate = frameRate
        });

        return builder.ToComfyWorkflow(registry);
    }

    /// <summary>
    /// Emits the third-pass scheduler + sampler. Reads the pass-2
    /// <c>av_latent_output</c> and the cropped conditioning produced by the
    /// refinement pass; registers <c>av_latent_output</c> (pass 3 final).
    /// </summary>
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
