using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that removes the background using the easy imageRemBg node from ComfyUI-Easy-Use.
/// Supports RMBG-2.0, RMBG-1.4, Inspyrenet, and BEN2 models via the rem_mode input.
/// Overwrites image_output in the node registry with the resulting IMAGE.
/// Conditional: Only builds when easy_rembg.IsActive is true.
/// </summary>
public class EasyRemBgFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "easy_rembg",
        Type = FragmentType.Enhancement,
        Title = "Remove Background (Easy RemBg)",
        Icon = "fa-solid fa-scissors",
        Order = 96,
        Collapsible = true,
        DefaultCollapsed = true,
        Component = "EasyRemBgForm",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "rem_mode",
                Label = "Model",
                Type = ParameterType.Select,
                Options = ["RMBG-2.0", "RMBG-1.4", "Inspyrenet", "BEN2"],
                DefaultValue = "RMBG-1.4"
            }
        ]
    };

    public class Parameters
    {
        public string RemMode { get; init; } = "RMBG-1.4";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        if (fragment?.IsActive != true)
            return;

        var p = new Parameters
        {
            RemMode = fragment.GetString("rem_mode", "RMBG-1.4") ?? "RMBG-1.4"
        };

        BuildInternal(builder, registry, p);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var imageRef = registry.GetRef("image_output");

        // Note: node input key is "images" (plural). image_output and save_prefix
        // are hardcoded to suppress the node's internal preview/save behaviour.
        builder.AddNode("easy_rembg", node => node
            .Type("easy imageRemBg")
            .Title("Remove Background (Easy RemBg)")
            .Input("rem_mode", p.RemMode)
            .Input("image_output", "Hide")
            .Input("save_prefix", "")
            .InputRef("images", imageRef));

        registry.Register("image_output", "easy_rembg", 0);
    }
}
