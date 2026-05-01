using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Ltx;

/// <summary>
/// LTX 2.3 Control V2V — body-movement transfer via the Lightricks Union
/// IC-LoRA driven by a DWPose preprocessor. Mirrors the upstream
/// <c>LTX-2.3 - IV2V_TV2V_transfer_body_movements_IC-Union-Control-lora_DWPose.json</c>.
///
/// Pipeline:
///   split loader -&gt; [optional NAG / Sage] -&gt; LoRAs
///   -&gt; <c>LTXICLoRALoaderModelOnly</c> (registers latent_downscale_factor)
///   -&gt; load motion-reference video -&gt; <c>DWPreprocessor</c>
///       -&gt; <c>ImageBlend</c> (multiply, 0.5)
///   -&gt; empty AV latents (sized off user video settings)
///   -&gt; prompts -&gt; <c>LTXVConditioning</c>
///   -&gt; <c>LTXAddVideoICLoRAGuide</c>
///       (re-binds positive / negative / video_latent)
///   -&gt; <c>LTXVConcatAVLatent</c>
///   -&gt; scheduler -&gt; sampling pass 1
///   -&gt; [optional refinement pass]
///   -&gt; decode.
///
/// Sources: <c>motion_video</c> (Video, required).
/// </summary>
public class LtxControlVid2VidWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadSplitFragment _loadFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxIcLoraLoaderFragment _icLoraLoaderFragment = new();
    private readonly LtxNagEnhancementFragment _nagFragment = new();
    private readonly LtxSageAttentionEnhancementFragment _sageFragment = new();
    private readonly LtxRefinementPassEnhancementFragment _refinementFragment = new();
    private readonly LtxLoadVideoFragment _loadVideoFragment = new();
    private readonly LtxControlPreprocessorFragment _controlPreprocessorFragment = new();
    private readonly LtxAddVideoIcLoraGuideFragment _addVideoIcLoraGuideFragment = new();
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
        Title = "LTX 2.3 Control V2V (DWPose)",
        Description = "Transfers body movements from a motion reference video onto a generated subject. " +
                      "DWPose extracts the pose sequence from your input video; the prompt drives subject and style.",
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
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "IcLora",
                Label = "IC-LoRA (Union Control)",
                Type = AssetType.Lora,
                DefaultValue = "ltx-2.3-22b-v1.1-ic-lora-union-control-ref0.5.safetensors",
                Order = 7,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "motion_video",
                Label = "Motion Reference Video",
                Type = SourceType.Video,
                Required = true,
                Parameter = "source_video"
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
        yield return _refinementFragment;
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
        var frameRate = videoSettings?.GetInt("frame_rate", 24) ?? 24;
        var frameCount = duration * frameRate + 1;

        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var seed = samplerSettings?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var cfg = samplerSettings?.GetDouble("cfg", 1.0) ?? 1.0;
        var samplerName = samplerSettings?.GetString("sampler_name", "euler_ancestral_cfg_pp") ?? "euler_ancestral_cfg_pp";

        var refinementFrag = parameters.GetFragment(_refinementFragment.Metadata.Id);
        var refinementActive = refinementFrag?.IsActive == true;
        var nagActive = parameters.GetFragment(_nagFragment.Metadata.Id)?.IsActive == true;
        var sageActive = parameters.GetFragment(_sageFragment.Metadata.Id)?.IsActive == true;

        // Refinement halves resolution for pass 1, upsamples 2x to full res for pass 2.
        var pass1Width = refinementActive ? width / 2 : width;
        var pass1Height = refinementActive ? height / 2 : height;

        // --- 1. loader ---
        _loadFragment.Build(builder, parameters, registry);

        // --- 2. enhancement model patches ---
        if (nagActive) _nagFragment.BuildPatch(builder, registry, parameters);
        if (sageActive) _sageFragment.BuildPatch(builder, registry);

        // --- 3. workflow LoRAs (patches model_output + clip_output) ---
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // --- 4. IC-LoRA loader (patches model_output, registers latent_downscale_factor) ---
        _icLoraLoaderFragment.Build(builder, parameters, registry);

        // --- 5. load motion-reference video ---
        _loadVideoFragment.Build(builder, parameters, registry);

        // --- 6. DWPose preprocessor + multiply blend (registers control_image) ---
        _controlPreprocessorFragment.Build(builder, registry, new LtxControlPreprocessorFragment.Parameters());

        // --- 7. empty AV latents sized off user settings ---
        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = pass1Width,
            Height = pass1Height,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = frameRate
        });

        // --- 8. prompts -> conditioning ---
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

        // --- 9. IC-LoRA video guide (re-registers conditioning + video_latent) ---
        _addVideoIcLoraGuideFragment.Build(builder, registry, new LtxAddVideoIcLoraGuideFragment.Parameters
        {
            FrameIdx = 0,
            Strength = 0.7
        });

        // --- 10. concat AV latent for the sampler ---
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

        // --- 11. scheduler ---
        _schedulerFragment.Build(builder, parameters, registry);

        // --- 12. sampling pass 1 ---
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
