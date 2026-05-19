using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class VaeDecodeAudioFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "vae_decode_audio",
        Type = FragmentType.Output,
        Title = "VAE Decode Audio",
        IsHidden = true
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var nodeId = $"{scope}vae_decode_audio";
        builder.AddNode(nodeId, node => node
            .Type("VAEDecodeAudio")
            .Title($"{scopeTitle}VAEDecodeAudio")
            .InputRef("samples", registry.GetRef($"{scope}latent_output"))
            .InputRef("vae", registry.GetRef($"{scope}vae_output")));

        registry.Register($"{scope}audio_output", nodeId, 0);
        registry.Register(new AudioOutput(nodeId, 0));
    }
}