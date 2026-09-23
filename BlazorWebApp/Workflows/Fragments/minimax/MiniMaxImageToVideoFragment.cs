using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Core MiniMaxH3ImageToVideo node. Encodes the prompt directly from a raw STRING (no
/// CLIPTextEncode - the node's CLIP does its own encoding), and optionally conditions on
/// first_frame/last_frame guiding images. Falls back to text-to-video (t2va) when neither
/// frame is supplied. Registers positive_output (CONDITIONING) and latent_output (LATENT).
/// </summary>
public class MiniMaxImageToVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_image_to_video",
        Type = FragmentType.Conditioning,
        Title = "MiniMax H3 Image To Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string Prompt { get; set; } = "";
        public int Width { get; set; } = 1344;
        public int Height { get; set; } = 768;
        public int Length { get; set; } = 124;
        /// <summary>
        /// Registry key for the loaded first-frame image, or null when no first frame was supplied.
        /// </summary>
        public string? FirstFrameInputName { get; set; }
        /// <summary>
        /// Registry key for the loaded last-frame image, or null when no last frame was supplied.
        /// </summary>
        public string? LastFrameInputName { get; set; }
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // Core pipeline fragment; the workflow calls the explicit Parameters overload directly.
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}minimax_image_to_video";

        builder.AddNode(nodeId, node =>
        {
            node.Type("MiniMaxH3ImageToVideo")
                .Title($"{scopeTitle}MiniMax H3 Image To Video")
                .InputRef("clip", registry.GetRef($"{scope}clip_output"))
                .InputRef("vae", registry.GetRef($"{scope}vae_output"))
                .Input("prompt", p.Prompt)
                .Input("width", p.Width)
                .Input("height", p.Height)
                .Input("length", p.Length);

            if (!string.IsNullOrEmpty(p.FirstFrameInputName))
                node.InputRef("first_frame", registry.GetRef(p.FirstFrameInputName));

            if (!string.IsNullOrEmpty(p.LastFrameInputName))
                node.InputRef("last_frame", registry.GetRef(p.LastFrameInputName));
        });

        registry.Register($"{scope}positive_output", nodeId, 0);
        registry.Register($"{scope}latent_output", nodeId, 1);
    }
}
