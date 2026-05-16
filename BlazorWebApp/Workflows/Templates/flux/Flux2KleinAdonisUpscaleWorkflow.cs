using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Flux;

/// <summary>
/// Flux2 Klein Adonis upscale workflow. Two-pass detail-reconstruction pipeline driven
/// by the hardcoded Adonis LoRAs (adonis_base + adonis_refine) and SharkOptions_Beta
/// laplacian noise. The input image is scaled to a target megapixel target, encoded
/// as a reference latent, and resampled twice through ClownsharKSampler_Beta:
/// pass 1 (standard, 5 of 9 steps, base LoRA) then pass 2 (resample, full 9 steps,
/// refine LoRA). Per-LoRA strength and target sampler (base / refine / both) are
/// exposed via AdonisSettingsForm.
/// </summary>
public class Flux2KleinAdonisUpscaleWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SamplerFragment _samplerFragment = new() { Defaults = new() { Steps = 9, Cfg = 1.0, Denoise = 0.8, Eta = 1.0, SamplerName = "exponential/res_2s", Scheduler = "simple" } };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Settings fragments (UI-only)
    private readonly AdonisSettingsFragment _adonisSettingsFragment = new();

    // Hardcoded Adonis LoRAs (file names match upstream workflow)
    private const string AdonisBaseLora = "Util/adonis_base.safetensors";
    private const string AdonisRefineLora = "Util/adonis_refine.safetensors";

    public WorkflowMetadata Metadata => new()
    {
        Title = "Flux2 Klein Adonis Upscale",
        Description = "Two-pass Adonis upscaler that reconstructs detail by scaling the input " +
                      "image to a target megapixel count and resampling it through Flux 2 Klein " +
                      "with the adonis_base / adonis_refine LoRAs. Use it to clean up halftone, " +
                      "noise, and softness on cellphone or low-resolution sources.",
        Base = Data.Enums.ModelBase.Flux,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "flux-2-klein-9b.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_3_8b_fp8mixed.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "flux2-vae.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "input_image",
                Label = "Input Image",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = false
            }
        ],
        CompatibleResourceBaseModels =
        [
            "Flux.2 D",
            "Flux.2 Klein 9B",
            "Flux.2 Klein 9B-base",
            "Flux.2 Klein 4B",
            "Flux.2 Klein 4B-base"
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _adonisSettingsFragment;
        yield return _samplerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");
        var adonisFragment = parameters.GetFragment("adonis_settings");

        // Resolve input image source
        parameters.Sources.TryGetValue("input_image", out var inputSource);
        var imagePath = inputSource?.Filename ?? inputSource?.FilePath ?? "";

        // Adonis tunables
        var megapixels = adonisFragment?.GetDouble("megapixels", 1.7) ?? 1.7;
        var multipleOf = adonisFragment?.GetInt("multiple_of", 16) ?? 16;
        var baseStrength = adonisFragment?.GetDouble("adonis_base_strength", 1.0) ?? 1.0;
        var refineStrength = adonisFragment?.GetDouble("adonis_refine_strength", 1.0) ?? 1.0;
        var baseTarget = adonisFragment?.GetString("adonis_base_target", AdonisSettingsFragment.TargetBase)
                         ?? AdonisSettingsFragment.TargetBase;
        var refineTarget = adonisFragment?.GetString("adonis_refine_target", AdonisSettingsFragment.TargetRefine)
                           ?? AdonisSettingsFragment.TargetRefine;

        // 1. Load models (UNet + CLIP[flux2] + VAE)
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "flux-2-klein-9b.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
            ClipType = "flux2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "flux2-vae.safetensors"
        });

        // 2. Apply user LoRAs (if any) - modifies model_output and clip_output. Adonis
        //    LoRAs are intentionally NOT in this chain so they can target individual
        //    samplers; they branch from the user-LoRA-modified model below.
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        var sharedModelRef = registry.GetRef("model_output");

        // 3. Branch model into per-sampler stacks, applying each Adonis LoRA per its target.
        var baseSamplerModelRef = sharedModelRef;
        var refineSamplerModelRef = sharedModelRef;
        ApplyAdonisLora(builder, ref baseSamplerModelRef, ref refineSamplerModelRef,
            slot: "base", loraFile: AdonisBaseLora, strength: baseStrength, target: baseTarget);
        ApplyAdonisLora(builder, ref baseSamplerModelRef, ref refineSamplerModelRef,
            slot: "refine", loraFile: AdonisRefineLora, strength: refineStrength, target: refineTarget);

        // 4. Encode prompts (positive only). Negative is replaced by ConditioningZeroOut below.
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = ""
        });

        // 5. Replace negative_output with a ConditioningZeroOut of positive_output.
        var positiveRef = registry.GetRef("positive_output");
        builder.AddNode("negative_zeroed", node => node
            .Type("ConditioningZeroOut")
            .Title("Negative (zeroed positive)")
            .InputRef("conditioning", positiveRef));
        registry.Register("negative_output", "negative_zeroed", 0);

        // 6. Load + scale input image to the target megapixel count, snapped to multiple_of.
        builder.AddNode("input_loader", node => node
            .Type("LoadImage")
            .Title("Load Input Image")
            .Input("image", imagePath));

        // ImageScaleToTotalPixelsX (scale-image-to-total-pixels-advanced pack).
        // Verified inputs: megapixels (FLOAT), multiple_of (INT), resize_mode (COMBO:
        // stretch|crop|pad), upscale_method (COMBO). Outputs: IMAGE(0), width(1), height(2).
        builder.AddNode("input_scaler", node => node
            .Type("ImageScaleToTotalPixelsX")
            .Title("Scale Input Image")
            .Input("megapixels", megapixels)
            .Input("multiple_of", multipleOf)
            .Input("resize_mode", "crop")
            .Input("upscale_method", "lanczos")
            .InputFromNode("image", "input_loader", 0));

        registry.Register("image_size_width", "input_scaler", 1);
        registry.Register("image_size_height", "input_scaler", 2);

        // 7. VAE encode the scaled image to get the reference latent.
        var vaeRef = registry.GetRef("vae_output");
        builder.AddNode("input_vae_encode", node => node
            .Type("VAEEncode")
            .Title("VAE Encode Input")
            .InputFromNode("pixels", "input_scaler", 0)
            .InputRef("vae", vaeRef));

        // 8. Wrap positive + negative conditioning with ReferenceLatent of the encoded input.
        builder.AddNode("ref_latent_positive", node => node
            .Type("ReferenceLatent")
            .Title("Reference Latent (positive)")
            .InputRef("conditioning", registry.GetRef("positive_output"))
            .InputFromNode("latent", "input_vae_encode", 0));
        registry.Register("positive_output", "ref_latent_positive", 0);

        builder.AddNode("ref_latent_negative", node => node
            .Type("ReferenceLatent")
            .Title("Reference Latent (negative)")
            .InputRef("conditioning", registry.GetRef("negative_output"))
            .InputFromNode("latent", "input_vae_encode", 0));
        registry.Register("negative_output", "ref_latent_negative", 0);

        // 9. Empty Flux2 latent sized to the scaled input dimensions.
        var widthRef = registry.GetRef("image_size_width");
        var heightRef = registry.GetRef("image_size_height");
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            BatchSize = 1,
            LatentClass = "EmptyFlux2LatentImage",
            WidthRef = widthRef,
            HeightRef = heightRef
        });

        // 10. SharkOptions_Beta for the base sampler (laplacian noise feature of Adonis).
        //     Required inputs verified against /object_info/SharkOptions_Beta:
        //     noise_type_init (COMBO), s_noise_init (FLOAT), denoise_alt (FLOAT),
        //     channelwise_cfg (BOOLEAN). Upstream widget values: [laplacian, 1, 1, false].
        builder.AddNode("shark_options", node => node
            .Type("SharkOptions_Beta")
            .Title("Adonis Shark Options")
            .Input("noise_type_init", "laplacian")
            .Input("s_noise_init", 1.0)
            .Input("denoise_alt", 1.0)
            .Input("channelwise_cfg", false));
        var optionsRef = (nodeId: "shark_options", outputIndex: 0);

        // 11. Resolve sampler controls (shared between both passes per upstream).
        // Fallback sampler name verified against /object_info/ClownsharKSampler_Beta;
        // bare "res_2s" is not a COMBO member.
        var samplerName = samplerFragment?.GetString("sampler_name", "exponential/res_2s") ?? "exponential/res_2s";
        var scheduler = samplerFragment?.GetString("scheduler", "simple") ?? "simple";
        var steps = samplerFragment?.GetInt("steps", 9) ?? 9;
        var cfg = samplerFragment?.GetDouble("cfg", 1.0) ?? 1.0;
        var denoise = samplerFragment?.GetDouble("denoise", 0.8) ?? 0.8;
        var eta = samplerFragment?.GetDouble("eta", 1.0) ?? 1.0;
        var seed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        // 12. Base sampler pass (5 of 9 steps, standard mode, with shark options).
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = "sampler_base",
            Title = "Adonis Base Sampler",
            ClassType = "ClownsharKSampler_Beta",
            SamplerName = samplerName,
            Scheduler = scheduler,
            Steps = steps,
            StepsToRun = 5,
            Cfg = cfg,
            Denoise = denoise,
            Eta = eta,
            Seed = seed,
            SamplerMode = "standard",
            ModelOverride = baseSamplerModelRef,
            OptionsRef = optionsRef
        });

        // 13. Refine sampler pass (resample, runs to completion, no options).
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = "sampler_refine",
            Title = "Adonis Refine Sampler",
            ClassType = "ClownsharKSampler_Beta",
            SamplerName = samplerName,
            Scheduler = scheduler,
            Steps = steps,
            StepsToRun = -1,
            Cfg = cfg,
            Denoise = denoise,
            Eta = eta,
            Seed = seed,
            SamplerMode = "resample",
            ModelOverride = refineSamplerModelRef
        });

        // 14. VAE decode and save.
        _vaeDecodeFragment.Build(builder, registry);
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }

    /// <summary>
    /// Inserts a <c>LoraLoaderModelOnly</c> node into the per-sampler model branches
    /// indicated by <paramref name="target"/> (<c>"base"</c>, <c>"refine"</c>, or
    /// <c>"both"</c>). Updates the supplied refs in place so subsequent LoRAs chain
    /// from the just-applied node.
    /// </summary>
    private static void ApplyAdonisLora(
        ComfyWorkflowBuilder builder,
        ref (string nodeId, int outputIndex) baseModelRef,
        ref (string nodeId, int outputIndex) refineModelRef,
        string slot,
        string loraFile,
        double strength,
        string target)
    {
        var toBase = target is AdonisSettingsFragment.TargetBase or AdonisSettingsFragment.TargetBoth;
        var toRefine = target is AdonisSettingsFragment.TargetRefine or AdonisSettingsFragment.TargetBoth;

        if (toBase)
        {
            var nodeId = $"adonis_{slot}_for_base";
            var inputRef = baseModelRef;
            builder.AddNode(nodeId, node => node
                .Type("LoraLoaderModelOnly")
                .Title($"Adonis {slot} (base)")
                .Input("lora_name", loraFile)
                .Input("strength_model", strength)
                .InputRef("model", inputRef));
            baseModelRef = (nodeId, 0);
        }

        if (toRefine)
        {
            var nodeId = $"adonis_{slot}_for_refine";
            var inputRef = refineModelRef;
            builder.AddNode(nodeId, node => node
                .Type("LoraLoaderModelOnly")
                .Title($"Adonis {slot} (refine)")
                .Input("lora_name", loraFile)
                .Input("strength_model", strength)
                .InputRef("model", inputRef));
            refineModelRef = (nodeId, 0);
        }
    }
}
