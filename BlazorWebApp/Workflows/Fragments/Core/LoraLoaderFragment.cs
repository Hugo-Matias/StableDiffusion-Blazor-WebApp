using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a LoRA and applies it to model and CLIP.
/// Overwrites model_output and clip_output in the node registry.
/// </summary>
public class LoraLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "lora_loader",
        Type = Workflows.Models.FragmentType.Loader,
        Title = "LoRA Loader",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the LoRA loader fragment.
    /// </summary>
    public class Parameters
    {
        public string LoraLoaderId { get; set; } = "lora_loader_0";
        public string LoraName { get; set; } = "";
        public string? LoraPath { get; set; }
        public double LoraStrength { get; set; } = 1.0;
        public double? LoraStrengthClip { get; set; }
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // This fragment is typically called with explicit parameters in a loop
        // The interface method is here for compatibility but workflow should use the explicit overload
    }

    /// <summary>
    /// Builds the fragment with explicit parameters (primary usage).
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope);
    }

    /// <summary>
    /// Builds the fragment for a specific LoRA from GenerationParameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        int loraIndex,
        string scope = "")
    {
        if (parameters.Loras == null || loraIndex >= parameters.Loras.Count)
            return;

        var lora = parameters.Loras[loraIndex];

        BuildInternal(builder, registry, new Parameters
        {
            LoraLoaderId = $"lora_loader_{loraIndex}",
            LoraName = lora.Name ?? "",
            LoraPath = lora.Path,
            LoraStrength = lora.Strength
        }, scope);
    }

    /// <summary>
    /// Builds LoRA loader nodes for all enabled LoRAs in the generation parameters.
    /// Skips disabled LoRAs automatically.
    /// </summary>
    public void BuildAll(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        IList<Lora>? loras,
        string scope = "")
    {
        if (loras == null || loras.Count == 0) return;

        for (int i = 0; i < loras.Count; i++)
        {
            var lora = loras[i];
            if (!lora.IsEnabled) continue;

            BuildInternal(builder, registry, new Parameters
            {
                LoraLoaderId = $"lora_loader_{i}",
                LoraName = lora.Name ?? "",
                LoraPath = lora.Path,
                LoraStrength = lora.Strength
            }, scope);
        }
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope)
    {
        // Get current model and clip references
        var modelRef = registry.GetRef($"{scope}model_output");
        var clipRef = registry.GetRef($"{scope}clip_output");

        // Use path if available, otherwise use name
        var loraFile = !string.IsNullOrEmpty(p.LoraPath) ? p.LoraPath : p.LoraName;
        var clipStrength = p.LoraStrengthClip ?? 1.0;

        builder.AddNode(p.LoraLoaderId, node => node
            .Type("LoraLoader")
            .Title("LoRA Loader")
            .Input("lora_name", loraFile)
            .Input("strength_model", p.LoraStrength)
            .Input("strength_clip", clipStrength)
            .InputRef("model", modelRef)
            .InputRef("clip", clipRef));

        // Overwrite model and clip outputs with LoRA-modified versions
        registry.Register($"{scope}model_output", p.LoraLoaderId, 0);
        registry.Register($"{scope}clip_output", p.LoraLoaderId, 1);
    }
}
