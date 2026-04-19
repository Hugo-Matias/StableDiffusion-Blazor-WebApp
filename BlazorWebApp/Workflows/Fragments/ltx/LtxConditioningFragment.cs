using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that creates LTX conditioning from prompts.
/// Build: LTXVConditioning (wraps prompts with frame_rate).
/// BuildCropped: LTXVCropGuides (crops conditioning to match upsampled latent).
/// Registers: ltx_positive_output, ltx_negative_output (Build),
///            ltx_cropped_positive_output, ltx_cropped_negative_output (BuildCropped).
/// </summary>
public class LtxConditioningFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_conditioning",
        Type = FragmentType.Conditioning,
        Title = "LTX Conditioning",
        IsHidden = true
    };

    public class Parameters
    {
        public int FrameRate { get; set; } = 25;
    }

    public class CropParameters
    {
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string LatentInputName { get; set; } = "pass1_video_latent";
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

    public void BuildCropped(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        CropParameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildCroppedInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var nodeId = $"{scope}ltx_conditioning";
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");

        builder.AddNode(nodeId, node => node
            .Type("LTXVConditioning")
            .Title($"{scopeTitle}LTXVConditioning")
            .Input("frame_rate", p.FrameRate)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef));

        registry.Register($"{scope}ltx_positive_output", nodeId, 0);
        registry.Register($"{scope}ltx_negative_output", nodeId, 1);
    }

    private static void BuildCroppedInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        CropParameters p,
        string scope,
        string scopeTitle)
    {
        var nodeId = $"{scope}ltx_crop_guides";
        var positiveRef = registry.GetRef(p.PositiveInputName);
        var negativeRef = registry.GetRef(p.NegativeInputName);
        var latentRef = registry.GetRef(p.LatentInputName);

        builder.AddNode(nodeId, node => node
            .Type("LTXVCropGuides")
            .Title($"{scopeTitle}LTXVCropGuides")
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("latent", latentRef));

        registry.Register($"{scope}ltx_cropped_positive_output", nodeId, 0);
        registry.Register($"{scope}ltx_cropped_negative_output", nodeId, 1);
    }
}
