using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Mirrors source workflow nodes 32/33: MiniMaxH3ImageConditioning (raw prompt string, no CLIPTextEncode)
/// plus ConditioningZeroOut to derive the negative conditioning KSampler requires. The model node's
/// COMFY_AUTOGROW_V3 image input is wired as flat "images.image_{n}" keys, one per prepared source
/// image; width/height come from the primary (first) image's GetImageSize outputs.
/// </summary>
public class MiniMaxH3ImageConditioningFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_h3_image_conditioning",
        Type = FragmentType.Conditioning,
        Title = "H3 Image Conditioning",
        IsHidden = true
    };

    public class Parameters
    {
        public string Prompt { get; set; } = "";
        public string WidthInputName { get; set; } = "";
        public string HeightInputName { get; set; } = "";
        public IList<string> ImageInputNames { get; set; } = [];
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        // UI-less utility fragment; the workflow calls the explicit Parameters overload directly.
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope = "", string scopeTitle = "")
    {
        var condNodeId = $"{scope}minimax_h3_image_conditioning";
        var zeroOutId = $"{scope}minimax_h3_image_zero_out";

        // Dynamic autogrow inputs require building the node outside AddNode's configure action.
        var condBuilder = new NodeBuilder(condNodeId)
            .Type("MiniMaxH3ImageConditioning")
            .Title($"{scopeTitle}H3 Image Conditioning")
            .Input("prompt", p.Prompt)
            .InputFromRegistry("clip", registry, $"{scope}clip_output")
            .InputFromRegistry("vae", registry, $"{scope}vae_output")
            .InputFromRegistry("width", registry, p.WidthInputName)
            .InputFromRegistry("height", registry, p.HeightInputName);

        for (var i = 0; i < p.ImageInputNames.Count; i++)
        {
            condBuilder.InputFromRegistry($"images.image_{i + 1}", registry, p.ImageInputNames[i]);
        }

        builder.AddNode(condNodeId, condBuilder.Build());

        builder.AddNode(zeroOutId, node => node
            .Type("ConditioningZeroOut")
            .Title($"{scopeTitle}Zero Out Conditioning")
            .InputRef("conditioning", (condNodeId, 0)));

        registry.Register($"{scope}positive_output", condNodeId, 0);
        registry.Register($"{scope}negative_output", zeroOutId, 0);
        registry.Register($"{scope}latent_output", condNodeId, 1);
    }
}
