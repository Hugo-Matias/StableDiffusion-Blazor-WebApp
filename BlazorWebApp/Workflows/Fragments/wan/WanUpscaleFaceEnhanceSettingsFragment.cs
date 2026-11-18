using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanUpscaleFaceEnhanceSettingsFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_upscale_face_settings";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Settings,
        Title = "Upscale Settings",
        Component = "WanUpscaleFaceEnhanceSettingsForm",
        Icon = "fa-solid fa-up-right-and-down-left-from-center",
        Order = 30,
        Collapsible = false,
        Parameters =
        [
            Number("frame_load_cap", "Frame Load Cap", 0, 512, 1, Defaults.FrameLoadCap),
            Number("skip_first_frames", "Skip First Frames", 0, 10000, 1, Defaults.SkipFirstFrames),
            Number("select_every_nth", "Select Every Nth", 1, 100, 1, Defaults.SelectEveryNth),
            Slider("frame_rate", "Output FPS", 1, 120, 1, Defaults.FrameRate),
            Slider("crf", "CRF", 0, 51, 1, Defaults.Crf),
            Number("blocks_to_swap", "Blocks To Swap", 0, 64, 1, Defaults.BlocksToSwap),
            Checkbox("use_non_blocking", "Use Non-blocking", Defaults.UseNonBlocking),
            Select("base_precision", "Base Precision", Defaults.BasePrecision, ["fp32", "bf16", "fp16", "fp16_fast"]),
            Select("quantization", "Quantization", Defaults.Quantization, ["disabled", "fp8_e4m3fn", "fp8_e4m3fn_fast", "fp8_e4m3fn_scaled", "fp8_e4m3fn_scaled_fast", "fp8_e5m2", "fp8_e5m2_fast", "fp8_e5m2_scaled", "fp8_e5m2_scaled_fast"]),
            Select("load_device", "Load Device", Defaults.LoadDevice, ["main_device", "offload_device"]),
            new FragmentParameter { Name = "attention_mode", Label = "Attention Mode", Type = ParameterType.Select, Source = new DynamicSource("WanVideoModelLoader", "attention_mode"), DefaultValue = Defaults.AttentionMode },
            Select("vae_precision", "VAE Precision", Defaults.VaePrecision, ["fp16", "fp32", "bf16"]),
            Slider("speed_lora_strength", "Speed LoRA Strength", 0, 2, 0.01, Defaults.SpeedLoraStrength),
            Checkbox("force_offload", "Force Offload", Defaults.ForceOffload),
            Number("upscale_max_side_length", "Video Max Side", 256, 8192, 64, Defaults.UpscaleMaxSideLength),
            Checkbox("upscale_use_fixed_resolution", "Use Fixed Resolution", Defaults.UpscaleUseFixedResolution),
            Slider("upscale_output_multiplier", "Upscale Multiplier", 0.25, 8, 0.25, Defaults.UpscaleOutputMultiplier),
            Number("upscale_fixed_width", "Fixed Width", 64, 8192, 8, Defaults.UpscaleFixedWidth),
            Number("upscale_fixed_height", "Fixed Height", 64, 8192, 8, Defaults.UpscaleFixedHeight),
            Select("upscale_tile_count", "Tile Count", Defaults.UpscaleTileCount, ["1", "4", "8", "16"]),
            Select("upscale_precision", "Upscale Precision", Defaults.UpscalePrecision, ["auto", "fp32", "fp16", "bf16"]),
            Number("upscale_batch_size", "Upscale Batch Size", 1, 16, 1, Defaults.UpscaleBatchSize),
            Checkbox("upscale_offload_model", "Offload Upscale Model", Defaults.UpscaleOffloadModel),
            Checkbox("upscale_disable_cache", "Disable Upscale Cache", Defaults.UpscaleDisableCache),
            Number("upscale_desired_steps", "Upscale Desired Steps", 1, 100, 1, Defaults.UpscaleDesiredSteps),
            Slider("upscale_strength_percent", "Upscale Strength", 1, 100, 1, Defaults.UpscaleStrengthPercent),
            Number("upscale_seed", "Upscale Seed", -1, null, 1, Defaults.UpscaleSeed),
            new FragmentParameter { Name = "sampler_scheduler", Label = "Sampler Scheduler", Type = ParameterType.Select, Source = new DynamicSource("WanVideoSampler", "scheduler"), DefaultValue = Defaults.SamplerScheduler },
            Slider("sampler_cfg", "Sampler CFG", 0, 20, 0.1, Defaults.SamplerCfg),
            Slider("sampler_shift", "Sampler Shift", 0, 20, 0.1, Defaults.SamplerShift),
            Slider("feta_weight", "Enhance Weight", 0, 10, 0.001, Defaults.FetaWeight),
            Slider("feta_start_percent", "Enhance Start", 0, 1, 0.01, Defaults.FetaStartPercent),
            Slider("feta_end_percent", "Enhance End", 0, 1, 0.01, Defaults.FetaEndPercent),
            Select("text_precision", "Text Precision", Defaults.TextPrecision, ["fp32", "bf16"]),
            Select("text_quantization", "Text Quantization", Defaults.TextQuantization, ["disabled", "fp8_e4m3fn"]),
            Select("text_device", "Text Device", Defaults.TextDevice, ["gpu", "cpu"]),
            Text("prompt_prefix", "Prompt Prefix", Defaults.PromptPrefix, textArea: true),
            Text("negative_prompt", "Negative Prompt", Defaults.NegativePrompt, textArea: true),
            Slider("nag_scale", "NAG Scale", 0, 30, 0.1, Defaults.NagScale),
            Slider("nag_tau", "NAG Tau", 0, 10, 0.1, Defaults.NagTau),
            Slider("nag_alpha", "NAG Alpha", 0, 1, 0.01, Defaults.NagAlpha)
        ]
    };

    public class Parameters
    {
        public int FrameLoadCap { get; set; } = 81;
        public int SkipFirstFrames { get; set; }
        public int SelectEveryNth { get; set; } = 1;
        public int FrameRate { get; set; } = 16;
        public int Crf { get; set; } = 8;
        public int BlocksToSwap { get; set; } = 27;
        public bool UseNonBlocking { get; set; }
        public string BasePrecision { get; set; } = "fp16_fast";
        public string Quantization { get; set; } = "fp8_e4m3fn_scaled";
        public string LoadDevice { get; set; } = "offload_device";
        public string AttentionMode { get; set; } = "sageattn";
        public string VaePrecision { get; set; } = "bf16";
        public double SpeedLoraStrength { get; set; } = 0.7;
        public bool ForceOffload { get; set; }
        public int UpscaleMaxSideLength { get; set; } = 1344;
        public bool UpscaleUseFixedResolution { get; set; }
        public double UpscaleOutputMultiplier { get; set; } = 2;
        public int UpscaleFixedWidth { get; set; } = 1024;
        public int UpscaleFixedHeight { get; set; } = 1024;
        public string UpscaleTileCount { get; set; } = "1";
        public string UpscalePrecision { get; set; } = "fp16";
        public int UpscaleBatchSize { get; set; } = 9;
        public bool UpscaleOffloadModel { get; set; } = true;
        public bool UpscaleDisableCache { get; set; }
        public int UpscaleDesiredSteps { get; set; } = 5;
        public double UpscaleStrengthPercent { get; set; } = 35;
        public long UpscaleSeed { get; set; }
        public string SamplerScheduler { get; set; } = "lcm";
        public double SamplerCfg { get; set; } = 1;
        public double SamplerShift { get; set; } = 5;
        public double FetaWeight { get; set; } = 2;
        public double FetaStartPercent { get; set; }
        public double FetaEndPercent { get; set; } = 1;
        public string TextPrecision { get; set; } = "bf16";
        public string TextQuantization { get; set; } = "disabled";
        public string TextDevice { get; set; } = "gpu";
        public string PromptPrefix { get; set; } = "High resolution, sharp details, soft, low contrast, natural, RAW, ";
        public string NegativePrompt { get; set; } = "bright, high contrast, glossy plastic skin, blurry, oversaturated";
        public double NagScale { get; set; } = 11;
        public double NagTau { get; set; } = 2.5;
        public double NagAlpha { get; set; } = 0.25;
    }

    public Parameters Read(GenerationParameters parameters)
    {
        var fragment = parameters.GetFragment(FragmentId);
        return new Parameters
        {
            FrameLoadCap = fragment?.GetInt("frame_load_cap", Defaults.FrameLoadCap) ?? Defaults.FrameLoadCap,
            SkipFirstFrames = fragment?.GetInt("skip_first_frames", Defaults.SkipFirstFrames) ?? Defaults.SkipFirstFrames,
            SelectEveryNth = fragment?.GetInt("select_every_nth", Defaults.SelectEveryNth) ?? Defaults.SelectEveryNth,
            FrameRate = fragment?.GetInt("frame_rate", Defaults.FrameRate) ?? Defaults.FrameRate,
            Crf = fragment?.GetInt("crf", Defaults.Crf) ?? Defaults.Crf,
            BlocksToSwap = fragment?.GetInt("blocks_to_swap", Defaults.BlocksToSwap) ?? Defaults.BlocksToSwap,
            UseNonBlocking = fragment?.GetBool("use_non_blocking", Defaults.UseNonBlocking) ?? Defaults.UseNonBlocking,
            BasePrecision = fragment?.GetString("base_precision", Defaults.BasePrecision) ?? Defaults.BasePrecision,
            Quantization = fragment?.GetString("quantization", Defaults.Quantization) ?? Defaults.Quantization,
            LoadDevice = fragment?.GetString("load_device", Defaults.LoadDevice) ?? Defaults.LoadDevice,
            AttentionMode = fragment?.GetString("attention_mode", Defaults.AttentionMode) ?? Defaults.AttentionMode,
            VaePrecision = fragment?.GetString("vae_precision", Defaults.VaePrecision) ?? Defaults.VaePrecision,
            SpeedLoraStrength = fragment?.GetDouble("speed_lora_strength", Defaults.SpeedLoraStrength) ?? Defaults.SpeedLoraStrength,
            ForceOffload = fragment?.GetBool("force_offload", Defaults.ForceOffload) ?? Defaults.ForceOffload,
            UpscaleMaxSideLength = fragment?.GetInt("upscale_max_side_length", Defaults.UpscaleMaxSideLength) ?? Defaults.UpscaleMaxSideLength,
            UpscaleUseFixedResolution = fragment?.GetBool("upscale_use_fixed_resolution", Defaults.UpscaleUseFixedResolution) ?? Defaults.UpscaleUseFixedResolution,
            UpscaleOutputMultiplier = fragment?.GetDouble("upscale_output_multiplier", Defaults.UpscaleOutputMultiplier) ?? Defaults.UpscaleOutputMultiplier,
            UpscaleFixedWidth = fragment?.GetInt("upscale_fixed_width", Defaults.UpscaleFixedWidth) ?? Defaults.UpscaleFixedWidth,
            UpscaleFixedHeight = fragment?.GetInt("upscale_fixed_height", Defaults.UpscaleFixedHeight) ?? Defaults.UpscaleFixedHeight,
            UpscaleTileCount = fragment?.GetString("upscale_tile_count", Defaults.UpscaleTileCount) ?? Defaults.UpscaleTileCount,
            UpscalePrecision = fragment?.GetString("upscale_precision", Defaults.UpscalePrecision) ?? Defaults.UpscalePrecision,
            UpscaleBatchSize = fragment?.GetInt("upscale_batch_size", Defaults.UpscaleBatchSize) ?? Defaults.UpscaleBatchSize,
            UpscaleOffloadModel = fragment?.GetBool("upscale_offload_model", Defaults.UpscaleOffloadModel) ?? Defaults.UpscaleOffloadModel,
            UpscaleDisableCache = fragment?.GetBool("upscale_disable_cache", Defaults.UpscaleDisableCache) ?? Defaults.UpscaleDisableCache,
            UpscaleDesiredSteps = fragment?.GetInt("upscale_desired_steps", Defaults.UpscaleDesiredSteps) ?? Defaults.UpscaleDesiredSteps,
            UpscaleStrengthPercent = fragment?.GetDouble("upscale_strength_percent", Defaults.UpscaleStrengthPercent) ?? Defaults.UpscaleStrengthPercent,
            UpscaleSeed = ResolveRuntimeSeed(fragment?.GetLong("upscale_seed", Defaults.UpscaleSeed) ?? Defaults.UpscaleSeed),
            SamplerScheduler = fragment?.GetString("sampler_scheduler", Defaults.SamplerScheduler) ?? Defaults.SamplerScheduler,
            SamplerCfg = fragment?.GetDouble("sampler_cfg", Defaults.SamplerCfg) ?? Defaults.SamplerCfg,
            SamplerShift = fragment?.GetDouble("sampler_shift", Defaults.SamplerShift) ?? Defaults.SamplerShift,
            FetaWeight = fragment?.GetDouble("feta_weight", Defaults.FetaWeight) ?? Defaults.FetaWeight,
            FetaStartPercent = fragment?.GetDouble("feta_start_percent", Defaults.FetaStartPercent) ?? Defaults.FetaStartPercent,
            FetaEndPercent = fragment?.GetDouble("feta_end_percent", Defaults.FetaEndPercent) ?? Defaults.FetaEndPercent,
            TextPrecision = fragment?.GetString("text_precision", Defaults.TextPrecision) ?? Defaults.TextPrecision,
            TextQuantization = fragment?.GetString("text_quantization", Defaults.TextQuantization) ?? Defaults.TextQuantization,
            TextDevice = fragment?.GetString("text_device", Defaults.TextDevice) ?? Defaults.TextDevice,
            PromptPrefix = fragment?.GetString("prompt_prefix", Defaults.PromptPrefix) ?? Defaults.PromptPrefix,
            NegativePrompt = fragment?.GetString("negative_prompt", Defaults.NegativePrompt) ?? Defaults.NegativePrompt,
            NagScale = fragment?.GetDouble("nag_scale", Defaults.NagScale) ?? Defaults.NagScale,
            NagTau = fragment?.GetDouble("nag_tau", Defaults.NagTau) ?? Defaults.NagTau,
            NagAlpha = fragment?.GetDouble("nag_alpha", Defaults.NagAlpha) ?? Defaults.NagAlpha
        };
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
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

    private static FragmentParameter Select(string name, string label, string defaultValue, IEnumerable<string> options) => new()
    {
        Name = name,
        Label = label,
        Type = ParameterType.Select,
        Options = options,
        DefaultValue = defaultValue
    };

    private static FragmentParameter Checkbox(string name, string label, bool defaultValue) => new()
    {
        Name = name,
        Label = label,
        Type = ParameterType.Checkbox,
        DefaultValue = defaultValue
    };

    private static FragmentParameter Text(string name, string label, string defaultValue, bool textArea = false) => new()
    {
        Name = name,
        Label = label,
        Type = textArea ? ParameterType.TextArea : ParameterType.Text,
        DefaultValue = defaultValue
    };
}