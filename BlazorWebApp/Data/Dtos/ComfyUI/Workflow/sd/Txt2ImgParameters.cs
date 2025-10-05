namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow.sd
{
    public class Txt2ImgParameters : SharedParameters
    {
        public string? Model { get; set; }
        public string? VAE { get; set; }
        public string? Clip1 { get; set; }
        public string? Clip2 { get; set; }
        public UpscaleParameters? Upscale { get; set; }
        public DetailerParameters? Detailer { get; set; }
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
    }
}
