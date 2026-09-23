using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Loads a standalone reference audio clip for MiniMax H3 Reference-to-Video. Only inserts a
/// TrimAudioDuration node when the source's trim window was actually touched - TrimAudioDuration
/// has no "untrimmed" sentinel (duration=0 would clip to silence), so an untouched clip is wired
/// straight from LoadAudio.
/// </summary>
public class MiniMaxLoadAudioFragment : IFragmentBuilder
{
    /// <summary>Sentinel "to end" duration used when the user set a start offset but no explicit trim length.</summary>
    private const double UntrimmedDuration = 3600;

    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_load_audio",
        Type = FragmentType.Input,
        Title = "MiniMax Reference Audio",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodePrefix { get; set; } = "minimax_audio";
        public string Title { get; set; } = "Reference Audio";
        public string AudioPath { get; set; } = "";
        public double TrimStart { get; set; } = 0;
        public double TrimDuration { get; set; } = 0;
        public string OutputName { get; set; } = "minimax_audio_output";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // UI-less utility fragment; the workflow calls the explicit Parameters overload directly.
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var loadId = $"{scope}{p.NodePrefix}_load_audio";

        builder.AddNode(loadId, node => node
            .Type("LoadAudio")
            .Title($"{scopeTitle}{p.Title}")
            .Input("audio", p.AudioPath));

        if (p.TrimStart > 0 || p.TrimDuration > 0)
        {
            var trimId = $"{scope}{p.NodePrefix}_trim_audio";
            builder.AddNode(trimId, node => node
                .Type("TrimAudioDuration")
                .Title($"{scopeTitle}Trim {p.Title}")
                .InputFromNode("audio", loadId, 0)
                .Input("start_index", p.TrimStart)
                .Input("duration", p.TrimDuration > 0 ? p.TrimDuration : UntrimmedDuration));

            registry.Register($"{scope}{p.OutputName}", trimId, 0);
            return;
        }

        registry.Register($"{scope}{p.OutputName}", loadId, 0);
    }
}
