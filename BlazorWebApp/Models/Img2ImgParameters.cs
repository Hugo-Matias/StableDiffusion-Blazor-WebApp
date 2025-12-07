using BlazorWebApp.Data.Dtos.WebUI;

namespace BlazorWebApp.Models
{
    public class Img2ImgParameters : SharedParameters
    {
        public List<string> InitImages { get; set; }
        public string Mask { get; set; }
        public int MaskBlur { get; set; }
        public int ResizeMode { get; set; }
        public int InpaintingFill { get; set; }
        public bool InpaintFullRes { get; set; }
        public int InpaintFullResPadding { get; set; }
        public int InpaintingMaskInvert { get; set; }
        public Img2ImgScriptParameters Scripts { get; set; }

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
    }

    public class Img2ImgScriptParameters
    {
        public List<ScriptParametersControlNet> ControlNet { get; set; }
        public ScriptParametersCutoff Cutoff { get; set; }
        public ScriptParametersDynamicPrompts DynamicPrompts { get; set; }
        public ScriptParametersUltimateUpscale UltimateUpscale { get; set; }
        public ScriptParametersMultiDiffusionTiledDiffusion MultiDiffusionTiledDiffusion { get; set; }
        public ScriptParametersMultiDiffusionTiledVae MultiDiffusionTiledVae { get; set; }
        public ScriptParametersRegionalPrompter RegionalPrompter { get; set; }
        public ScriptParametersXYZPlot XYZPlot { get; set; }
        public ScriptParametersADetailer ADetailer { get; set; }
        public ScriptParametersIncantations Incantations { get; set; }
    }
}
