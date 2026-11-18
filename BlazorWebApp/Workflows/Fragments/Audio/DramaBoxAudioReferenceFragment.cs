using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class DramaBoxAudioReferenceFragment : IFragmentBuilder
{
    public const string Id = "dramabox_reference_audio";
    public const string SourceKey = "reference_audio";

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = FragmentType.Settings,
        Title = "Reference Audio",
        Component = "AudioReferenceForm",
        Icon = "fa-solid fa-microphone-lines",
        Order = 10,
        Collapsible = false,
        Parameters = []
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var source = parameters.Sources.GetValueOrDefault(SourceKey);
        var audioPath = source?.Filename ?? source?.FilePath ?? source?.Data ?? "";
        var fragment = parameters.GetFragment(Id);
        var loadId = $"{scope}dramabox_load_audio";
        var trimId = $"{scope}dramabox_trim_audio";

        // Trim values come from AudioOptions on the source asset (edited in SourceItem trim panel).
        var startIndex = source?.AudioOptions?.TrimStart ?? 0.0;
        var duration = source?.AudioOptions?.TrimDuration > 0
            ? source!.AudioOptions!.TrimDuration
            : 15.0;

        builder.AddNode(loadId, node => node
            .Type("LoadAudio")
            .Title($"{scopeTitle}Reference Audio (Input)")
            .Input("audio", audioPath));

        builder.AddNode(trimId, node => node
            .Type("TrimAudioDuration")
            .Title($"{scopeTitle}Trim Audio Duration")
            .InputFromNode("audio", loadId, 0)
            .Input("start_index", startIndex)
            .Input("duration", duration));

        registry.Register($"{scope}reference_audio", trimId, 0);
    }
}