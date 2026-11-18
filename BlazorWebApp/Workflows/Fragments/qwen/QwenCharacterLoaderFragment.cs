using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenCharacterLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader_qwen_character",
        Type = FragmentType.Loader,
        Title = "Load Qwen Character",
        IsHidden = true
    };

    public class Parameters
    {
        public CharacterLoaderMode LoaderMode { get; set; } = CharacterLoaderMode.AioCheckpoint;
        public string CheckpointName { get; set; } = CharacterReferenceDefaults.AioCheckpoint;
        public string UnetName { get; set; } = string.Empty;
        public string WeightDtype { get; set; } = "default";
        public string ClipName { get; set; } = CharacterReferenceDefaults.SplitClip;
        public string ClipType { get; set; } = "qwen_image";
        public string ClipDevice { get; set; } = "default";
        public string VaeName { get; set; } = CharacterReferenceDefaults.SplitVae;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        if (fragmentParams.LoaderMode == CharacterLoaderMode.SplitStack)
        {
            BuildSplitStack(builder, registry, fragmentParams, scope, scopeTitle);
            return;
        }

        BuildAioCheckpoint(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildAioCheckpoint(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var checkpointLoaderId = $"{scope}checkpoint_loader";

        builder.AddNode(checkpointLoaderId, node => node
            .Type("CheckpointLoaderSimple")
            .Title($"{scopeTitle}Load Qwen Checkpoint")
            .Input("ckpt_name", p.CheckpointName));

        registry.Register($"{scope}model_output", checkpointLoaderId, 0);
        registry.Register($"{scope}clip_output", checkpointLoaderId, 1);
        registry.Register($"{scope}vae_output", checkpointLoaderId, 2);
    }

    private static void BuildSplitStack(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var unetLoaderId = $"{scope}unet_loader";
        var clipLoaderId = $"{scope}clip_loader";
        var vaeLoaderId = $"{scope}vae_loader";

        builder.AddNode(unetLoaderId, node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Qwen Diffusion Model")
            .Input("unet_name", p.UnetName)
            .Input("weight_dtype", p.WeightDtype));

        builder.AddNode(clipLoaderId, node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load Qwen CLIP")
            .Input("clip_name", p.ClipName)
            .Input("type", p.ClipType)
            .Input("device", p.ClipDevice));

        builder.AddNode(vaeLoaderId, node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load Qwen VAE")
            .Input("vae_name", p.VaeName));

        registry.Register($"{scope}model_output", unetLoaderId, 0);
        registry.Register($"{scope}clip_output", clipLoaderId, 0);
        registry.Register($"{scope}vae_output", vaeLoaderId, 0);
    }
}
