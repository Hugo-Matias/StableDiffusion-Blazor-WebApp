using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a checkpoint model (model+clip+vae from a single file) with
/// PCLazyLoraLoader for LoRA scheduling and PCLazyTextEncode for prompt encoding.
/// Used by StableDiffusion workflows. Supports scoping for detailer.
/// Registers: model_output, clip_output, vae_output, positive_output, negative_output.
/// </summary>
public class LoadCheckpointFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_checkpoint",
        Type = FragmentType.Loader,
        Title = "Load Checkpoint",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the load checkpoint fragment.
    /// </summary>
    public class Parameters
    {
        public string LoaderId { get; set; } = "model_loader";
        public string CheckpointName { get; set; } = "";
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
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
            LoaderId = fragment?.GetString("loader_id", "model_loader") ?? "model_loader",
            CheckpointName = fragment?.GetString("ckpt_name", "") ?? "",
            Positive = fragment?.GetString("prompt", "") ?? "",
            Negative = fragment?.GetString("negative", "") ?? ""
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
        // Checkpoint loader (outputs: model[0], clip[1], vae[2])
        builder.AddNode($"{scope}{p.LoaderId}", node => node
            .Type("CheckpointLoaderSimple")
            .Title($"{scopeTitle}Load Checkpoint")
            .Input("ckpt_name", p.CheckpointName));

        // Positive text primitive
        builder.AddNode($"{scope}text_prompt", node => node
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
            .InputRef("text", ($"{scope}text_prompt", 0))
            .InputRef("model", ($"{scope}{p.LoaderId}", 0))
            .InputRef("clip", ($"{scope}{p.LoaderId}", 1)));

        // LoRA loader for negative
        builder.AddNode($"{scope}lora_negative", node => node
            .Type("PCLazyLoraLoader")
            .Title($"{scopeTitle}LoRAs (Negative)")
            .InputRef("text", ($"{scope}text_negative", 0))
            .InputRef("model", ($"{scope}lora_positive", 0))
            .InputRef("clip", ($"{scope}lora_positive", 1)));

        // Encode positive
        builder.AddNode($"{scope}encode_positive", node => node
            .Type("PCLazyTextEncode")
            .Title($"{scopeTitle}Encode Positive")
            .InputRef("text", ($"{scope}text_prompt", 0))
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
        registry.Register($"{scope}vae_output", $"{scope}{p.LoaderId}", 2);
        registry.Register($"{scope}positive_output", $"{scope}encode_positive", 0);
        registry.Register($"{scope}negative_output", $"{scope}encode_negative", 0);
    }
}
