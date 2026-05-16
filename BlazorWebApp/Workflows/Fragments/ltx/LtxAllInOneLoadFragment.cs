using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxAllInOneLoadFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_all_in_one_load",
        Type = FragmentType.Loader,
        Title = "LTX All-In-One Loader",
        IsHidden = true
    };

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
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var checkpointId = $"{scope}ltx_checkpoint_loader";
        var unetId = $"{scope}ltx_unet_loader";
        var clipId = $"{scope}ltx_dual_clip_loader";
        var videoVaeId = $"{scope}ltx_video_vae_loader";
        var audioVaeId = $"{scope}ltx_audio_vae_loader";
        var upscaleId = $"{scope}ltx_latent_upscale_loader";

        builder.AddNode(clipId, node => node
            .Type("DualCLIPLoader")
            .Title($"{scopeTitle}DualCLIPLoader")
            .Input("clip_name1", p.ClipName)
            .Input("clip_name2", p.ClipName2)
            .Input("type", "ltxv")
            .Input("device", "default"));

        builder.AddNode(audioVaeId, node => node
            .Type("VAELoaderKJ")
            .Title($"{scopeTitle}Audio VAELoaderKJ")
            .Input("vae_name", p.AudioVaeName)
            .Input("device", "main_device")
            .Input("weight_dtype", p.VaeWeightDtype));

        builder.AddNode(upscaleId, node => node
            .Type("LatentUpscaleModelLoader")
            .Title($"{scopeTitle}Latent Upscale Model")
            .Input("model_name", p.UpscaleModelName));

        string modelInputId;
        string videoVaeInputId;
        var useDiffusion = string.Equals(p.LoaderMode, "diffusion", StringComparison.OrdinalIgnoreCase);

        if (useDiffusion)
        {
            builder.AddNode(unetId, node => node
                .Type("UNETLoader")
                .Title($"{scopeTitle}UNETLoader")
                .Input("unet_name", p.UnetName)
                .Input("weight_dtype", p.UnetWeightDtype));

            builder.AddNode(videoVaeId, node => node
                .Type("VAELoaderKJ")
                .Title($"{scopeTitle}Video VAELoaderKJ")
                .Input("vae_name", p.VideoVaeName)
                .Input("device", "main_device")
                .Input("weight_dtype", p.VaeWeightDtype));

            modelInputId = unetId;
            videoVaeInputId = videoVaeId;
        }
        else
        {
            builder.AddNode(checkpointId, node => node
                .Type("CheckpointLoaderSimple")
                .Title($"{scopeTitle}CheckpointLoaderSimple")
                .Input("ckpt_name", p.CheckpointName));

            if (p.UseSeparateVideoVae)
            {
                builder.AddNode(videoVaeId, node => node
                    .Type("VAELoaderKJ")
                    .Title($"{scopeTitle}Video VAELoaderKJ")
                    .Input("vae_name", p.VideoVaeName)
                    .Input("device", "main_device")
                    .Input("weight_dtype", p.VaeWeightDtype));
            }

            modelInputId = checkpointId;
            videoVaeInputId = p.UseSeparateVideoVae ? videoVaeId : checkpointId;
        }

        registry.Register($"{scope}model_output", modelInputId, 0);
        registry.Register($"{scope}clip_output", clipId, 0);
        registry.Register($"{scope}vae_output", videoVaeInputId, useDiffusion || p.UseSeparateVideoVae ? 0 : 2);
        registry.Register($"{scope}audio_vae_output", audioVaeId, 0);
        registry.Register($"{scope}upscale_model_output", upscaleId, 0);
    }

    public class Parameters
    {
        public string LoaderMode { get; set; } = "checkpoint";
        public string CheckpointName { get; set; } = "Photography/sulphur_dev_fp8mixed.safetensors";
        public string UnetName { get; set; } = "ltx-2.3-22b-distilled-1.1_transformer_only_mxfp8_block32.safetensors";
        public string ClipName { get; set; } = "gemma_3_12B_it_fp4_mixed.safetensors";
        public string ClipName2 { get; set; } = "ltx-2.3_text_projection_bf16.safetensors";
        public string VideoVaeName { get; set; } = "LTX23_video_vae_bf16.safetensors";
        public string AudioVaeName { get; set; } = "LTX23_audio_vae_bf16.safetensors";
        public string UpscaleModelName { get; set; } = "ltx-2.3-spatial-upscaler-x2-1.1.safetensors";
        public string UnetWeightDtype { get; set; } = "default";
        public string VaeWeightDtype { get; set; } = "bf16";
        public bool UseSeparateVideoVae { get; set; } = false;
    }
}