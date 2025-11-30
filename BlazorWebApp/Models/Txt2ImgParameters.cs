using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Dtos.WebUI;

namespace BlazorWebApp.Models
{
    public class Txt2ImgParameters : SharedParameters
    {
        public bool? EnableHR { get; set; }
        public int? FirstphaseWidth { get; set; }
        public int? FirstphaseHeight { get; set; }
        public double HRScale { get; set; }
        public string HRUpscaler { get; set; }
        public int HRSecondPassSteps { get; set; }
        public int HRWidth { get; set; }
        public int HRHeight { get; set; }
        public SeedVR2Parameters SeedVR2 { get; set; }
        public ConditioningVariationParameters ConditioningVariation { get; set; }
        public Txt2ImgScriptParameters Scripts { get; set; }

        public Txt2ImgParameters() { }
        public Txt2ImgParameters(SharedParameters clone)
        {
            Comfy = clone.Comfy;
            Loras = clone.Loras;
            DenoisingStrength = clone.DenoisingStrength;
            Prompt = clone.Prompt;
            Styles = clone.Styles;
            Seed = clone.Seed;
            Subseed = clone.Subseed;
            SubseedStrength = clone.SubseedStrength;
            SeedResizeFromH = clone.SeedResizeFromH;
            SeedResizeFromW = clone.SeedResizeFromW;
            SamplerName = clone.SamplerName;
            Scheduler = clone.Scheduler;
            BatchSize = clone.BatchSize;
            NIter = clone.NIter;
            Steps = clone.Steps;
            CfgScale = clone.CfgScale;
            DistilledCfgScale = clone.DistilledCfgScale;
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

    public class Txt2ImgScriptParameters
    {
        public List<ScriptParametersControlNet> ControlNet { get; set; }
        public ScriptParametersCutoff Cutoff { get; set; }
        public ScriptParametersDynamicPrompts DynamicPrompts { get; set; }
        public ScriptParametersMultiDiffusionTiledDiffusion MultiDiffusionTiledDiffusion { get; set; }
        public ScriptParametersMultiDiffusionTiledVae MultiDiffusionTiledVae { get; set; }
        public ScriptParametersRegionalPrompter RegionalPrompter { get; set; }
        public ScriptParametersXYZPlot XYZPlot { get; set; }
        public ScriptParametersADetailer ADetailer { get; set; }
        public ScriptParametersIncantations Incantations { get; set; }
    }
}
