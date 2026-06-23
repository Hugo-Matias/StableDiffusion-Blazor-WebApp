using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Loads a user-supplied audio track and trims it to the target duration.
/// Pipeline: <c>LoadAudio</c> -> <c>TrimAudioDuration</c>.
///
/// Reads the audio filename from <c>parameters.Sources["audio_track"]</c>
/// (falling back to <c>FilePath</c>) and computes the duration in seconds
/// from <c>frames / fps</c>.
///
/// Registers: <c>{scope}audio_input</c>.
/// </summary>
public class LtxLoadAudioFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_audio",
        Type = FragmentType.Input,
        Title = "LTX Load Audio",
        IsHidden = true
    };

    public class Parameters
    {
        public string AudioPath { get; set; } = "";
        public double DurationSeconds { get; set; } = 5.0;
        public double StartTime { get; set; } = 0.0;
        public string SourceKey { get; set; } = "audio_track";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var source = parameters.Sources?.GetValueOrDefault("audio_track");
        var path = source?.Filename ?? source?.FilePath ?? "";

        var videoSettings = parameters.GetFragment("ltx_video_settings");
        var duration = videoSettings?.GetInt("duration", 5) ?? 5;
        var fps = videoSettings?.GetInt("frame_rate", 25) ?? 25;
        var seconds = (double)duration; // duration is already in seconds in LtxVideoSettings
        // Frame count is duration * fps + 1; trimming by seconds keeps the audio aligned with video.
        BuildInternal(builder, registry, new Parameters
        {
            AudioPath = path,
            DurationSeconds = seconds,
            StartTime = 0.0
        }, scope, scopeTitle);
        _ = fps;
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
        var loadId = $"{scope}ltx_load_audio";
        var trimId = $"{scope}ltx_trim_audio";

        builder.AddNode(loadId, node => node
            .Type("LoadAudio")
            .Title($"{scopeTitle}Load Audio")
            .Input("audio", p.AudioPath));

        builder.AddNode(trimId, node => node
            .Type("TrimAudioDuration")
            .Title($"{scopeTitle}Trim Audio Duration")
            .InputFromNode("audio", loadId, 0)
            .Input("start_time", p.StartTime)
            .Input("duration", p.DurationSeconds));

        registry.Register($"{scope}audio_input", trimId, 0);
    }
}
