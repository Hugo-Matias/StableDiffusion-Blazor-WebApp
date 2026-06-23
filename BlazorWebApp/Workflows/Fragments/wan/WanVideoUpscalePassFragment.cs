using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVideoUpscalePassFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_video_upscale_pass",
        Type = FragmentType.Enhancement,
        Title = "Wan Video Upscale Pass",
        IsHidden = true
    };

    public class Parameters
    {
        public string UpscaleModelName { get; set; } = "Remacri (foolhardy).pth";
        public string TextEncoderName { get; set; } = "umt5_xxl_fp16.safetensors";
        public WanUpscaleFaceEnhanceSettingsFragment.Parameters Settings { get; set; } = new();
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope = "", string scopeTitle = "")
    {
        var s = p.Settings;
        var prefix = string.IsNullOrEmpty(scope) ? "upscale_" : scope;

        builder.AddNode($"{prefix}pixel_upscale", node => node
            .Type("CRT_UpscaleModelAdv")
            .Title($"{scopeTitle}Pixel Upscale")
            .InputRef("image", registry.GetRef("video_frames"))
            .Input("upscale_model_name", p.UpscaleModelName)
            .Input("use_fixed_resolution", s.UpscaleUseFixedResolution)
            .Input("output_multiplier", s.UpscaleOutputMultiplier)
            .Input("fixed_width", s.UpscaleFixedWidth)
            .Input("fixed_height", s.UpscaleFixedHeight)
            .Input("tile_count", s.UpscaleTileCount)
            .Input("precision", s.UpscalePrecision)
            .Input("batch_size", s.UpscaleBatchSize)
            .Input("offload_model", s.UpscaleOffloadModel)
            .Input("disable_cache", s.UpscaleDisableCache));

        builder.AddNode($"{prefix}first_frame", node => node
            .Type("ImageFromBatch+")
            .Title($"{scopeTitle}First Frame")
            .InputRef("image", registry.GetRef("video_frames"))
            .Input("start", 0)
            .Input("length", 1));

        builder.AddNode($"{prefix}florence_loader", node => node
            .Type("DownloadAndLoadFlorence2Model")
            .Title($"{scopeTitle}Florence2 Loader")
            .Input("model", "microsoft/Florence-2-base")
            .Input("precision", "fp16")
            .Input("convert_to_safetensors", false));

        builder.AddNode($"{prefix}caption", node => node
            .Type("Florence2Run")
            .Title($"{scopeTitle}Caption First Frame")
            .InputFromNode("image", $"{prefix}first_frame", 0)
            .InputFromNode("florence2_model", $"{prefix}florence_loader", 0)
            .Input("text_input", string.Empty)
            .Input("task", "caption")
            .Input("fill_mask", true)
            .Input("keep_model_loaded", false)
            .Input("max_new_tokens", 128)
            .Input("num_beams", 3)
            .Input("do_sample", true)
            .Input("output_mask_select", string.Empty)
            .Input("seed", 1));

        builder.AddNode($"{prefix}prompt_join", node => node
            .Type("JoinStrings")
            .Title($"{scopeTitle}Prompt Join")
            .Input("delimiter", " ")
            .Input("string1", s.PromptPrefix)
            .InputFromNode("string2", $"{prefix}caption", 2));

        builder.AddNode($"{prefix}text_encode", node => node
            .Type("WanVideoTextEncodeCached")
            .Title($"{scopeTitle}Wan Text Encode")
            .Input("model_name", p.TextEncoderName)
            .Input("precision", s.TextPrecision)
            .InputFromNode("positive_prompt", $"{prefix}prompt_join", 0)
            .Input("negative_prompt", s.NegativePrompt)
            .Input("quantization", s.TextQuantization)
            .Input("use_disk_cache", true)
            .Input("device", s.TextDevice));

        builder.AddNode($"{prefix}nag", node => node
            .Type("WanVideoApplyNAG")
            .Title($"{scopeTitle}Apply NAG")
            .InputFromNode("original_text_embeds", $"{prefix}text_encode", 0)
            .InputFromNode("nag_text_embeds", $"{prefix}text_encode", 1)
            .Input("nag_scale", s.NagScale)
            .Input("nag_tau", s.NagTau)
            .Input("nag_alpha", s.NagAlpha)
            .Input("inplace", true));

        builder.AddNode($"{prefix}quantize_crop", node => node
            .Type("CRT_QuantizeAndCropImage")
            .Title($"{scopeTitle}Quantize Crop")
            .InputFromNode("image", $"{prefix}pixel_upscale", 0)
            .Input("max_side_length", s.UpscaleMaxSideLength));

        builder.AddNode($"{prefix}size", node => node
            .Type("GetImageSizeAndCount")
            .Title($"{scopeTitle}Upscale Size")
            .InputFromNode("image", $"{prefix}quantize_crop", 0));

        builder.AddNode($"{prefix}empty_embeds", node => node
            .Type("WanVideoEmptyEmbeds")
            .Title($"{scopeTitle}Empty Embeds")
            .InputFromNode("width", $"{prefix}size", 1)
            .InputFromNode("height", $"{prefix}size", 2)
            .InputFromNode("num_frames", $"{prefix}size", 3));

        builder.AddNode($"{prefix}encode", node => node
            .Type("WanVideoEncode")
            .Title($"{scopeTitle}Encode Upscale")
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputFromNode("image", $"{prefix}size", 0)
            .Input("enable_vae_tiling", true)
            .Input("tile_x", 512)
            .Input("tile_y", 512)
            .Input("tile_stride_x", 256)
            .Input("tile_stride_y", 256)
            .Input("noise_aug_strength", 0.05)
            .Input("latent_strength", 1.0));

        builder.AddNode($"{prefix}steps", node => node
            .Type("Strength To Steps")
            .Title($"{scopeTitle}Strength To Steps")
            .Input("desired_steps", s.UpscaleDesiredSteps)
            .Input("strength_percent", s.UpscaleStrengthPercent));

        builder.AddNode($"{prefix}feta", node => node
            .Type("WanVideoEnhanceAVideo")
            .Title($"{scopeTitle}Enhance-A-Video")
            .Input("weight", s.FetaWeight)
            .Input("start_percent", s.FetaStartPercent)
            .Input("end_percent", s.FetaEndPercent));

        builder.AddNode($"{prefix}sampler", node => node
            .Type("WanVideoSampler")
            .Title($"{scopeTitle}Wan Upscale Sampler")
            .InputRef("model", registry.GetRef("model_output"))
            .InputFromNode("image_embeds", $"{prefix}empty_embeds", 0)
            .InputFromNode("text_embeds", $"{prefix}nag", 0)
            .InputFromNode("samples", $"{prefix}encode", 0)
            .InputFromNode("feta_args", $"{prefix}feta", 0)
            .InputFromNode("steps", $"{prefix}steps", 0)
            .Input("cfg", s.SamplerCfg)
            .Input("shift", s.SamplerShift)
            .Input("seed", s.UpscaleSeed)
            .Input("force_offload", s.ForceOffload)
            .Input("scheduler", s.SamplerScheduler)
            .Input("riflex_freq_index", 0)
            .Input("denoise_strength", 1.0)
            .Input("batched_cfg", false)
            .Input("rope_function", "comfy")
            .InputFromNode("start_step", $"{prefix}steps", 1)
            .Input("end_step", -1)
            .Input("add_noise_to_samples", true));

        builder.AddNode($"{prefix}decode", node => node
            .Type("WanVideoDecode")
            .Title($"{scopeTitle}Decode Upscale")
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputFromNode("samples", $"{prefix}sampler", 1)
            .Input("enable_vae_tiling", true)
            .Input("tile_x", 512)
            .Input("tile_y", 512)
            .Input("tile_stride_x", 256)
            .Input("tile_stride_y", 256)
            .Input("normalization", "default"));

        registry.Register("text_embeds_output", $"{prefix}nag", 0);
        registry.Register("image_output", $"{prefix}decode", 0);
    }
}