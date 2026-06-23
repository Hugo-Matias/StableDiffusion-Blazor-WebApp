using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenSwapAnythingSettingsFragment : IFragmentBuilder
{
    public const string LatentSourceTarget = "target";
    public const string LatentSourceReference = "reference";
    public const string LatentSourceEmpty = "empty";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "swap_anything_settings",
        Type = FragmentType.Settings,
        Title = "Swap Settings",
        Component = "QwenSwapAnythingSettingsForm",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Order = 30,
        Collapsible = true,
        Parameters =
        [
            new() { Name = "sam_prompt_a", Label = "SAM Prompt A", Type = ParameterType.Text, DefaultValue = Defaults.SamPromptA },
            new() { Name = "sam_prompt_b", Label = "SAM Prompt B", Type = ParameterType.Text, DefaultValue = Defaults.SamPromptB },
            new() { Name = "reference_prompt", Label = "Reference Mask Prompt", Type = ParameterType.Text, DefaultValue = Defaults.ReferencePrompt },
            new() { Name = "sam_threshold", Label = "SAM Threshold", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = Defaults.SamThreshold },
            new() { Name = "sam_refine_iterations", Label = "SAM Refine", Type = ParameterType.Slider, Min = 0, Max = 10, Step = 1, DefaultValue = Defaults.SamRefineIterations },
            new() { Name = "grow_mask_expand", Label = "Grow Mask", Type = ParameterType.Slider, Min = 0, Max = 128, Step = 1, DefaultValue = Defaults.GrowMaskExpand },
            new() { Name = "target_megapixels", Label = "Target Megapixels", Type = ParameterType.Slider, Min = 0.01, Max = 16, Step = 0.01, DefaultValue = Defaults.TargetMegapixels },
            new() { Name = "reference_megapixels", Label = "Reference Megapixels", Type = ParameterType.Slider, Min = 0.01, Max = 16, Step = 0.01, DefaultValue = Defaults.ReferenceMegapixels },
            new() { Name = "reference_scale_length", Label = "Reference Long Side", Type = ParameterType.Slider, Min = 256, Max = 4096, Step = 8, DefaultValue = Defaults.ReferenceScaleLength },
            new() { Name = "latent_source", Label = "Latent Source", Type = ParameterType.Select, Options = [LatentSourceTarget, LatentSourceReference, LatentSourceEmpty], DefaultValue = Defaults.LatentSource },
            new() { Name = "empty_latent_width", Label = "Empty Width", Type = ParameterType.Number, Min = 64, Max = 8192, Step = 8, DefaultValue = Defaults.EmptyLatentWidth },
            new() { Name = "empty_latent_height", Label = "Empty Height", Type = ParameterType.Number, Min = 64, Max = 8192, Step = 8, DefaultValue = Defaults.EmptyLatentHeight },
            new() { Name = "empty_latent_batch_size", Label = "Empty Batch", Type = ParameterType.Number, Min = 1, Max = 64, Step = 1, DefaultValue = Defaults.EmptyLatentBatchSize },
            new() { Name = "easycache_reuse_threshold", Label = "Cache Reuse", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = Defaults.EasyCacheReuseThreshold },
            new() { Name = "easycache_start_percent", Label = "Cache Start", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = Defaults.EasyCacheStartPercent },
            new() { Name = "easycache_end_percent", Label = "Cache End", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = Defaults.EasyCacheEndPercent }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public class Parameters
    {
        public string SamPromptA { get; set; } = "face";
        public string SamPromptB { get; set; } = "hair";
        public string ReferencePrompt { get; set; } = "face,hair";
        public double SamThreshold { get; set; } = 0.5;
        public int SamRefineIterations { get; set; } = 2;
        public int GrowMaskExpand { get; set; } = 20;
        public double TargetMegapixels { get; set; } = 1;
        public double ReferenceMegapixels { get; set; } = 1;
        public int ReferenceScaleLength { get; set; } = 1328;
        public string LatentSource { get; set; } = LatentSourceTarget;
        public int EmptyLatentWidth { get; set; } = 896;
        public int EmptyLatentHeight { get; set; } = 1152;
        public int EmptyLatentBatchSize { get; set; } = 1;
        public double EasyCacheReuseThreshold { get; set; } = 0.27;
        public double EasyCacheStartPercent { get; set; } = 0.15;
        public double EasyCacheEndPercent { get; set; } = 0.95;
    }
}