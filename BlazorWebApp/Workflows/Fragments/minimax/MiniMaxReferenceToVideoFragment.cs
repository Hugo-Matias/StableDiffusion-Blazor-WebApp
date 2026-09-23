using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Core MiniMaxH3ReferenceToVideo node. Conditions on up to 9 reference images, 1 reference
/// video (optionally paired with that video's own audio), and 1 standalone reference audio clip.
/// Encodes the prompt directly from a raw STRING (no CLIPTextEncode - the node's CLIP does its
/// own encoding). Registers positive_output (CONDITIONING) and latent_output (LATENT).
/// </summary>
public class MiniMaxReferenceToVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_reference_to_video",
        Type = FragmentType.Conditioning,
        Title = "MiniMax H3 Reference To Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string Prompt { get; set; } = "";
        public int Width { get; set; } = 1344;
        public int Height { get; set; } = 768;
        public int Length { get; set; } = 124;
        public string RefImageSize { get; set; } = "match";
        /// <summary>Registry keys for loaded reference images, in order (wired to ref_image_0, ref_image_1, ...).</summary>
        public List<string> RefImageInputNames { get; set; } = [];
        /// <summary>Registry key for the loaded reference video frames, or null when no video was supplied.</summary>
        public string? RefVideoInputName { get; set; }
        /// <summary>Registry key for the reference video's own audio track, or null when not included.</summary>
        public string? RefVideoAudioInputName { get; set; }
        /// <summary>Registry key for the standalone reference audio, or null when no audio was supplied.</summary>
        public string? RefAudioInputName { get; set; }
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
        var nodeId = $"{scope}minimax_reference_to_video";

        builder.AddNode(nodeId, node =>
        {
            node.Type("MiniMaxH3ReferenceToVideo")
                .Title($"{scopeTitle}MiniMax H3 Reference To Video")
                .InputRef("clip", registry.GetRef($"{scope}clip_output"))
                .InputRef("vae", registry.GetRef($"{scope}vae_output"))
                .InputRef("audio_vae", registry.GetRef($"{scope}audio_vae_output"))
                .Input("prompt", p.Prompt)
                .Input("width", p.Width)
                .Input("height", p.Height)
                .Input("length", p.Length)
                .Input("ref_image_size", p.RefImageSize);

            // Autogrow slots are wired as flat "group.slot" keys (e.g. "ref_images.ref_image_0") -
            // ComfyUI's build_nested_inputs() splits on the dot server-side to regroup them into
            // the dict kwargs (ref_images=, ref_videos=, ...) execute() actually expects.
            for (var i = 0; i < p.RefImageInputNames.Count; i++)
                node.InputRef($"ref_images.ref_image_{i}", registry.GetRef(p.RefImageInputNames[i]));

            if (!string.IsNullOrEmpty(p.RefVideoInputName))
                node.InputRef("ref_videos.ref_video_0", registry.GetRef(p.RefVideoInputName));

            if (!string.IsNullOrEmpty(p.RefVideoAudioInputName))
                node.InputRef("ref_video_audios.ref_video_audio_0", registry.GetRef(p.RefVideoAudioInputName));

            if (!string.IsNullOrEmpty(p.RefAudioInputName))
                node.InputRef("ref_audios.ref_audio_0", registry.GetRef(p.RefAudioInputName));
        });

        registry.Register($"{scope}positive_output", nodeId, 0);
        registry.Register($"{scope}latent_output", nodeId, 1);
    }
}
