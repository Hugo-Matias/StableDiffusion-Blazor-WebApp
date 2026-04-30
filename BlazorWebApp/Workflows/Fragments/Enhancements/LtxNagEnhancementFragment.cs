using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// LTX 2.3 Normalized Attention Guidance (NAG) enhancement.
/// Header-only collapsible enhancement (no parameters, no form).
/// When active, the workflow class calls <see cref="BuildPatch"/> right after the loader.
/// The patch chains an <c>LTX2_NAG</c> node onto <c>{scope}model_output</c> and re-registers
/// the registry key to point at the patched model so downstream fragments use it transparently.
/// </summary>
public class LtxNagEnhancementFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_nag",
        Type = FragmentType.Enhancement,
        Title = "Normalized Attention Guidance",
        Icon = "fa-solid fa-bullseye",
        Order = 70,
        Collapsible = true,
        DefaultCollapsed = true,
        DefaultActive = true
    };

    /// <summary>No-op. NAG is a workflow-driven model patch invoked via <see cref="BuildPatch"/>.</summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    /// <summary>
    /// Patches <c>{scope}model_output</c> with <c>LTX2_NAG</c>.
    /// The fragment's negative prompt is reused as the NAG negative input.
    /// </summary>
    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}ltx_nag_patch";
        var modelRef = registry.GetRef($"{scope}model_output");

        builder.AddNode(nodeId, node => node
            .Type("LTX2_NAG")
            .Title($"{scopeTitle}LTX2 NAG")
            .InputRef("model", modelRef));

        registry.Register($"{scope}model_output", nodeId, 0);
    }
}
