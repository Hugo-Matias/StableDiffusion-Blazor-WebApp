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

        // Frame interpolation (simple inline version)
        public bool? FrameInterpolationActive { get; set; } = true;
        public double? FrameInterpolationScaleBy { get; set; } = 2.0;
        public int? FrameInterpolationMultiplier { get; set; } = 2;
        public string? FrameInterpolationRifeModel { get; set; } = "rife49.pth";

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
            FrameInterpolationActive = clone.FrameInterpolationActive;
            FrameInterpolationScaleBy = clone.FrameInterpolationScaleBy;
            FrameInterpolationMultiplier = clone.FrameInterpolationMultiplier;
            FrameInterpolationRifeModel = clone.FrameInterpolationRifeModel;
        }

        public class ComfyImg2VidParameters
        {
            public Workflow? Workflow { get; set; }
        }
    }
}
