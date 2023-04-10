namespace BlazorWebApp.Models
{
    public class ScriptParametersRegionalPrompter : BaseScriptParameters
    {
        public bool IsDebug { get; set; }
        public string Mode { get; set; }
        public string DivideRatio { get; set; }
        public string BaseRatio { get; set; }
        public bool UseBasePrompt { get; set; }
        public bool UseCommonPrompt { get; set; }
        public bool UseNegativeCommonPrompt { get; set; }
        public string GenerationMode { get; set; }
        public bool DisableConvertAND { get; set; }
    }
}
