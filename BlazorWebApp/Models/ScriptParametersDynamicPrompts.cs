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

        public ScriptParametersDynamicPrompts() { }
        public ScriptParametersDynamicPrompts(ScriptParametersDynamicPrompts clone)
        {
            IsCombinatorial = clone.IsCombinatorial;
            CombinatorialBatches = clone.CombinatorialBatches;
            IsMagicPrompt = clone.IsMagicPrompt;
            IsFeelingLucky = clone.IsFeelingLucky;
            IsAttentionGrabber = clone.IsAttentionGrabber;
            MinAttention = clone.MinAttention;
            MaxAttention = clone.MaxAttention;
            MagicPromptLength = clone.MagicPromptLength;
            MagicPromptCreativity = clone.MagicPromptCreativity;
            UseFixedSeed = clone.UseFixedSeed;
            UnlinkSeedFromPrompt = clone.UnlinkSeedFromPrompt;
            DisableNegativePrompt = clone.DisableNegativePrompt;
            EnableJinjaTemplates = clone.EnableJinjaTemplates;
            NoImageGeneration = clone.NoImageGeneration;
            MaxGenerations = clone.MaxGenerations;
            MagicModel = clone.MagicModel;
            MagicBlocklistRegex = clone.MagicBlocklistRegex;
        }
    }
}
