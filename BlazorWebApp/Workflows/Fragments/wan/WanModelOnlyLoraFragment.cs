using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanModelOnlyLoraFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_model_only_lora",
        Type = FragmentType.Loader,
        Title = "Wan Model-Only LoRA",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "model_only_lora";
        public string LoraName { get; set; } = "";
        public double Strength { get; set; } = 1;
        public string ModelInputName { get; set; } = "model_output";
        public string ModelOutputName { get; set; } = "model_output";
        public string Title { get; set; } = "Model-Only LoRA";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}{fragmentParams.NodeId}";
        builder.AddNode(nodeId, node => node
            .Type("LoraLoaderModelOnly")
            .Title($"{scopeTitle}{fragmentParams.Title}")
            .InputRef("model", registry.GetRef(fragmentParams.ModelInputName))
            .Input("lora_name", fragmentParams.LoraName)
            .Input("strength_model", fragmentParams.Strength));

        registry.Register(fragmentParams.ModelOutputName, nodeId, 0);
    }
}
