using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// LTX 2.3 SageAttention performance enhancement.
/// Header-only collapsible enhancement (no parameters, no form). Default OFF
/// because SageAttention requires a working install on the ComfyUI side.
/// When active, the workflow class calls <see cref="BuildPatch"/> after the loader.
/// Patch chain (each output feeds the next):
///   PathchSageAttentionKJ -> LTX2AttentionTunerPatch -> LTX2MemoryEfficientSageAttentionPatch
/// Re-registers <c>{scope}model_output</c> at each step.
/// </summary>
public class LtxSageAttentionEnhancementFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_sage_attention",
        Type = FragmentType.Enhancement,
        Title = "SageAttention (performance)",
        Icon = "fa-solid fa-bolt",
        Order = 71,
        Collapsible = true,
        DefaultCollapsed = true
    };

    /// <summary>No-op. The workflow class invokes <see cref="BuildPatch"/> when active.</summary>
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
        var sageId = $"{scope}ltx_sage_kj";
        var tunerId = $"{scope}ltx_attention_tuner";
        var memEffId = $"{scope}ltx_mem_efficient_sage";

        var modelRef = registry.GetRef($"{scope}model_output");

        builder.AddNode(sageId, node => node
            .Type("PathchSageAttentionKJ")
            .Title($"{scopeTitle}Patch SageAttention KJ")
            .Input("sage_attention", "auto")
            .InputRef("model", modelRef));
        registry.Register($"{scope}model_output", sageId, 0);

        builder.AddNode(tunerId, node => node
            .Type("LTX2AttentionTunerPatch")
            .Title($"{scopeTitle}LTX2 Attention Tuner Patch")
            .InputFromNode("model", sageId, 0));
        registry.Register($"{scope}model_output", tunerId, 0);

        builder.AddNode(memEffId, node => node
            .Type("LTX2MemoryEfficientSageAttentionPatch")
            .Title($"{scopeTitle}LTX2 Memory Efficient SageAttention Patch")
            .InputFromNode("model", tunerId, 0));
        registry.Register($"{scope}model_output", memEffId, 0);
    }
}
