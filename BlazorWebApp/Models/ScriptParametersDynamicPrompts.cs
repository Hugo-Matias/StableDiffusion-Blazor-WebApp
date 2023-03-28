namespace BlazorWebApp.Models
{
    public class ScriptParametersDynamicPrompts : BaseScriptParameters
    {
        // Properties will be passed to the payload as an object[] and order is important!
        public bool IsCombinatorial { get; set; }
        public int CombinatorialBatches { get; set; }
        public bool IsMagicPrompt { get; set; }
        public bool IsFeelingLucky { get; set; }
        public bool IsAttentionGrabber { get; set; }
        public float MinAttention { get; set; }
        public float MaxAttention { get; set; }
        public int MagicPromptLength { get; set; }
        public float MagicPromptCreativity { get; set; }
        public bool UseFixedSeed { get; set; }
        public bool UnlinkSeedFromPrompt { get; set; }
        public bool DisableNegativePrompt { get; set; }
        public bool EnableJinjaTemplates { get; set; }
        public bool NoImageGeneration { get; set; }
        public int MaxGenerations { get; set; }
        public string MagicModel { get; set; }
        public string? MagicBlocklistRegex { get; set; }
        public int MagicBatchSize { get; set; }
    }
}
