using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ideogram;

/// <summary>
/// Loads the Ideogram 4 model set and encodes the positive prompt.
///
/// Registers:
///   {scope}model_output        - main UNet (raw, before CFGOverride)
///   {scope}uncond_model_output - unconditional UNet
///   {scope}clip_output         - CLIPLoader (type=ideogram4)
///   {scope}vae_output          - VAELoader
///   {scope}positive_output     - CLIPTextEncode from positive prompt
///   {scope}negative_output     - ConditioningZeroOut derived from positive
///
/// Unconditional conditioning follows Ideogram 4's asymmetric CFG pattern:
/// the same positive conditioning is zeroed-out to form the unconditional side.
/// IdeogramSamplerFragment applies CFGOverride on the main model and feeds both
/// models into DualModelGuider.
/// </summary>
public class LoadIdeogramFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader_ideogram",
        Type = FragmentType.Loader,
        Title = "Ideogram Loader",
        IsHidden = true
    };

    public record Parameters
    {
        public required string UnetName { get; init; }
        public required string UnconditionalUnetName { get; init; }
        public required string ClipName { get; init; }
        public required string VaeName { get; init; }
        public required string Positive { get; init; }
        public string WeightDtype { get; init; } = "default";
        public string ClipDevice { get; init; } = "default";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var s = scope;
        var st = scopeTitle;

        // 1. Load main diffusion model
        builder.AddNode($"{s}unet_loader", node => node
            .ClassType("UNETLoader")
            .Title($"{st}Load Ideogram 4 Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", p.WeightDtype));

        // 2. Load unconditional diffusion model
        builder.AddNode($"{s}uncond_unet_loader", node => node
            .ClassType("UNETLoader")
            .Title($"{st}Load Ideogram 4 Unconditional Model")
            .Input("unet_name", p.UnconditionalUnetName)
            .Input("weight_dtype", p.WeightDtype));

        // 3. Load CLIP encoder (type=ideogram4)
        builder.AddNode($"{s}clip_loader", node => node
            .ClassType("CLIPLoader")
            .Title($"{st}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", "ideogram4")
            .Input("device", p.ClipDevice));

        // 4. Load VAE
        builder.AddNode($"{s}vae_loader", node => node
            .ClassType("VAELoader")
            .Title($"{st}Load VAE")
            .Input("vae_name", p.VaeName));

        // 5. Encode positive prompt
        builder.AddNode($"{s}positive_encode", node => node
            .ClassType("CLIPTextEncode")
            .Title($"{st}CLIP Text Encode (Positive Prompt)")
            .Input("text", p.Positive)
            .InputFromNode("clip", $"{s}clip_loader", 0));

        // 6. Zero-out conditioning for unconditional side
        //    Ideogram 4 uses asymmetric CFG: negative = zeroed positive conditioning
        builder.AddNode($"{s}conditioning_zero_out", node => node
            .ClassType("ConditioningZeroOut")
            .Title($"{st}Conditioning Zero Out")
            .InputFromNode("conditioning", $"{s}positive_encode", 0));

        // Register outputs
        registry.Register($"{s}model_output", $"{s}unet_loader", 0);
        registry.Register($"{s}uncond_model_output", $"{s}uncond_unet_loader", 0);
        registry.Register($"{s}clip_output", $"{s}clip_loader", 0);
        registry.Register($"{s}vae_output", $"{s}vae_loader", 0);
        registry.Register($"{s}positive_output", $"{s}positive_encode", 0);
        registry.Register($"{s}negative_output", $"{s}conditioning_zero_out", 0);
    }

    // IFragmentBuilder implementation - delegates to typed Build method
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        throw new InvalidOperationException(
            "LoadIdeogramFragment must be called with the typed Build(builder, registry, Parameters) overload. " +
            "The workflow's Build() method handles parameter extraction.");
    }
}
