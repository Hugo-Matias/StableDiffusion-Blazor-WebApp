namespace BlazorWebApp.Services.WildcardForge.Models
{
    /// <summary>
    /// Loaded form of a single template from <c>LLM_PROMPTS.json</c>.
    /// The system/user prompt strings retain their <c>{placeholder}</c> tokens for slot filling
    /// at compose time.
    /// </summary>
    public class LlmTemplate
    {
        public string Key { get; set; } = string.Empty; // e.g. "basic_generation"
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPrompt { get; set; } = string.Empty;
        public float? RecommendedTemperature { get; set; }
        public float? RecommendedTopP { get; set; }
        public int? RecommendedMaxTokens { get; set; }
    }
}
