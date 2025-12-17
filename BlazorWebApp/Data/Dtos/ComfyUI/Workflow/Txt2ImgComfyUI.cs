using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow
{
    public class Txt2ImgComfyUI : SharedComfyUI
    {
        public string? Model { get; set; }
        public string? Vae { get; set; }
        public string? Clip1 { get; set; }
        public string? Clip2 { get; set; }
        public List<Lora>? Loras { get; set; }
        public UpscaleParameters? Upscale { get; set; }
        public DetailerParameters? Detailer { get; set; }
        public SeedVR2Parameters SeedVR2 { get; set; } = new();
        public ConditioningVariationParameters ConditioningVariation { get; set; } = new();
        public SeedVarianceEnhancerParameters SeedVarianceEnhancer { get; set; } = new();
    }

    public class UpscaleParameters
    {
        public bool? IsActive { get; set; }
        public string? Model { get; set; }
        public double? Mult { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public double? Denoise { get; set; }
        public int? Steps { get; set; }
    }

    public class DetailerParameters
    {
        public bool? IsActive { get; set; }
        public string? Model { get; set; }
        public string? Sampler { get; set; }
        public string? Scheduler { get; set; }
        public string? Checkpoint { get; set; }
        public string? Prompt { get; set; }
        public string? NegativePrompt { get; set; }
        public int? Steps { get; set; }
        public double? CfgScale { get; set; }
        public int? Feather { get; set; }
        public double? Denoise { get; set; }
        public double? BBoxThreshold { get; set; }
        public int? DropSize { get; set; }
        public long? Seed { get; set; }
        public int? GuideSize { get; set; }
        public int? MaxSize { get; set; }
        public int? BBoxDilation { get; set; }
        public double? BBoxCropFactor { get; set; }
        public int? Cycle { get; set; }
        public List<Lora> Loras { get; set; }
    }

    public class SeedVR2Parameters
    {
        public bool IsActive { get; set; }
        public string? Model { get; set; }
        public string? VaeModel { get; set; }
        public int? BlocksToSwap { get; set; }
        public int? VaeTileSize { get; set; }
        public int? VaeTileOverlap { get; set; }
        public int? Resolution { get; set; }
        public double? Scale { get; set; }
        public int? BatchSize { get; set; }
        public double? InputNoiseScale { get; set; }
        public double? LatentNoiseScale { get; set; }
    }

    public class ConditioningVariationParameters
    {
        public bool IsActive { get; set; }
        public double? SwitchPoint { get; set; }
    }

    public class SeedVarianceEnhancerParameters
    {
        public bool IsActive { get; set; }
        public int? RandomizePercent { get; set; }
        public int? Strength { get; set; }
        public string? NoiseInsert { get; set; }
        public int? StepsSwitchoverPercent { get; set; }
        public long? Seed { get; set; }
        public string? MaskStartsAt { get; set; }
        public int? MaskPercent { get; set; }
        public bool? LogToConsole { get; set; }
    }
}
