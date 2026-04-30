using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Encodes a real audio track into an LTX audio latent and applies a zero noise mask so
/// the sampler treats it as a fixed conditioning rather than something to denoise.
/// Pipeline: <c>LTXVAudioVAEEncode</c> -> <c>SolidMask</c> -> <c>SetLatentNoiseMask</c>.
///
/// Reads: <c>{scope}audio_input</c>, <c>{scope}audio_vae_output</c>.
/// Registers: <c>{scope}audio_latent</c> (overrides the empty audio latent so the
/// downstream <see cref="LtxConcatAVLatentFragment"/> picks up the encoded path).
/// </summary>
public class LtxAudioVaeEncodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_audio_vae_encode",
        Type = FragmentType.Latent,
        Title = "LTX Audio VAE Encode",
        IsHidden = true
    };

    public class Parameters
    {
        public int MaskWidth { get; set; } = 512;
        public int MaskHeight { get; set; } = 512;
        public double MaskValue { get; set; } = 0.0;
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

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var encodeId = $"{scope}ltx_audio_vae_encode";
        var maskId = $"{scope}ltx_audio_mask";
        var setMaskId = $"{scope}ltx_audio_set_mask";

        var audioRef = registry.GetRef($"{scope}audio_input");
        var audioVaeRef = registry.GetRef($"{scope}audio_vae_output");

        builder.AddNode(encodeId, node => node
            .Type("LTXVAudioVAEEncode")
            .Title($"{scopeTitle}LTXV Audio VAE Encode")
            .InputRef("audio", audioRef)
            .InputRef("audio_vae", audioVaeRef));

        builder.AddNode(maskId, node => node
            .Type("SolidMask")
            .Title($"{scopeTitle}Solid Mask")
            .Input("value", p.MaskValue)
            .Input("width", p.MaskWidth)
            .Input("height", p.MaskHeight));

        builder.AddNode(setMaskId, node => node
            .Type("SetLatentNoiseMask")
            .Title($"{scopeTitle}Set Latent Noise Mask")
            .InputFromNode("samples", encodeId, 0)
            .InputFromNode("mask", maskId, 0));

        registry.Register($"{scope}audio_latent", setMaskId, 0);
    }
}
