using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class DramaBoxSpeechFragment : IFragmentBuilder
{
    public const string Id = "dramabox_speech";

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = FragmentType.Settings,
        Title = "Speech Prompt",
        Component = "DramaBoxSpeechForm",
        Icon = "fa-solid fa-comment-dots",
        Order = 20,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter { Name = "text", Label = "Text", Type = ParameterType.TextArea, DefaultValue = "Prompt" },
            Slider("cfg_scale", "CFG Scale", 0, 10, 0.1, 3.5),
            Slider("stg_scale", "STG Scale", 0, 10, 0.1, 1.5),
            Number("seed", "Seed", -1, null, 1, 3L),
            Slider("duration_multiplier", "Duration Multiplier", 0.25, 3, 0.05, 1.2)
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        var textId = $"{scope}dramabox_prompt";
        var ttsId = $"{scope}dramabox_tts";

        builder.AddNode(textId, node => node
            .Type("Text Multiline")
            .Title($"{scopeTitle}Prompt")
            .Input("text", fragment?.GetString("text", "Prompt") ?? "Prompt"));

        builder.AddNode(ttsId, node => node
            .Type("DramaBoxTTS")
            .Title($"{scopeTitle}DramaBox TTS")
            .InputFromNode("text", textId, 0)
            .Input("cfg_scale", fragment?.GetDouble("cfg_scale", 3.5) ?? 3.5)
            .Input("stg_scale", fragment?.GetDouble("stg_scale", 1.5) ?? 1.5)
            .Input("seed", fragment?.GetLong("seed", 3) ?? 3)
            .Input("duration_multiplier", fragment?.GetDouble("duration_multiplier", 1.2) ?? 1.2)
            .InputRef("voice_sample", registry.GetRef($"{scope}voice_sample")));

        registry.Register($"{scope}audio_output", ttsId, 0);
        registry.Register(new AudioOutput(ttsId, 0));
    }

    private static FragmentParameter Number(string name, string label, double? min, double? max, double step, object defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Number, Min = min, Max = max, Step = step, DefaultValue = defaultValue };
    private static FragmentParameter Slider(string name, string label, double min, double max, double step, object defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Slider, Min = min, Max = max, Step = step, DefaultValue = defaultValue };
}