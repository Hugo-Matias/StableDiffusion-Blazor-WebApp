using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads CLIP and VAE for Wan Img2Vid workflows.
/// Creates CLIPLoader (type=wan, device=cpu) and VAELoader nodes.
/// Registers clip_output and vae_output.
/// </summary>
public class LoadClipVaeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_clip_vae",
        Type = FragmentType.Loader,
        Title = "Load CLIP & VAE",
        IsHidden = true
    };

    public class Parameters
    {
        public string ClipName { get; set; } = "";
        public string ClipType { get; set; } = "wan";
        public string ClipDevice { get; set; } = "cpu";
        public string VaeName { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters
        {
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? ""
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
        var clipId = $"{scope}clip_loader";
        var vaeId = $"{scope}vae_loader";

        builder.AddNode(clipId, node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", p.ClipType)
            .Input("device", p.ClipDevice));

        builder.AddNode(vaeId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load VAE")
            .Input("vae_name", p.VaeName));

        registry.Register($"{scope}clip_output", clipId, 0);
        registry.Register($"{scope}vae_output", vaeId, 0);
    }
}
