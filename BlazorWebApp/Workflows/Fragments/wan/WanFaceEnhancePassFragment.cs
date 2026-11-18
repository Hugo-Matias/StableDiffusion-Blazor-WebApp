using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFaceEnhancePassFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_face_enhance_pass";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Enhancement,
        Title = "Face Enhance",
        Component = "WanFaceEnhancePassForm",
        Icon = "fa-solid fa-face-smile",
        Order = 70,
        Collapsible = true,
        DefaultCollapsed = false,
        DefaultActive = true,
        Description = "Runs the source workflow's CLIPSeg face crop, Wan low-model refinement, and masked uncrop pass after video upscaling.",
        Parameters =
        [
            Number("max_output_resolution", "Max Output Resolution", 256, 4096, 64, Defaults.MaxOutputResolution),
            Number("crop_resolution", "Face Crop Resolution", 256, 2048, 64, Defaults.CropResolution),
            Number("desired_steps", "Desired Steps", 1, 100, 1, Defaults.DesiredSteps),
            Slider("strength_percent", "Strength", 1, 100, 0.1, Defaults.StrengthPercent),
            Number("seed", "Seed", -1, null, 1, Defaults.Seed),
            Checkbox("force_offload", "Force Offload", Defaults.ForceOffload),
            new FragmentParameter { Name = "scheduler", Label = "Scheduler", Type = ParameterType.Select, Source = new DynamicSource("WanVideoSampler", "scheduler"), DefaultValue = Defaults.Scheduler },
            Slider("cfg", "CFG", 0, 20, 0.1, Defaults.Cfg),
            Slider("shift", "Shift", 0, 20, 0.1, Defaults.Shift),
            Slider("feta_weight", "Enhance Weight", 0, 10, 0.001, Defaults.FetaWeight),
            Slider("primary_threshold", "Primary Mask Threshold", 0, 1, 0.001, Defaults.PrimaryThreshold),
            Slider("composite_threshold", "Composite Mask Threshold", 0, 1, 0.001, Defaults.CompositeThreshold),
            Slider("mask_blur_sigma", "Mask Blur Sigma", 0, 100, 0.1, Defaults.MaskBlurSigma),
            Number("grow_expand", "Mask Expand", -512, 512, 1, Defaults.GrowExpand),
            Slider("grow_blur_radius", "Mask Blur Radius", 0, 100, 0.1, Defaults.GrowBlurRadius),
            Number("sharpen_iterations", "Sharpen Iterations", 1, 12, 1, Defaults.SharpenIterations),
            Number("sharpen_kernel_size", "Sharpen Kernel", 1, 16, 1, Defaults.SharpenKernelSize),
            Slider("border_blending", "Border Blending", 0, 1, 0.01, Defaults.BorderBlending),
            Slider("crop_rescale", "Crop Rescale", 0, 10, 0.01, Defaults.CropRescale),
            Checkbox("use_combined_mask", "Use Combined Mask", Defaults.UseCombinedMask),
            Checkbox("use_square_mask", "Use Square Mask", Defaults.UseSquareMask)
        ]
    };

    public class Parameters
    {
        public int MaxOutputResolution { get; set; } = 2048;
        public int CropResolution { get; set; } = 768;
        public int DesiredSteps { get; set; } = 4;
        public double StrengthPercent { get; set; } = 24.54541015625;
        public long Seed { get; set; }
        public bool ForceOffload { get; set; } = true;
        public string Scheduler { get; set; } = "lcm";
        public double Cfg { get; set; } = 1;
        public double Shift { get; set; } = 5;
        public double FetaWeight { get; set; } = 2;
        public double PrimaryThreshold { get; set; } = 0.25;
        public double CompositeThreshold { get; set; } = 0.2;
        public double MaskBlurSigma { get; set; } = 10;
        public int GrowExpand { get; set; } = 55;
        public double GrowBlurRadius { get; set; } = 30;
        public int SharpenIterations { get; set; } = 2;
        public int SharpenKernelSize { get; set; } = 3;
        public double BorderBlending { get; set; } = 0.5;
        public double CropRescale { get; set; } = 1;
        public bool UseCombinedMask { get; set; }
        public bool UseSquareMask { get; set; } = true;
    }

    public Parameters Read(GenerationParameters parameters)
    {
        var fragment = parameters.GetFragment(FragmentId);
        return new Parameters
        {
            MaxOutputResolution = fragment?.GetInt("max_output_resolution", Defaults.MaxOutputResolution) ?? Defaults.MaxOutputResolution,
            CropResolution = fragment?.GetInt("crop_resolution", Defaults.CropResolution) ?? Defaults.CropResolution,
            DesiredSteps = fragment?.GetInt("desired_steps", Defaults.DesiredSteps) ?? Defaults.DesiredSteps,
            StrengthPercent = fragment?.GetDouble("strength_percent", Defaults.StrengthPercent) ?? Defaults.StrengthPercent,
            Seed = ResolveRuntimeSeed(fragment?.GetLong("seed", Defaults.Seed) ?? Defaults.Seed),
            ForceOffload = fragment?.GetBool("force_offload", Defaults.ForceOffload) ?? Defaults.ForceOffload,
            Scheduler = fragment?.GetString("scheduler", Defaults.Scheduler) ?? Defaults.Scheduler,
            Cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg,
            Shift = fragment?.GetDouble("shift", Defaults.Shift) ?? Defaults.Shift,
            FetaWeight = fragment?.GetDouble("feta_weight", Defaults.FetaWeight) ?? Defaults.FetaWeight,
            PrimaryThreshold = fragment?.GetDouble("primary_threshold", Defaults.PrimaryThreshold) ?? Defaults.PrimaryThreshold,
            CompositeThreshold = fragment?.GetDouble("composite_threshold", Defaults.CompositeThreshold) ?? Defaults.CompositeThreshold,
            MaskBlurSigma = fragment?.GetDouble("mask_blur_sigma", Defaults.MaskBlurSigma) ?? Defaults.MaskBlurSigma,
            GrowExpand = fragment?.GetInt("grow_expand", Defaults.GrowExpand) ?? Defaults.GrowExpand,
            GrowBlurRadius = fragment?.GetDouble("grow_blur_radius", Defaults.GrowBlurRadius) ?? Defaults.GrowBlurRadius,
            SharpenIterations = fragment?.GetInt("sharpen_iterations", Defaults.SharpenIterations) ?? Defaults.SharpenIterations,
            SharpenKernelSize = fragment?.GetInt("sharpen_kernel_size", Defaults.SharpenKernelSize) ?? Defaults.SharpenKernelSize,
            BorderBlending = fragment?.GetDouble("border_blending", Defaults.BorderBlending) ?? Defaults.BorderBlending,
            CropRescale = fragment?.GetDouble("crop_rescale", Defaults.CropRescale) ?? Defaults.CropRescale,
            UseCombinedMask = fragment?.GetBool("use_combined_mask", Defaults.UseCombinedMask) ?? Defaults.UseCombinedMask,
            UseSquareMask = fragment?.GetBool("use_square_mask", Defaults.UseSquareMask) ?? Defaults.UseSquareMask
        };
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        if (parameters.GetFragment(FragmentId)?.IsActive != true)
        {
            return;
        }

        Build(builder, registry, Read(parameters), scope, scopeTitle);
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope = "", string scopeTitle = "")
    {
        var prefix = string.IsNullOrEmpty(scope) ? "face_" : scope;

        builder.AddNode($"{prefix}clipseg_loader", node => node
            .Type("DownloadAndLoadCLIPSeg")
            .Title($"{scopeTitle}CLIPSeg Loader")
            .Input("model", "Kijai/clipseg-rd64-refined-fp16"));

        builder.AddNode($"{prefix}full_resize", node => node
            .Type("ImageResize+")
            .Title($"{scopeTitle}Resize For Face Detection")
            .InputRef("image", registry.GetRef("image_output"))
            .Input("width", p.MaxOutputResolution)
            .Input("height", p.MaxOutputResolution)
            .Input("interpolation", "lanczos")
            .Input("method", "keep proportion")
            .Input("condition", "always")
            .Input("multiple_of", 0));

        builder.AddNode($"{prefix}primary_clipseg", node => node
            .Type("BatchCLIPSeg")
            .Title($"{scopeTitle}Primary Face Mask")
            .InputFromNode("images", $"{prefix}full_resize", 0)
            .Input("text", "face")
            .Input("threshold", p.PrimaryThreshold)
            .Input("binary_mask", true)
            .Input("combine_mask", true)
            .Input("use_cuda", true)
            .Input("blur_sigma", p.MaskBlurSigma)
            .InputFromNode("opt_model", $"{prefix}clipseg_loader", 0)
            .Input("image_bg_level", 0.0)
            .Input("invert", false));

        builder.AddNode($"{prefix}crop_from_mask", node => node
            .Type("BatchCropFromMaskAdvanced")
            .Title($"{scopeTitle}Crop Faces")
            .InputFromNode("original_images", $"{prefix}full_resize", 0)
            .InputFromNode("masks", $"{prefix}primary_clipseg", 0)
            .Input("crop_size_mult", 1.0)
            .Input("bbox_smooth_alpha", 1.0));

        builder.AddNode($"{prefix}sharpen", node => node
            .Type("Image Lucy Sharpen")
            .Title($"{scopeTitle}Sharpen Face Crop")
            .InputFromNode("images", $"{prefix}crop_from_mask", 1)
            .Input("iterations", p.SharpenIterations)
            .Input("kernel_size", p.SharpenKernelSize));

        builder.AddNode($"{prefix}crop_resize", node => node
            .Type("ImageResize+")
            .Title($"{scopeTitle}Resize Face Crop")
            .InputFromNode("image", $"{prefix}sharpen", 0)
            .Input("width", p.CropResolution)
            .Input("height", p.CropResolution)
            .Input("interpolation", "nearest-exact")
            .Input("method", "keep proportion")
            .Input("condition", "always")
            .Input("multiple_of", 0));

        builder.AddNode($"{prefix}remove_alpha", node => node
            .Type("ImageRemoveAlpha+")
            .Title($"{scopeTitle}Remove Alpha")
            .InputFromNode("image", $"{prefix}crop_resize", 0));

        builder.AddNode($"{prefix}crop_size", node => node
            .Type("GetImageSize+")
            .Title($"{scopeTitle}Face Crop Size")
            .InputFromNode("image", $"{prefix}remove_alpha", 0));

        builder.AddNode($"{prefix}empty_embeds", node => node
            .Type("WanVideoEmptyEmbeds")
            .Title($"{scopeTitle}Empty Embeds")
            .InputFromNode("width", $"{prefix}crop_size", 0)
            .InputFromNode("height", $"{prefix}crop_size", 1)
            .InputFromNode("num_frames", $"{prefix}crop_size", 2));

        builder.AddNode($"{prefix}encode", node => node
            .Type("WanVideoEncode")
            .Title($"{scopeTitle}Encode Face Crop")
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputFromNode("image", $"{prefix}remove_alpha", 0)
            .Input("enable_vae_tiling", false)
            .Input("tile_x", 272)
            .Input("tile_y", 272)
            .Input("tile_stride_x", 144)
            .Input("tile_stride_y", 128)
            .Input("noise_aug_strength", 0.03)
            .Input("latent_strength", 1.0));

        builder.AddNode($"{prefix}steps", node => node
            .Type("Strength To Steps")
            .Title($"{scopeTitle}Strength To Steps")
            .Input("desired_steps", p.DesiredSteps)
            .Input("strength_percent", p.StrengthPercent));

        builder.AddNode($"{prefix}feta", node => node
            .Type("WanVideoEnhanceAVideo")
            .Title($"{scopeTitle}Enhance-A-Video")
            .Input("weight", p.FetaWeight)
            .Input("start_percent", 0.0)
            .Input("end_percent", 1.0));

        builder.AddNode($"{prefix}sampler", node => node
            .Type("WanVideoSampler")
            .Title($"{scopeTitle}Wan Face Sampler")
            .InputRef("model", registry.GetRef("model_output"))
            .InputFromNode("image_embeds", $"{prefix}empty_embeds", 0)
            .InputRef("text_embeds", registry.GetRef("text_embeds_output"))
            .InputFromNode("samples", $"{prefix}encode", 0)
            .InputFromNode("feta_args", $"{prefix}feta", 0)
            .InputFromNode("steps", $"{prefix}steps", 0)
            .Input("cfg", p.Cfg)
            .Input("shift", p.Shift)
            .Input("seed", p.Seed)
            .Input("force_offload", p.ForceOffload)
            .Input("scheduler", p.Scheduler)
            .Input("riflex_freq_index", 0)
            .Input("denoise_strength", 1.0)
            .Input("batched_cfg", false)
            .Input("rope_function", "comfy")
            .InputFromNode("start_step", $"{prefix}steps", 1)
            .Input("end_step", -1)
            .Input("add_noise_to_samples", true));

        builder.AddNode($"{prefix}decode", node => node
            .Type("WanVideoDecode")
            .Title($"{scopeTitle}Decode Face Crop")
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputFromNode("samples", $"{prefix}sampler", 1)
            .Input("enable_vae_tiling", false)
            .Input("tile_x", 512)
            .Input("tile_y", 512)
            .Input("tile_stride_x", 256)
            .Input("tile_stride_y", 256)
            .Input("normalization", "default"));

        builder.AddNode($"{prefix}composite_clipseg", node => node
            .Type("BatchCLIPSeg")
            .Title($"{scopeTitle}Composite Face Mask")
            .InputFromNode("images", $"{prefix}remove_alpha", 0)
            .Input("text", "face")
            .Input("threshold", p.CompositeThreshold)
            .Input("binary_mask", true)
            .Input("combine_mask", true)
            .Input("use_cuda", true)
            .Input("blur_sigma", 0.0)
            .InputFromNode("opt_model", $"{prefix}clipseg_loader", 0)
            .Input("image_bg_level", 0.0)
            .Input("invert", false));

        builder.AddNode($"{prefix}grow_mask", node => node
            .Type("GrowMaskWithBlur")
            .Title($"{scopeTitle}Grow Mask")
            .InputFromNode("mask", $"{prefix}composite_clipseg", 0)
            .Input("expand", p.GrowExpand)
            .Input("incremental_expandrate", 0.0)
            .Input("tapered_corners", true)
            .Input("flip_input", false)
            .Input("blur_radius", p.GrowBlurRadius)
            .Input("lerp_alpha", 1.0)
            .Input("decay_factor", 1.0)
            .Input("fill_holes", false));

        builder.AddNode($"{prefix}composite", node => node
            .Type("ImageCompositeMasked")
            .Title($"{scopeTitle}Composite Face")
            .InputFromNode("source", $"{prefix}decode", 0)
            .Input("x", 0)
            .Input("y", 0)
            .Input("resize_source", true)
            .InputFromNode("destination", $"{prefix}remove_alpha", 0)
            .InputFromNode("mask", $"{prefix}grow_mask", 0));

        builder.AddNode($"{prefix}uncrop", node => node
            .Type("BatchUncropAdvanced")
            .Title($"{scopeTitle}Uncrop Face")
            .InputFromNode("original_images", $"{prefix}crop_from_mask", 0)
            .InputFromNode("cropped_images", $"{prefix}composite", 0)
            .InputFromNode("cropped_masks", $"{prefix}crop_from_mask", 2)
            .InputFromNode("combined_crop_mask", $"{prefix}crop_from_mask", 4)
            .InputFromNode("bboxes", $"{prefix}crop_from_mask", 5)
            .Input("border_blending", p.BorderBlending)
            .Input("crop_rescale", p.CropRescale)
            .Input("use_combined_mask", p.UseCombinedMask)
            .Input("use_square_mask", p.UseSquareMask));

        registry.Register("image_output", $"{prefix}uncrop", 0);
    }

    private static long ResolveRuntimeSeed(long seed)
    {
        return seed < 0 ? Random.Shared.NextInt64(0, int.MaxValue) : seed;
    }

    private static FragmentParameter Number(string name, string label, double? min, double? max, double step, object defaultValue) => new()
    {
        Name = name,
        Label = label,
        Type = ParameterType.Number,
        Min = min,
        Max = max,
        Step = step,
        DefaultValue = defaultValue
    };

    private static FragmentParameter Slider(string name, string label, double min, double max, double step, object defaultValue) => new()
    {
        Name = name,
        Label = label,
        Type = ParameterType.Slider,
        Min = min,
        Max = max,
        Step = step,
        DefaultValue = defaultValue
    };

    private static FragmentParameter Checkbox(string name, string label, bool defaultValue) => new()
    {
        Name = name,
        Label = label,
        Type = ParameterType.Checkbox,
        DefaultValue = defaultValue
    };
}