using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenPoseTransferConditioningFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "qwen_pose_transfer_conditioning",
        Type = FragmentType.Conditioning,
        Title = "Qwen Pose Transfer Conditioning",
        IsHidden = true
    };

    public class Parameters
    {
        public string Prompt { get; set; } = string.Empty;
        public string TargetImageKey { get; set; } = QwenPoseTransferImageFragment.TargetImageOutput;
        public string PoseImageKey { get; set; } = QwenPoseTransferImageFragment.PoseImageOutput;
        public int LatentBatchSize { get; set; } = 1;
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
        builder.AddNode("qwen_pose_encode", node => node
            .Type("TextEncodeQwenImageEditPlus")
            .Title("Qwen Pose Transfer Encode")
            .Input("prompt", fragmentParams.Prompt)
            .InputRef("clip", registry.GetRef($"{scope}clip_output"))
            .InputRef("vae", registry.GetRef($"{scope}vae_output"))
            .InputRef("image1", registry.GetRef(fragmentParams.TargetImageKey))
            .InputRef("image2", registry.GetRef(fragmentParams.PoseImageKey)));

        builder.AddNode("qwen_pose_positive_method", node => node
            .Type("FluxKontextMultiReferenceLatentMethod")
            .Title("Positive Reference Method")
            .Input("reference_latents_method", "index_timestep_zero")
            .InputFromNode("conditioning", "qwen_pose_encode", 0));

        builder.AddNode("qwen_pose_negative_zero", node => node
            .Type("ConditioningZeroOut")
            .Title("Negative Zero Out")
            .InputFromNode("conditioning", "qwen_pose_encode", 0));

        builder.AddNode("qwen_pose_negative_method", node => node
            .Type("FluxKontextMultiReferenceLatentMethod")
            .Title("Negative Reference Method")
            .Input("reference_latents_method", "index_timestep_zero")
            .InputFromNode("conditioning", "qwen_pose_negative_zero", 0));

        builder.AddNode("qwen_pose_vae_encode", node => node
            .Type("VAEEncode")
            .Title("Pose VAE Encode")
            .InputRef("pixels", registry.GetRef(fragmentParams.PoseImageKey))
            .InputRef("vae", registry.GetRef($"{scope}vae_output")));

        builder.AddNode("qwen_pose_repeat_latent", node => node
            .Type("RepeatLatentBatch")
            .Title("Repeat Pose Latent")
            .Input("amount", fragmentParams.LatentBatchSize)
            .InputFromNode("samples", "qwen_pose_vae_encode", 0));

        registry.Register("positive_output", "qwen_pose_positive_method", 0);
        registry.Register("negative_output", "qwen_pose_negative_method", 0);
        registry.Register("latent_output", "qwen_pose_repeat_latent", 0);
    }
}
