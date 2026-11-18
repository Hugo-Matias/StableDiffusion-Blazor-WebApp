using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a single image using LoadImage.
/// Registers image_input output. No resize - use ResizeImageFragment for that.
/// </summary>
public class LoadImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_image",
        Type = FragmentType.Input,
        Title = "Load Image",
        IsHidden = true
    };

    public class Parameters
    {
        public string Image { get; set; } = "";
        public string OutputName { get; set; } = "image_input";
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
            Image = fragment?.GetString("image", "") ?? ""
        }, scope, scopeTitle);
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
        var nodeId = $"{scope}load_image";

        builder.AddNode(nodeId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}Load Image")
            .Input("image", p.Image));

        registry.Register($"{scope}{p.OutputName}", nodeId, 0);
    }
}
