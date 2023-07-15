namespace BlazorWebApp.Models
{
    public class ScriptParametersRegionalPrompter : BaseScriptParameters
    {
        public bool IsDebug { get; set; }
        public string SelectedTab { get; set; } = "Matrix";
        public string MatrixMode { get; set; }
        public string MaskMode { get; set; }
        public string PromptMode { get; set; }
        public string DivideRatio { get; set; }
        public string BaseRatio { get; set; }
        public bool UseBasePrompt { get; set; }
        public bool UseCommonPrompt { get; set; }
        public bool UseNegativeCommonPrompt { get; set; }
        public string GenerationMode { get; set; }
        public bool DisableConvertAND { get; set; }
        public string LoraNegTeRatios { get; set; }
        public string LoraNegURatios { get; set; }
        public float PromptThreshold { get; set; }
        public string Polymask { get; set; }
    }
}
