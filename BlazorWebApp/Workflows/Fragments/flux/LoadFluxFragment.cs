using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// Fragment for loading Flux models with dual CLIP, ReFlux, and advanced VAE encoding.
/// Supports scoped loading for detailer.
/// </summary>
public class LoadFluxFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader_flux",
        Type = FragmentType.Loader,
        Title = "Flux Loader",
        IsHidden = true
    };

    public record Parameters
    {
        public required string UnetName { get; init; }
        public required string ClipName1 { get; init; }
        public required string ClipName2 { get; init; }
        public required string VaeName { get; init; }
        public required string Positive { get; init; }
        public double Guidance { get; init; } = 3.5;
        public bool RefluxEnabled { get; init; } = true;
        public string Scaling { get; init; } = "exponential";
        public double MaxShift { get; init; } = 1.35;
        public double BaseShift { get; init; } = 0.85;
        public int Width { get; init; } = 872;
        public int Height { get; init; } = 1248;
        public int BatchSize { get; init; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder, 
        NodeRegistry registry, 
        Parameters parameters,
        string scope = "",
        string scopeTitle = "")
    {
        var s = scope;
        var st = scopeTitle;

        // 1. Load UNet
        builder.AddNode($"{s}unet_loader", node => node
            .ClassType("UNETLoader")
            .Title($"{st}Load Diffusion Model")
            .Input("unet_name", parameters.UnetName)
            .Input("weight_dtype", "default"));

        // 2. Load Dual CLIPs (T5 + ViT)
        builder.AddNode($"{s}dual_clip_loader", node => node
            .ClassType("DualCLIPLoader")
            .Title($"{st}DualCLIPLoader")
            .Input("clip_name1", parameters.ClipName1)
            .Input("clip_name2", parameters.ClipName2)
            .Input("type", "flux")
            .Input("device", "default"));

        // 3. Load VAE
        builder.AddNode($"{s}vae_loader", node => node
            .ClassType("VAELoader")
            .Title($"{st}Load VAE")
            .Input("vae_name", parameters.VaeName));

        // 4. Positive prompt text
        builder.AddNode($"{s}text_positive", node => node
            .ClassType("PrimitiveStringMultiline")
            .Title($"{st}Prompt")
            .Input("value", parameters.Positive));

        // 5. LoRA loader (handles LoRAs embedded in prompt)
        builder.AddNode($"{s}lora_loader", node => node
            .ClassType("PCLazyLoraLoader")
            .Title($"{st}LoRAs")
            .InputFromNode("text", $"{s}text_positive", 0)
            .InputFromNode("model", $"{s}unet_loader", 0)
            .InputFromNode("clip", $"{s}dual_clip_loader", 0));

        // 6. ReFlux patcher (optional)
        if (parameters.RefluxEnabled)
        {
            builder.AddNode($"{s}reflux_patcher", node => node
                .ClassType("ReFluxPatcher")
                .Title($"{st}ReFluxPatcher")
                .Input("style_dtype", "float64")
                .Input("enable", true)
                .InputFromNode("model", $"{s}lora_loader", 0));
        }

        // 7. Encode positive prompt
        builder.AddNode($"{s}prompt_encode", node => node
            .ClassType("PCLazyTextEncode")
            .Title($"{st}Encode Prompt")
            .InputFromNode("text", $"{s}text_positive", 0)
            .InputFromNode("clip", $"{s}lora_loader", 1));

        // 8. Apply Flux guidance
        builder.AddNode($"{s}flux_guidance", node => node
            .ClassType("FluxGuidance")
            .Title($"{st}FluxGuidance")
            .Input("guidance", parameters.Guidance)
            .InputFromNode("conditioning", $"{s}prompt_encode", 0));

        // 9. Empty negative encode (Flux doesn't use negative prompts traditionally)
        builder.AddNode($"{s}empty_encode", node => node
            .ClassType("CLIPTextEncode")
            .Title($"{st}Empty Negative Encode")
            .Input("text", "")
            .InputFromNode("clip", $"{s}dual_clip_loader", 0));

        // 10. Create empty latent
        builder.AddNode($"{s}empty_latent", node => node
            .ClassType("EmptySD3LatentImage")
            .Title($"{st}EmptySD3LatentImage")
            .Input("width", parameters.Width)
            .Input("height", parameters.Height)
            .Input("batch_size", parameters.BatchSize));

        // 11. Advanced VAE encode
        builder.AddNode($"{s}vae_encode_advanced", node => node
            .ClassType("VAEEncodeAdvanced")
            .Title($"{st}VAEEncodeAdvanced")
            .Input("resize_to_input", "false")
            .Input("width", parameters.Width)
            .Input("height", parameters.Height)
            .Input("mask_channel", "red")
            .Input("invert_mask", false)
            .Input("latent_type", "16_channels")
            .InputFromNode("latent", $"{s}empty_latent", 0)
            .InputFromNode("vae", $"{s}vae_loader", 0));

        // 12. Model sampling with advanced resolution
        // Wire model from reflux_patcher when enabled, otherwise directly from lora_loader
        var modelSourceNode = parameters.RefluxEnabled ? $"{s}reflux_patcher" : $"{s}lora_loader";
        builder.AddNode($"{s}flux_model_sampling", node => node
            .ClassType("ModelSamplingAdvancedResolution")
            .Title($"{st}ModelSamplingAdvancedResolution")
            .Input("scaling", parameters.Scaling)
            .Input("max_shift", parameters.MaxShift)
            .Input("base_shift", parameters.BaseShift)
            .InputFromNode("model", modelSourceNode, 0)
            .InputFromNode("latent_image", $"{s}vae_encode_advanced", 3));

        // Register outputs
        registry.Register($"{s}model_output", $"{s}flux_model_sampling", 0);
        registry.Register($"{s}clip_output", $"{s}dual_clip_loader", 0);
        registry.Register($"{s}vae_output", $"{s}vae_loader", 0);
        registry.Register($"{s}latent_output", $"{s}vae_encode_advanced", 3);
        registry.Register($"{s}positive_output", $"{s}flux_guidance", 0);
        registry.Register($"{s}negative_output", $"{s}empty_encode", 0);
    }

    // IFragmentBuilder implementation - delegates to typed Build method
    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        // This fragment is not typically called via the interface directly.
        // Workflow classes use the typed Build method with Parameters record.
        // This implementation exists for interface compliance.
        throw new InvalidOperationException(
            $"LoadFluxFragment should be called with the typed Build(builder, registry, Parameters) method. " +
            $"Use the workflow's Build method which handles parameter extraction.");
    }
}
