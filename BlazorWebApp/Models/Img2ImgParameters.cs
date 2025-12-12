using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;

namespace BlazorWebApp.Models
{
    public class Img2ImgParameters : SharedParameters
    {
        // WebUI-specific properties
        public List<string> InitImages { get; set; }
        public string Mask { get; set; }
        public int MaskBlur { get; set; }
        public int ResizeMode { get; set; }
        public int InpaintingFill { get; set; }
        public bool InpaintFullRes { get; set; }
        public int InpaintFullResPadding { get; set; }
        public int InpaintingMaskInvert { get; set; }

        // ComfyUI-specific properties
        public string? Image { get; set; }
        public float? Megapixels { get; set; } = 1;
        public string? LightningLora { get; set; }
        public float? LoraStrength { get; set; } = 1;
        public int? ModelShift { get; set; } = 3;
        public float? CfgNormStrength { get; set; } = 1;
        public float? Denoise { get; set; } = 1;

        public Img2ImgParameters() { }
        public Img2ImgParameters(SharedParameters clone)
        {
            Comfy = clone.Comfy;
            Loras = clone.Loras;
            WorkflowAssets = clone.WorkflowAssets != null 
                ? new Dictionary<string, string>(clone.WorkflowAssets) 
                : new Dictionary<string, string>();
            DenoisingStrength = clone.DenoisingStrength;
            Prompt = clone.Prompt;
            Styles = clone.Styles;
            Seed = clone.Seed;
            Subseed = clone.Subseed;
            SubseedStrength = clone.SubseedStrength;
            SeedResizeFromH = clone.SeedResizeFromH;
            SeedResizeFromW = clone.SeedResizeFromW;
            BatchSize = clone.BatchSize;
            SamplerName = clone.SamplerName;
            Scheduler = clone.Scheduler;
            NIter = clone.NIter;
            Steps = clone.Steps;
            CfgScale = clone.CfgScale;
            Width = clone.Width;
            Height = clone.Height;
            RestoreFaces = clone.RestoreFaces;
            Tiling = clone.Tiling;
            NegativePrompt = clone.NegativePrompt;
            Eta = clone.Eta;
            SChurn = clone.SChurn;
            STmax = clone.STmax;
            STmin = clone.STmin;
            SNoise = clone.SNoise;
            SamplerIndex = clone.SamplerIndex;
            RefinerCheckpoint = clone.RefinerCheckpoint;
            RefinerSwitchAt = clone.RefinerSwitchAt;
            AlwaysOnScripts = clone.AlwaysOnScripts;
            ScriptName = clone.ScriptName;
            ScriptArgs = clone.ScriptArgs;
        }

        /// <summary>
        /// Converts to Img2ImgComfyUI DTO for workflow rendering
        /// </summary>
        public Img2ImgComfyUI ToComfyUI()
        {
            return new Img2ImgComfyUI
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
                Model = WorkflowAssets?.GetValueOrDefault("Model"),
                Clip = WorkflowAssets?.GetValueOrDefault("Clip"),
                Vae = WorkflowAssets?.GetValueOrDefault("Vae"),
                Megapixels = Megapixels,
                LightningLora = LightningLora,
                LoraStrength = LoraStrength,
                ModelShift = ModelShift,
                CfgNormStrength = CfgNormStrength,
                Denoise = Denoise,
                Loras = Loras?.Where(l => l.IsEnabled && !l.IsNegative).ToList()
            };
        }
    }
}
