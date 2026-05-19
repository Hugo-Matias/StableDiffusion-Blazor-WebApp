using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class AceStepMusicConditioningFragment : IFragmentBuilder
{
    public const string Id = "ace_step_music";

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = FragmentType.Settings,
        Title = "Music Prompt",
        Component = "AceStepMusicForm",
        Icon = "fa-solid fa-music",
        Order = 10,
        Collapsible = false,
        Parameters =
        [
            TextArea("tags", "Tags", "Neo-Soul, soulful vocals, steady groove, warm keys, polished studio mix"),
            TextArea("lyrics", "Lyrics", ""),
            Number("seed", "Seed", -1, null, 1, 31L),
            Number("bpm", "BPM", 40, 300, 1, 190),
            Number("duration", "Duration", 1, 240, 1, 120),
            Select("timesignature", "Time Signature", ["2", "3", "4", "5", "6", "7", "8"], "4"),
            Select("language", "Language", ["en", "zh", "ja", "ko"], "en"),
            Select("keyscale", "Key", ["C major", "C minor", "C# major", "C# minor", "D major", "D minor", "D# major", "D# minor", "E major", "E minor", "F major", "F minor", "F# major", "F# minor", "G major", "G minor", "G# major", "G# minor", "A major", "A minor", "A# major", "A# minor", "B major", "B minor"], "E minor"),
            Checkbox("generate_audio_codes", "Generate Audio Codes", true),
            Slider("cfg_scale", "Text CFG", 0, 10, 0.1, 2.0),
            Slider("temperature", "Temperature", 0, 2, 0.01, 0.85),
            Slider("top_p", "Top P", 0, 1, 0.01, 0.9),
            Number("top_k", "Top K", 0, 200, 1, 0),
            Slider("min_p", "Min P", 0, 1, 0.01, 0.0),
            Number("batch_size", "Batch Size", 1, 16, 1, 1)
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        var clipRef = registry.GetRef($"{scope}clip_output");

        var encodeId = $"{scope}ace_step_text_encode";
        builder.AddNode(encodeId, node => node
            .Type("TextEncodeAceStepAudio1.5")
            .Title($"{scopeTitle}TextEncodeAceStepAudio1.5")
            .InputRef("clip", clipRef)
            .Input("tags", fragment?.GetString("tags", "") ?? "")
            .Input("lyrics", fragment?.GetString("lyrics", "") ?? "")
            .Input("seed", fragment?.GetLong("seed", 31) ?? 31)
            .Input("bpm", fragment?.GetInt("bpm", 190) ?? 190)
            .Input("duration", fragment?.GetInt("duration", 120) ?? 120)
            .Input("timesignature", fragment?.GetString("timesignature", "4") ?? "4")
            .Input("language", fragment?.GetString("language", "en") ?? "en")
            .Input("keyscale", fragment?.GetString("keyscale", "E minor") ?? "E minor")
            .Input("generate_audio_codes", fragment?.GetValueOrDefault("generate_audio_codes", true) ?? true)
            .Input("cfg_scale", fragment?.GetDouble("cfg_scale", 2.0) ?? 2.0)
            .Input("temperature", fragment?.GetDouble("temperature", 0.85) ?? 0.85)
            .Input("top_p", fragment?.GetDouble("top_p", 0.9) ?? 0.9)
            .Input("top_k", fragment?.GetInt("top_k", 0) ?? 0)
            .Input("min_p", fragment?.GetDouble("min_p", 0.0) ?? 0.0));

        var zeroOutId = $"{scope}ace_step_conditioning_zero_out";
        builder.AddNode(zeroOutId, node => node
            .Type("ConditioningZeroOut")
            .Title($"{scopeTitle}ConditioningZeroOut")
            .InputFromNode("conditioning", encodeId, 0));

        registry.Register($"{scope}positive_output", encodeId, 0);
        registry.Register($"{scope}negative_output", zeroOutId, 0);
    }

    private static FragmentParameter TextArea(string name, string label, string defaultValue) => new() { Name = name, Label = label, Type = ParameterType.TextArea, DefaultValue = defaultValue };
    private static FragmentParameter Number(string name, string label, double? min, double? max, double step, object defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Number, Min = min, Max = max, Step = step, DefaultValue = defaultValue };
    private static FragmentParameter Slider(string name, string label, double min, double max, double step, object defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Slider, Min = min, Max = max, Step = step, DefaultValue = defaultValue };
    private static FragmentParameter Select(string name, string label, IEnumerable<string> options, string defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Select, Options = options, DefaultValue = defaultValue };
    private static FragmentParameter Checkbox(string name, string label, bool defaultValue) => new() { Name = name, Label = label, Type = ParameterType.Checkbox, DefaultValue = defaultValue };
}