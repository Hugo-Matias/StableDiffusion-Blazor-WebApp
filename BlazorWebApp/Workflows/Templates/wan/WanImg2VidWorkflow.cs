using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Wan;

/// <summary>
/// Wan Img2Vid workflow using dual-model (high/low noise) architecture.
/// Generates video from a source image using two separate UNet models with split sampling.
/// Pipeline: Load Models -> LoRA (optional) -> ModelSampling -> Image+CLIP -> PainterI2V -> Dual KSampler -> Decode -> Interpolation (optional) -> Save
/// </summary>
public class WanImg2VidWorkflow : IWorkflowBuilder
{
    private readonly LoadModelSageFragment _loadModelSageFragment = new();
    private readonly LoadClipVaeFragment _loadClipVaeFragment = new();
    private readonly LoraLoaderModelOnlyFragment _loraModelOnlyFragment = new();
    private readonly ModelSamplingSD3Fragment _modelSamplingSD3Fragment = new();
    private readonly WanLoadImageFragment _wanLoadImageFragment = new();
    private readonly ClipVisionEncodeFragment _clipVisionEncodeFragment = new();
    private readonly WanPromptsFragment _wanPromptsFragment = new();
    private readonly PainterI2VFragment _painterI2VFragment = new();
    private readonly SamplerAdvancedFragment _samplerAdvancedFragment = new();
    private readonly CleanVramFragment _cleanVramFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly FrameInterpolationFragment _frameInterpolationFragment = new();
    private readonly SaveVideoFragment _saveVideoFragment = new();
    private readonly PromptsFragment _promptsFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Img2Vid",
        Base = Data.Enums.ModelBase.Wan,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "HighModel",
                Label = "High Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "wan22RemixT2VI2V_i2vHighV20.safetensors",
                Order = 1,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "LowModel",
                Label = "Low Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "wan22RemixT2VI2V_i2vLowV20.safetensors",
                Order = 2,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "umt5_xxl_fp8_e4m3fn_scaled.safetensors",
                Order = 3,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "ClipVision",
                Label = "CLIP Vision",
                Type = AssetType.ClipVision,
                DefaultValue = "clip_vision_h.safetensors",
                Order = 4,
                ColumnSize = 2
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "wan_2.1_vae.safetensors",
                Order = 5,
                ColumnSize = 2
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "source_image",
                Label = "Source Image",
                Type = SourceType.Image,
                Required = true
            }
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _wanLoadImageFragment;
        yield return _painterI2VFragment;
        yield return _samplerAdvancedFragment;
        yield return _frameInterpolationFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var samplerFragment = parameters.GetFragment(_samplerAdvancedFragment.Metadata.Id);
        var painterFragment = parameters.GetFragment(_painterI2VFragment.Metadata.Id);

        var seed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var steps = samplerFragment?.GetInt("steps", 8) ?? 8;
        var cfg = samplerFragment?.GetDouble("cfg", 1.0) ?? 1.0;
        var samplerName = samplerFragment?.GetString("sampler_name", "euler") ?? "euler";
        var scheduler = samplerFragment?.GetString("scheduler", "simple") ?? "simple";
        var autoSplit = samplerFragment?.GetBool("auto_split", true) ?? true;
        var shift = painterFragment?.GetInt("shift", 5) ?? 5;

        // 1. Load High Model (UNETLoader -> SageAttention -> TorchSettings)
        _loadModelSageFragment.Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = parameters.Assets?.GetValueOrDefault("HighModel") ?? "wan22RemixT2VI2V_i2vHighV20.safetensors",
            LoaderTitle = "Load High Noise Model"
        }, scope: "high_", scopeTitle: "High ");

        // 2. Load Low Model (UNETLoader -> SageAttention -> TorchSettings)
        _loadModelSageFragment.Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = parameters.Assets?.GetValueOrDefault("LowModel") ?? "wan22RemixT2VI2V_i2vLowV20.safetensors",
            LoaderTitle = "Load Low Noise Model"
        }, scope: "low_", scopeTitle: "Low ");

        // 3. Load CLIP + VAE
        _loadClipVaeFragment.Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "umt5_xxl_fp8_e4m3fn_scaled.safetensors",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "wan_2.1_vae.safetensors"
        });

        // 4. LoRAs (optional, per model)
        var currentHighOutput = "high_model_output";
        var currentLowOutput = "low_model_output";

        if (parameters.Loras?.Count > 0)
        {
            for (int i = 0; i < parameters.Loras.Count; i++)
            {
                var lora = parameters.Loras[i];

                if (lora.HasHighPath)
                {
                    _loraModelOnlyFragment.Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
                    {
                        LoraLoaderId = $"high_lora_loader_{i}",
                        LoraName = lora.Name,
                        LoraPath = lora.HighPath,
                        LoraStrength = lora.Strength,
                        ModelInputName = currentHighOutput,
                        ModelOutputName = "high_lora_model_output",
                        Title = $"LoRA (High) {i}"
                    });
                    currentHighOutput = "high_lora_model_output";
                }

                if (lora.HasLowPath)
                {
                    _loraModelOnlyFragment.Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
                    {
                        LoraLoaderId = $"low_lora_loader_{i}",
                        LoraName = lora.Name,
                        LoraPath = lora.LowPath,
                        LoraStrength = lora.Strength,
                        ModelInputName = currentLowOutput,
                        ModelOutputName = "low_lora_model_output",
                        Title = $"LoRA (Low) {i}"
                    });
                    currentLowOutput = "low_lora_model_output";
                }
            }
        }

        // 5. ModelSamplingSD3 (High)
        _modelSamplingSD3Fragment.Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_model_sampling",
            Shift = shift,
            ModelInputName = currentHighOutput,
            ModelOutputName = "high_sampled_model_output",
            Title = "ModelSamplingSD3 (High)"
        });

        // 6. ModelSamplingSD3 (Low)
        _modelSamplingSD3Fragment.Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "low_model_sampling",
            Shift = shift,
            ModelInputName = currentLowOutput,
            ModelOutputName = "low_sampled_model_output",
            Title = "ModelSamplingSD3 (Low)"
        });

        // 7. Load Image + Resize
        _wanLoadImageFragment.Build(builder, parameters, registry);

        // 8. CLIP Vision Encode
        _clipVisionEncodeFragment.Build(builder, registry, new ClipVisionEncodeFragment.Parameters
        {
            ClipVisionName = parameters.Assets?.GetValueOrDefault("ClipVision") ?? "clip_vision_h.safetensors"
        });

        // 9. Prompts
        _wanPromptsFragment.Build(builder, parameters, registry);

        // 10. PainterI2V
        _painterI2VFragment.Build(builder, parameters, registry);

        // 11-12. Dual Sampling (High -> Low)
        int startHigh, endHigh, startLow, endLow;
        if (autoSplit)
        {
            var midpoint = (int)Math.Round((double)steps / 2);
            startHigh = 0;
            endHigh = midpoint;
            startLow = midpoint;
            endLow = 10000;
        }
        else
        {
            startHigh = samplerFragment?.GetInt("start_at_step_high", 0) ?? 0;
            endHigh = samplerFragment?.GetInt("end_at_step_high", steps / 2) ?? steps / 2;
            startLow = samplerFragment?.GetInt("start_at_step_low", steps / 2) ?? steps / 2;
            endLow = samplerFragment?.GetInt("end_at_step_low", 10000) ?? 10000;
        }

        // High sampler
        _samplerAdvancedFragment.Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            Seed = seed,
            Steps = steps,
            Cfg = cfg,
            SamplerName = samplerName,
            Scheduler = scheduler,
            StartAtStep = startHigh,
            EndAtStep = endHigh,
            AddNoise = "enable",
            ReturnWithLeftoverNoise = "enable",
            ModelInputName = "high_sampled_model_output",
            LatentOutputName = "latent_output",
            Title = "KSampler High Noise"
        });

        // Low sampler (uses high latent, seed=0, no noise)
        _samplerAdvancedFragment.Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_low",
            Seed = 0,
            Steps = steps,
            Cfg = cfg,
            SamplerName = samplerName,
            Scheduler = scheduler,
            StartAtStep = startLow,
            EndAtStep = endLow,
            AddNoise = "disable",
            ReturnWithLeftoverNoise = "disable",
            ModelInputName = "low_sampled_model_output",
            LatentInputName = "latent_output",
            LatentOutputName = "latent_output",
            Title = "KSampler Low Noise"
        });

        // 13. Clean VRAM (overwrites latent_output so VaeDecode picks it up)
        _cleanVramFragment.Build(builder, registry, new CleanVramFragment.Parameters
        {
            NodeId = "clean_sampler",
            InputName = "latent_output",
            OutputName = "latent_output",
            Title = "Clean VRAM (Sampler)"
        });

        // 14. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 15-16. Frame Interpolation (conditional) + Save Video
        var interpolationFragment = parameters.GetFragment(_frameInterpolationFragment.Metadata.Id);
        var isInterpolationActive = interpolationFragment?.IsActive == true;
        var baseFrameRate = painterFragment?.GetInt("frame_rate", 16) ?? 16;

        if (isInterpolationActive)
        {
            var multiplier = interpolationFragment?.GetInt("frame_multiplier", 2) ?? 2;

            _frameInterpolationFragment.Build(builder, registry, new FrameInterpolationFragment.Parameters
            {
                RifeModel = interpolationFragment?.GetString("rife_model", "rife49.pth") ?? "rife49.pth",
                FrameMultiplier = multiplier,
                ScaleBy = interpolationFragment?.GetDouble("scale_by", 2.0) ?? 2.0
            });

            _saveVideoFragment.Build(builder, registry, new SaveVideoFragment.Parameters
            {
                NodeId = "video_output",
                FrameRate = baseFrameRate * multiplier,
                ImageInputName = "frames_output"
            });
        }
        else
        {
            _saveVideoFragment.Build(builder, registry, new SaveVideoFragment.Parameters
            {
                NodeId = "video_output",
                FrameRate = baseFrameRate,
                ImageInputName = "image_output"
            });
        }

        return builder.ToComfyWorkflow(registry);
    }
}
