using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class AceStepEmptyAudioLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ace_step_empty_audio_latent",
        Type = FragmentType.Latent,
        Title = "Audio Latent",
        IsHidden = true
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var music = parameters.GetFragment(AceStepMusicConditioningFragment.Id);
        var duration = music?.GetInt("duration", 120) ?? 120;
        var batchSize = music?.GetInt("batch_size", 1) ?? 1;
        var nodeId = $"{scope}ace_step_empty_audio_latent";

        builder.AddNode(nodeId, node => node
            .Type("EmptyAceStep1.5LatentAudio")
            .Title($"{scopeTitle}EmptyAceStep1.5LatentAudio")
            .Input("seconds", duration)
            .Input("batch_size", batchSize));

        registry.Register($"{scope}latent_output", nodeId, 0);
    }
}