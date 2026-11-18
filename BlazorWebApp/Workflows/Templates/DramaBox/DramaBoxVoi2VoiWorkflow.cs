using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Audio;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.DramaBox;

public class DramaBoxVoi2VoiWorkflow : IWorkflowBuilder
{
    private readonly DramaBoxAudioReferenceFragment _reference = new();
    private readonly MelBandVoiceIsolationFragment _voiceIsolation = new();
    private readonly DramaBoxSpeechFragment _speech = new();
    private readonly SaveAudioMp3Fragment _save = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "DramaBox Voice Clone",
        Description = "Voice-to-voice text-to-speech with a reference audio clip. Supply a clean reference voice, trim it to the useful section, then write the dialogue text to synthesize with DramaBox.",
        Base = Data.Enums.ModelBase.DramaBox,
        Mode = ModeType.Voi2Voi,
        Sources =
        [
            new WorkflowSource
            {
                Id = DramaBoxAudioReferenceFragment.SourceKey,
                Label = "Reference Audio",
                Type = SourceType.Audio,
                Required = true,
                Parameter = "audio"
            }
        ],
        CompatibleResourceBaseModels = ["DramaBox"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _reference;
        yield return _speech;
        yield return _save;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        _reference.Build(builder, parameters, registry);
        _voiceIsolation.Build(builder, parameters, registry);
        _speech.Build(builder, parameters, registry);
        _save.Build(builder, parameters, registry);

        return builder.ToComfyWorkflow(registry);
    }
}