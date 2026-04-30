using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Mel-Band RoFormer vocal isolation enhancement for LTX 2.3 custom-audio workflows.
/// Header-only collapsible enhancement (no parameters, no form).
/// When active, the workflow class calls <see cref="BuildPatch"/> after the audio is loaded
/// and trimmed. The patch chains <c>MelBandRoFormerModelLoader</c> + <c>MelBandRoFormerSampler</c>
/// onto <c>{scope}audio_input</c> and re-registers the registry key to point at the
/// vocals-only stem so downstream encoders use it transparently.
/// </summary>
public class LtxMelSeparationEnhancementFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_mel_separation",
        Type = FragmentType.Enhancement,
        Title = "Isolate Vocals (Mel-Band RoFormer)",
        Icon = "fa-solid fa-microphone",
        Order = 73,
        Collapsible = true,
        DefaultCollapsed = true
    };

    /// <summary>No-op. The patch is invoked explicitly by the workflow class.</summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    /// <summary>
    /// Loads the Mel-Band RoFormer model and runs the sampler on <c>{scope}audio_input</c>,
    /// then re-registers <c>{scope}audio_input</c> to the vocals-only stem (output index 0).
    /// </summary>
    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "",
        string modelName = "MelBandRoformer\\MelBandRoformer_fp16.safetensors")
    {
        var loaderId = $"{scope}ltx_mel_loader";
        var samplerId = $"{scope}ltx_mel_sampler";
        var audioRef = registry.GetRef($"{scope}audio_input");

        builder.AddNode(loaderId, node => node
            .Type("MelBandRoFormerModelLoader")
            .Title($"{scopeTitle}Mel-Band RoFormer Model")
            .Input("model", modelName));

        builder.AddNode(samplerId, node => node
            .Type("MelBandRoFormerSampler")
            .Title($"{scopeTitle}Mel-Band RoFormer Sampler")
            .InputFromNode("model", loaderId, 0)
            .InputRef("audio", audioRef));

        // Output 0 is the vocals stem; output 1 is the instrumental stem.
        registry.Register($"{scope}audio_input", samplerId, 0);
    }
}
