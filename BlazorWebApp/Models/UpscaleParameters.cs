namespace BlazorWebApp.Models
{
    public class UpscaleParameters : SharedParameters
    {
        public int ResizeMode { get; set; }
        public bool ShowResults { get; set; }
        public double GfpganVisibility { get; set; }
        public double CodeformerVisibility { get; set; }
        public double CodeformerWeight { get; set; }
        public double UpscalingMultiplier { get; set; }
        public int UpscalingWidth { get; set; }
        public int UpscalingHeight { get; set; }
        public bool UpscalingCrop { get; set; }
        public string UpscalerPrimary { get; set; }
        public string UpscalerSecondary { get; set; }
        public double UpscalerSecondaryVisibility { get; set; }
        public bool UpscalePriority { get; set; }
        public string Image { get; set; }

        public UpscaleParameters() { }

        public UpscaleParameters(SharedParameters clone)
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
        }
    }
}
