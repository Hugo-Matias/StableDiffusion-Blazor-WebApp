using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Optional SageAttention performance patch for the MiniMax H3 diffusion model.
/// Overwrites model_output with the patched model. Enabled by default (matches source graph values).
/// </summary>
public class MiniMaxSagePatchFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_sage_patch",
        Type = FragmentType.Enhancement,
        Title = "SageAttention (performance)",
        Description = "Patches the model to use SageAttention's memory-efficient attention kernel. " +
                      "Disable if you don't have a working SageAttention install on the ComfyUI side.",
        Icon = "fa-solid fa-bolt",
        Order = 70,
        Collapsible = true,
        DefaultActive = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Header-only enhancement toggle; the workflow calls BuildPatch() directly when active.
    }

    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var patchId = $"{scope}minimax_sage_attention";

        builder.AddNode(patchId, node => node
            .Type("PathchSageAttentionKJ")
            .Title($"{scopeTitle}Patch SageAttention KJ")
            .InputRef("model", registry.GetRef($"{scope}model_output"))
            .Input("sage_attention", "auto")
            .Input("allow_compile", false));

        registry.Register($"{scope}model_output", patchId, 0);
    }
}
