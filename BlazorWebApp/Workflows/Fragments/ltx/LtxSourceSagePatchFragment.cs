using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxSourceSagePatchFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_source_sage_patch",
        Type = FragmentType.Enhancement,
        Title = "Sage/Torch Patch",
        Icon = "fa-solid fa-bolt",
        Order = 70,
        Collapsible = true,
        DefaultCollapsed = false
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var sageId = $"{scope}ltx_source_sage_attention";
        var torchId = $"{scope}ltx_source_torch_settings";

        builder.AddNode(sageId, node => node
            .Type("PathchSageAttentionKJ")
            .Title($"{scopeTitle}PathchSageAttentionKJ")
            .InputRef("model", registry.GetRef($"{scope}model_output"))
            .Input("sage_attention", "auto")
            .Input("allow_compile", true));

        builder.AddNode(torchId, node => node
            .Type("ModelPatchTorchSettings")
            .Title($"{scopeTitle}Model Patch Torch Settings")
            .InputFromNode("model", sageId, 0)
            .Input("enable_fp16_accumulation", true));

        registry.Register($"{scope}model_output", torchId, 0);
    }
}