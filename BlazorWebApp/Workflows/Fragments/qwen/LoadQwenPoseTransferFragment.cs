using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class LoadQwenPoseTransferFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader_qwen_pose_transfer",
        Type = FragmentType.Loader,
        Title = "Load Qwen Pose Transfer",
        IsHidden = true
    };

    public class Parameters
    {
        public string UnetName { get; set; } = "qwen_image_edit_2511_fp8mixed.safetensors";
        public string ClipName { get; set; } = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        public string VaeName { get; set; } = "qwen_image_vae.safetensors";
        public string LightningLoraName { get; set; } = "Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors";
        public string ConsistencyLoraName { get; set; } = "Util/qe2511_consis_alpha_patched.safetensors";
        public double LightningLoraStrength { get; set; } = 1.0;
        public double ConsistencyLoraStrength { get; set; } = 0.6;
        public double ModelShift { get; set; } = 3.0;
        public double CfgNormStrength { get; set; } = 1.0;
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
        builder.AddNode($"{scope}unet_loader", node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}Load Qwen Edit Model")
            .Input("unet_name", fragmentParams.UnetName)
            .Input("weight_dtype", "default"));

        builder.AddNode($"{scope}clip_loader", node => node
            .Type("CLIPLoader")
            .Title($"{scopeTitle}Load Qwen CLIP")
            .Input("clip_name", fragmentParams.ClipName)
            .Input("type", "stable_diffusion")
            .Input("device", "default"));

        builder.AddNode($"{scope}vae_loader", node => node
            .Type("VAELoader")
            .Title($"{scopeTitle}Load Qwen VAE")
            .Input("vae_name", fragmentParams.VaeName));

        builder.AddNode($"{scope}qwen_lightning_lora_loader", node => node
            .Type("LoraLoaderModelOnly")
            .Title($"{scopeTitle}Qwen Lightning LoRA")
            .Input("lora_name", fragmentParams.LightningLoraName)
            .Input("strength_model", fragmentParams.LightningLoraStrength)
            .InputFromNode("model", $"{scope}unet_loader", 0));

        builder.AddNode($"{scope}qwen_consistency_lora_loader", node => node
            .Type("LoraLoaderModelOnly")
            .Title($"{scopeTitle}Qwen Consistency LoRA")
            .Input("lora_name", fragmentParams.ConsistencyLoraName)
            .Input("strength_model", fragmentParams.ConsistencyLoraStrength)
            .InputFromNode("model", $"{scope}qwen_lightning_lora_loader", 0));

        builder.AddNode($"{scope}model_sampling", node => node
            .Type("ModelSamplingAuraFlow")
            .Title($"{scopeTitle}ModelSamplingAuraFlow")
            .Input("shift", fragmentParams.ModelShift)
            .InputFromNode("model", $"{scope}qwen_consistency_lora_loader", 0));

        builder.AddNode($"{scope}cfg_norm", node => node
            .Type("CFGNorm")
            .Title($"{scopeTitle}CFGNorm")
            .Input("strength", fragmentParams.CfgNormStrength)
            .InputFromNode("model", $"{scope}model_sampling", 0));

        registry.Register($"{scope}model_output", $"{scope}cfg_norm", 0);
        registry.Register($"{scope}clip_output", $"{scope}clip_loader", 0);
        registry.Register($"{scope}vae_output", $"{scope}vae_loader", 0);
    }
}
