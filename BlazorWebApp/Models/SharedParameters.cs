namespace BlazorWebApp.Models
{
    public class SharedParameters
    {
        public ComfySharedParameters Comfy { get; set; }
        public List<Lora> Loras { get; set; }
        public double? DenoisingStrength { get; set; }
        public string? Prompt { get; set; }
        public string[]? Styles { get; set; }
        public long? Seed { get; set; }
        public long? Subseed { get; set; }
        public float? SubseedStrength { get; set; }
        public int? SeedResizeFromH { get; set; }
        public int? SeedResizeFromW { get; set; }
        public string? SamplerName { get; set; }
        public string? Scheduler { get; set; }
        public int? BatchSize { get; set; }
        public int? NIter { get; set; }
        public int? Steps { get; set; }
        public float? CfgScale { get; set; }
        public float? DistilledCfgScale { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool? RestoreFaces { get; set; }
        public bool? Tiling { get; set; }
        public string? NegativePrompt { get; set; }
        public float? Eta { get; set; }
        public float? SChurn { get; set; }
        public float? STmax { get; set; }
        public float? STmin { get; set; }
        public float? SNoise { get; set; }
        public string? SamplerIndex { get; set; }
        public string? RefinerCheckpoint { get; set; }
        public float? RefinerSwitchAt { get; set; }
        public Dictionary<string, object>? AlwaysOnScripts { get; set; }
        public string? ScriptName { get; set; }
        public object? ScriptArgs { get; set; }

        public SharedParameters() { }
        public SharedParameters(Txt2ImgParameters clone)
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
            RefinerCheckpoint = clone.RefinerCheckpoint == "None" ? null : clone.RefinerCheckpoint;
            RefinerSwitchAt = clone.RefinerSwitchAt;
        }
        public SharedParameters(Img2ImgParameters clone)
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
            RefinerCheckpoint = clone.RefinerCheckpoint == "None" ? null : clone.RefinerCheckpoint;
            RefinerSwitchAt = clone.RefinerSwitchAt;
        }

        public class ComfySharedParameters
        {
            public Workflow Workflow { get; set; }
        }
    }
}
