using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// OmniVoice TTS voice-clone fragment for LTX 2.3 talking-avatar workflows.
///
/// Pipeline:
///   <c>LoadAudio</c> (reference voice file) -&gt;
///   <c>OmniVoiceWhisperLoader</c> -&gt;
///   <c>OmniVoiceVoiceCloneTTS</c> (text widget + ref_audio + whisper_model
///       inputs) -&gt;
///   <c>TrimAudioDuration</c> (clamps the cloned voice to the target video
///       duration so the AV concat / audio VAE encode chain stays aligned).
///
/// Reads:
/// - <c>parameters.Sources["reference_audio"]</c> for the reference voice
///   sample.
/// - <c>parameters.GetFragment("ltx_video_settings").duration</c> for the
///   trim length (default 5 s).
/// - <c>parameters.GetFragment("prompts").positive</c> for the TTS text in
///   the default <see cref="Build(ComfyWorkflowBuilder, GenerationParameters, NodeRegistry, string, string)"/>
///   overload.
///
/// Registers: <c>{scope}audio_input</c> (AUDIO, post-trim) so the existing
/// <see cref="LtxAudioVaeEncodeFragment"/> picks it up unchanged.
/// </summary>
public class LtxOmniVoiceFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_omnivoice",
        Type = FragmentType.Input,
        Title = "LTX OmniVoice TTS",
        IsHidden = true
    };

    public class Parameters
    {
        // Reference audio file (LoadAudio "audio" widget).
        public string ReferenceAudioPath { get; set; } = "";

        // Spoken text - the script the cloned voice will say.
        public string Text { get; set; } = "";

        // Trim duration (seconds) applied to the OmniVoice output to match
        // the video length.
        public double DurationSeconds { get; set; } = 5.0;

        // OmniVoiceWhisperLoader widgets.
        public string WhisperModel { get; set; } = "whisper-small (auto-download)";
        public string WhisperDevice { get; set; } = "auto";
        public string WhisperDtype { get; set; } = "fp16";

        // OmniVoiceVoiceCloneTTS widgets (mirror upstream JSON defaults).
        public string OmniModel { get; set; } = "OmniVoice-bf16 (auto download)";
        public string RefText { get; set; } = ""; // empty => auto-transcribe via whisper
        public int Steps { get; set; } = 32;
        public double GuidanceScale { get; set; } = 2.0;
        public double TShift { get; set; } = 0.1;
        public double Speed { get; set; } = 1.0;
        public double Duration { get; set; } = 0.0; // 0 => auto from text + speed
        public string Device { get; set; } = "auto";
        public string Dtype { get; set; } = "auto";
        public string Attention { get; set; } = "auto";
        public long Seed { get; set; } = 0; // 0 => OmniVoice picks a random seed
        public double PositionTemperature { get; set; } = 5.0;
        public double ClassTemperature { get; set; } = 0.0;
        public double LayerPenaltyFactor { get; set; } = 5.0;
        public bool Denoise { get; set; } = true;
        public bool PreprocessPrompt { get; set; } = true;
        public bool PostprocessOutput { get; set; } = true;
        public bool KeepModelLoaded { get; set; } = false;
        public string Instruct { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var source = parameters.Sources?.GetValueOrDefault("reference_audio");
        var refPath = source?.Filename ?? source?.FilePath ?? "";

        var videoSettings = parameters.GetFragment("ltx_video_settings");
        var duration = videoSettings?.GetInt("duration", 5) ?? 5;

        var promptsData = parameters.GetFragment("prompts");
        var ttsText = promptsData?.GetString("positive", "") ?? "";

        BuildInternal(builder, registry, new Parameters
        {
            ReferenceAudioPath = refPath,
            Text = ttsText,
            DurationSeconds = duration
        }, scope, scopeTitle);
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
        var loadRefId = $"{scope}ltx_omnivoice_load_ref";
        var whisperId = $"{scope}ltx_omnivoice_whisper";
        var cloneId = $"{scope}ltx_omnivoice_clone";
        var trimId = $"{scope}ltx_omnivoice_trim";

        // 1. Load reference audio file (LoadAudio).
        builder.AddNode(loadRefId, node => node
            .Type("LoadAudio")
            .Title($"{scopeTitle}Load Reference Audio")
            .Input("audio", p.ReferenceAudioPath));

        // 2. OmniVoice Whisper loader.
        builder.AddNode(whisperId, node => node
            .Type("OmniVoiceWhisperLoader")
            .Title($"{scopeTitle}OmniVoice Whisper Loader")
            .Input("model", p.WhisperModel)
            .Input("device", p.WhisperDevice)
            .Input("dtype", p.WhisperDtype));

        // 3. OmniVoice Voice Clone TTS.
        builder.AddNode(cloneId, node => node
            .Type("OmniVoiceVoiceCloneTTS")
            .Title($"{scopeTitle}OmniVoice Voice Clone TTS")
            .Input("model", p.OmniModel)
            .Input("text", p.Text)
            .InputFromNode("ref_audio", loadRefId, 0)
            .Input("ref_text", p.RefText)
            .Input("steps", p.Steps)
            .Input("guidance_scale", p.GuidanceScale)
            .Input("t_shift", p.TShift)
            .Input("speed", p.Speed)
            .Input("duration", p.Duration)
            .Input("device", p.Device)
            .Input("dtype", p.Dtype)
            .Input("attention", p.Attention)
            .Input("seed", p.Seed)
            .Input("position_temperature", p.PositionTemperature)
            .Input("class_temperature", p.ClassTemperature)
            .Input("layer_penalty_factor", p.LayerPenaltyFactor)
            .Input("denoise", p.Denoise)
            .Input("preprocess_prompt", p.PreprocessPrompt)
            .Input("postprocess_output", p.PostprocessOutput)
            .Input("keep_model_loaded", p.KeepModelLoaded)
            .Input("instruct", p.Instruct)
            .InputFromNode("whisper_model", whisperId, 0));

        // 4. Trim cloned audio to the video duration so the AV concat /
        //    audio VAE encode chain stays aligned.
        builder.AddNode(trimId, node => node
            .Type("TrimAudioDuration")
            .Title($"{scopeTitle}Trim Cloned Audio")
            .InputFromNode("audio", cloneId, 0)
            .Input("start_time", 0.0)
            .Input("duration", p.DurationSeconds));

        registry.Register($"{scope}audio_input", trimId, 0);
    }
}
