using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Loaders;

/// <summary>
/// Fragment that loads diffusion models with prompt encoding.
/// Used for detailer and other scoped workflows that need their own model + prompts.
/// Registers scoped outputs: model_output, clip_output, vae_output, positive_output, negative_output.
/// </summary>
public class LoadDiffusionWithPromptsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_diffusion_w_prompts",
        Type = FragmentType.Loader,
        Title = "Load Diffusion with Prompts",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the load diffusion with prompts fragment.
    /// </summary>
    public class Parameters
    {
        public string UnetName { get; set; } = "";
        public string ClipName { get; set; } = "";
        public string ClipType { get; set; } = "stable_diffusion";
        public string VaeName { get; set; } = "";
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public int BatchSize { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Get detailer-specific parameters
        var detailerFragment = parameters.GetFragment("detailer");
        var promptsFragment = parameters.GetFragment("prompts");
        var latentFragment = parameters.GetFragment("latent");
        
        var unetName = detailerFragment?.GetString("detailer_checkpoint") 
                       ?? parameters.Assets?.GetValueOrDefault("Model") ?? "";
        var clipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "";
        var clipType = "lumina2"; // Z-Image uses lumina2
        var vaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "";
        var positive = detailerFragment?.GetString("detailer_prompt") 
                       ?? promptsFragment?.GetString("positive", "") ?? "";
        var negative = detailerFragment?.GetString("detailer_negative_prompt") 
                       ?? promptsFragment?.GetString("negative", "") ?? "";
        var width = latentFragment?.GetInt("width", 1024) ?? 1024;
        var height = latentFragment?.GetInt("height", 1024) ?? 1024;
        var batchSize = latentFragment?.GetInt("batch_size", 1) ?? 1;

        BuildInternal(builder, registry, new Parameters
        {
            UnetName = unetName,
            ClipName = clipName,
            ClipType = clipType,
            VaeName = vaeName,
            Positive = positive,
            Negative = negative,
            Width = width,
            Height = height,
            BatchSize = batchSize
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        // UNet Loader
        builder.AddNode($"{scope}unet_loader", node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Diffusion Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", "default"));

        // CLIP Loader
        builder.AddNode($"{scope}clip_loader", node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", p.ClipType)
            .Input("device", "default"));

        // VAE Loader
        builder.AddNode($"{scope}vae_loader", node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        // Positive text primitive
        builder.AddNode($"{scope}text_positive", node => node
            .Type("PrimitiveStringMultiline")
            .Title($"{scopeTitle}Prompt")
            .Input("value", p.Positive));

        // Negative text primitive
        builder.AddNode($"{scope}text_negative", node => node
            .Type("PrimitiveStringMultiline")
            .Title($"{scopeTitle}Negative Prompt")
            .Input("value", p.Negative));

        // LoRA loader for positive (lazy loader that handles LoRA syntax in prompts)
        builder.AddNode($"{scope}lora_positive", node => node
            .Type("PCLazyLoraLoader")
            .Title($"{scopeTitle}LoRAs (Positive)")
            .InputRef("text", ($"{scope}text_positive", 0))
            .InputRef("model", ($"{scope}unet_loader", 0))
            .InputRef("clip", ($"{scope}clip_loader", 0)));

        // LoRA loader for negative
        builder.AddNode($"{scope}lora_negative", node => node
            .Type("PCLazyLoraLoader")
            .Title($"{scopeTitle}LoRAs (Negative)")
            .InputRef("text", ($"{scope}text_negative", 0))
            .InputRef("model", ($"{scope}unet_loader", 0))
            .InputRef("clip", ($"{scope}clip_loader", 0)));

        // Encode positive
        builder.AddNode($"{scope}encode_positive", node => node
            .Type("PCLazyTextEncode")
            .Title($"{scopeTitle}Encode Positive")
            .InputRef("text", ($"{scope}text_positive", 0))
            .InputRef("clip", ($"{scope}lora_positive", 1)));

        // Encode negative
        builder.AddNode($"{scope}encode_negative", node => node
            .Type("PCLazyTextEncode")
            .Title($"{scopeTitle}Encode Negative")
            .InputRef("text", ($"{scope}text_negative", 0))
            .InputRef("clip", ($"{scope}lora_negative", 1)));

        // Register all scoped outputs
        registry.Register($"{scope}model_output", $"{scope}lora_positive", 0);
        registry.Register($"{scope}clip_output", $"{scope}lora_positive", 1);
        registry.Register($"{scope}vae_output", $"{scope}vae_loader", 0);
        registry.Register($"{scope}positive_output", $"{scope}encode_positive", 0);
        registry.Register($"{scope}negative_output", $"{scope}encode_negative", 0);
    }
}
