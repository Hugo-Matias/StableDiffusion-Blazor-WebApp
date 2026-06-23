using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

/// <summary>
/// Fragment that loads the Qwen Edit model pipeline:
/// UNETLoader -> LoraLoaderModelOnly -> ModelSamplingAuraFlow -> CFGNorm
/// Plus CLIPLoader (qwen_image type) and VAELoader.
/// Registers model_output (CFGNorm), clip_output, and vae_output.
/// </summary>
public class LoadQwenEditFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader_qwen_edit",
        Type = FragmentType.Loader,
        Title = "Load Qwen Edit",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the load qwen edit fragment.
    /// </summary>
    public class Parameters
    {
        public string UnetName { get; set; } = "qwen_image_edit_2509_fp8_e4m3fn.safetensors";
        public string ClipName { get; set; } = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        public string VaeName { get; set; } = "qwen_image_vae.safetensors";
        public string LoraName { get; set; } = "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors";
        public double LoraStrength { get; set; } = 1;
        public double ModelShift { get; set; } = 3;
        public double CfgNormStrength { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        BuildInternal(builder, registry, new Parameters
        {
            UnetName = fragment?.GetString("unet_name") ?? parameters.Assets?.GetValueOrDefault("Model") ?? "qwen_image_edit_2509_fp8_e4m3fn.safetensors",
            ClipName = fragment?.GetString("clip_name") ?? parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_2.5_vl_7b_fp8_scaled.safetensors",
            VaeName = fragment?.GetString("vae_name") ?? parameters.Assets?.GetValueOrDefault("Vae") ?? "qwen_image_vae.safetensors",
            LoraName = fragment?.GetString("lora_name", "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors") ?? "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors",
            LoraStrength = fragment?.GetDouble("lora_strength", 1) ?? 1,
            ModelShift = fragment?.GetDouble("model_shift", 3) ?? 3,
            CfgNormStrength = fragment?.GetDouble("cfg_norm_strength", 1) ?? 1
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
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

        // CLIP Loader (qwen_image type)
        builder.AddNode($"{scope}clip_loader", node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", "qwen_image")
            .Input("device", "default"));

        // VAE Loader
        builder.AddNode($"{scope}vae_loader", node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        // LoRA Loader (model only)
        builder.AddNode($"{scope}lora_loader", node => node
            .Type("LoraLoaderModelOnly")
            .Title($"{scopeTitle}LoRA Loader (Lightning)")
            .Input("lora_name", p.LoraName)
            .Input("strength_model", p.LoraStrength)
            .InputRef("model", ($"{scope}unet_loader", 0)));

        // ModelSamplingAuraFlow
        builder.AddNode($"{scope}model_sampling", node => node
            .Type("ModelSamplingAuraFlow")
            .Title($"{scopeTitle}ModelSamplingAuraFlow")
            .Input("shift", p.ModelShift)
            .InputRef("model", ($"{scope}lora_loader", 0)));

        // CFGNorm
        builder.AddNode($"{scope}cfg_norm", node => node
            .Type("CFGNorm")
            .Title($"{scopeTitle}CFGNorm")
            .Input("strength", p.CfgNormStrength)
            .InputRef("model", ($"{scope}model_sampling", 0)));

        // Register outputs
        registry.Register($"{scope}model_output", $"{scope}cfg_norm", 0);
        registry.Register($"{scope}clip_output", $"{scope}clip_loader", 0);
        registry.Register($"{scope}vae_output", $"{scope}vae_loader", 0);
    }
}
