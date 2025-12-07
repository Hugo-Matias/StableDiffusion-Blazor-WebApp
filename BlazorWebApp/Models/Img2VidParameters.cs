using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Parameters for Image-to-Video generation
    /// </summary>
    public class Img2VidParameters
    {
        public ComfyImg2VidParameters Comfy { get; set; } = new();
        public List<Lora> Loras { get; set; } = new();
        
        /// <summary>
        /// Dynamic asset storage for workflow-specific models.
        /// Key: Asset parameter name from WorkflowAsset.Parameter (e.g., "HighModel", "LowModel", "Vae", "Clip", "ClipVision")
        /// Value: Selected asset filename
        /// </summary>
        public Dictionary<string, string> WorkflowAssets { get; set; } = new();

        // Core generation parameters
        public string? Prompt { get; set; }
        public string? NegativePrompt { get; set; }
        public long? Seed { get; set; } = -1;
        public int? Steps { get; set; } = 8;
        public float? CfgScale { get; set; } = 1;
        public string? SamplerName { get; set; } = "euler";
        public string? Scheduler { get; set; } = "simple";
        public int? Width { get; set; } = 768;
        public int? Height { get; set; } = 768;
        public int? BatchSize { get; set; } = 1;

        // Video-specific parameters
        public string? Image { get; set; }
        public int? Length { get; set; } = 81;
        public float? MotionAmplitude { get; set; } = 1.1f;
        public int? Shift { get; set; } = 5;
        public int? FrameRate { get; set; } = 16;

        // Frame interpolation
        public FrameInterpolationParameters FrameInterpolation { get; set; } = new();

        public Img2VidParameters() { }

        public Img2VidParameters(Img2VidParameters clone)
        {
            Comfy = clone.Comfy;
            Loras = clone.Loras?.Select(l => new Lora(l)).ToList() ?? new List<Lora>();
            WorkflowAssets = clone.WorkflowAssets != null 
                ? new Dictionary<string, string>(clone.WorkflowAssets) 
                : new Dictionary<string, string>();
            Prompt = clone.Prompt;
            NegativePrompt = clone.NegativePrompt;
            Seed = clone.Seed;
            Steps = clone.Steps;
            CfgScale = clone.CfgScale;
            SamplerName = clone.SamplerName;
            Scheduler = clone.Scheduler;
            Width = clone.Width;
            Height = clone.Height;
            BatchSize = clone.BatchSize;
            Image = clone.Image;
            Length = clone.Length;
            MotionAmplitude = clone.MotionAmplitude;
            Shift = clone.Shift;
            FrameRate = clone.FrameRate;
            FrameInterpolation = new FrameInterpolationParameters
            {
                IsActive = clone.FrameInterpolation?.IsActive,
                ScaleBy = clone.FrameInterpolation?.ScaleBy,
                Multiplier = clone.FrameInterpolation?.Multiplier,
                RifeModel = clone.FrameInterpolation?.RifeModel
            };
        }

        /// <summary>
        /// Converts to Img2VidComfyUI DTO for workflow rendering
        /// </summary>
        public Img2VidComfyUI ToComfyUI()
        {
            return new Img2VidComfyUI
            {
                Prompt = Prompt,
                NegativePrompt = NegativePrompt,
                Seed = Seed,
                Steps = Steps,
                CfgScale = CfgScale,
                SamplerName = SamplerName,
                Scheduler = Scheduler,
                Width = Width,
                Height = Height,
                BatchSize = BatchSize,
                Image = Image,
                HighModel = WorkflowAssets?.GetValueOrDefault("HighModel"),
                LowModel = WorkflowAssets?.GetValueOrDefault("LowModel"),
                Clip = WorkflowAssets?.GetValueOrDefault("Clip"),
                ClipVision = WorkflowAssets?.GetValueOrDefault("ClipVision"),
                Vae = WorkflowAssets?.GetValueOrDefault("Vae"),
                Loras = Loras?.Where(l => l.IsEnabled && !l.IsNegative).ToList(),
                Length = Length,
                MotionAmplitude = MotionAmplitude,
                Shift = Shift,
                FrameRate = FrameRate,
                FrameInterpolation = FrameInterpolation
            };
        }

        public class ComfyImg2VidParameters
        {
            public Workflow? Workflow { get; set; }
        }
    }
}
