using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Audio;

public class AceStepCheckpointLoaderFragment : IFragmentBuilder
{
    public const string AssetKey = "Model";
    public const string DefaultCheckpoint = "AceStep/ace_step_1.5_turbo_aio.safetensors";

    public FragmentMetadata Metadata => new()
    {
        Id = "ace_step_checkpoint_loader",
        Type = FragmentType.Loader,
        Title = "Ace Step Checkpoint Loader",
        IsHidden = true
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var checkpoint = parameters.Assets.GetValueOrDefault(AssetKey, DefaultCheckpoint);
        var nodeId = $"{scope}ace_step_checkpoint_loader";

        builder.AddNode(nodeId, node => node
            .Type("CheckpointLoaderSimple")
            .Title($"{scopeTitle}CheckpointLoaderSimple")
            .Input("ckpt_name", checkpoint));

        registry.Register($"{scope}model_output", nodeId, 0);
        registry.Register($"{scope}clip_output", nodeId, 1);
        registry.Register($"{scope}vae_output", nodeId, 2);
        registry.Register(new ModelOutput(nodeId, 0));
        registry.Register(new ClipOutput(nodeId, 1));
        registry.Register(new VaeOutput(nodeId, 2));
    }
}