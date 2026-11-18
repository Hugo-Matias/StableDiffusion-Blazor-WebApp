using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class MelBandVoiceIsolationFragment : IFragmentBuilder
{
    public const string AssetKey = "MelBandModel";
    public const string DefaultModel = "MelBandRoformer_fp16.safetensors";

    public FragmentMetadata Metadata => new()
    {
        Id = "melband_voice_isolation",
        Type = FragmentType.Utility,
        Title = "Mel-Band Voice Isolation",
        IsHidden = true
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var modelName = parameters.Assets.GetValueOrDefault(AssetKey, DefaultModel);
        var loaderId = $"{scope}melband_model_loader";
        var samplerId = $"{scope}melband_sampler";

        builder.AddNode(loaderId, node => node
            .Type("MelBandRoFormerModelLoader")
            .Title($"{scopeTitle}Mel-Band RoFormer Model Loader")
            .Input("model_name", modelName));

        builder.AddNode(samplerId, node => node
            .Type("MelBandRoFormerSampler")
            .Title($"{scopeTitle}Mel-Band RoFormer Sampler")
            .InputFromNode("model", loaderId, 0)
            .InputRef("audio", registry.GetRef($"{scope}reference_audio")));

        registry.Register($"{scope}voice_sample", samplerId, 0);
    }
}