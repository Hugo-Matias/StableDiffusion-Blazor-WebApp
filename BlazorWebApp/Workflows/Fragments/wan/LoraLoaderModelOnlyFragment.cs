using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that applies a LoRA to a model using LoraLoader (model-only, clip strength 1).
/// Designed for dual-model workflows where each model gets independent LoRAs.
/// All node IDs, input/output registry names are fully parameterized.
/// </summary>
public class LoraLoaderModelOnlyFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "lora_loader_model_only",
        Type = FragmentType.Loader,
        Title = "LoRA Loader (Model Only)",
        IsHidden = true
    };

    public class Parameters
    {
        public string LoraLoaderId { get; set; } = "lora_loader";
        public string LoraName { get; set; } = "";
        public string LoraPath { get; set; } = "";
        public float LoraStrength { get; set; } = 1.0f;
        public float LoraClipStrength { get; set; } = 1.0f;
        public string ModelInputName { get; set; } = "model_output";
        public string ModelOutputName { get; set; } = "lora_model_output";
        public string Title { get; set; } = "LoRA Loader";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
    }

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
        var nodeId = $"{scope}{p.LoraLoaderId}";
        var loraFile = !string.IsNullOrEmpty(p.LoraPath) ? p.LoraPath : p.LoraName;
        var modelRef = registry.GetRef(p.ModelInputName);

        builder.AddNode(nodeId, node => node
            .Type("LoraLoader")
            .Title($"{scopeTitle}{p.Title}")
            .Input("lora_name", loraFile)
            .Input("strength_model", (double)p.LoraStrength)
            .Input("strength_clip", (double)p.LoraClipStrength)
            .InputRef("model", modelRef));

        registry.Register(p.ModelOutputName, nodeId, 0);
    }
}
