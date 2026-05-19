using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class SaveAudioMp3Fragment : IFragmentBuilder
{
    public const string DefaultFilenamePrefix = "Audio/ComfyUI";

    public FragmentMetadata Metadata => new()
    {
        Id = "save_audio_mp3",
        Type = FragmentType.Output,
        Title = "Save Audio",
        Component = "SaveAudioForm",
        Icon = "fa-solid fa-file-audio",
        Order = 90,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter { Name = "quality", Label = "MP3 Quality", Type = ParameterType.Select, Options = ["V0", "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8", "V9"], DefaultValue = "V0" }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var nodeId = $"{scope}save_audio_mp3";

        builder.AddNode(nodeId, node => node
            .Type("SaveAudioMP3")
            .Title($"{scopeTitle}SaveAudioMP3")
            .InputRef("audio", registry.GetRef($"{scope}audio_output"))
            .Input("filename_prefix", DefaultFilenamePrefix)
            .Input("quality", fragment?.GetString("quality", "V0") ?? "V0"));
    }
}