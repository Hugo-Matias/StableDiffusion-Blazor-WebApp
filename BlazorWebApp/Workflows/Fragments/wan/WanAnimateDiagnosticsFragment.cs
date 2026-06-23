using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Header-only toggle that enables the optional Wan Animate diagnostic collage output.
/// </summary>
public class WanAnimateDiagnosticsFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_animate_diagnostics";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Enhancement,
        Title = "Diagnostic Collage",
        Icon = "fa-solid fa-table-cells-large",
        Order = 130,
        Collapsible = true,
        DefaultActive = false,
        Description = "Optionally saves a side-by-side diagnostic video with the generated frames and preprocessing previews."
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }
}