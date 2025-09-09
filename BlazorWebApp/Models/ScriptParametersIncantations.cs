namespace BlazorWebApp.Models
{
    public class ScriptParametersIncantations : BaseScriptParameters
    {
        public bool IsPAGEnabled { get; set; }
        public float PAGScale { get; set; }
        public bool IsMultiConceptEnabled { get; set; }
        public bool UNK1 { get; set; }
        public int CorrectionSize { get; set; }
        public float SuppresionAlpha { get; set; }
        public float CbSScoreThreshold { get; set; }
        public float CbSCorrectionStrength { get; set; }
        public string UNK2 { get; set; }
        public float EMAFactor { get; set; }
        public int StepEnd { get; set; }
        public bool IsSeekEnabled { get; set; }
        public bool AppendGenCaption { get; set; }
        public bool DeepbooruInterrogate { get; set; }
        public string Delimiter { get; set; }
        public string WordReplacement { get; set; }
        public float Gamma { get; set; }
        public int UNK3 { get; set; }
    }
}
