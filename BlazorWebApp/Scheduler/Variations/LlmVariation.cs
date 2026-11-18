namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces <see cref="Count"/> prompt variations by asking Ollama to generate alternatives of
    /// <see cref="BasePrompt"/>, optionally guided by a system-prompt template selected from the
    /// Prompts page (see <see cref="SystemPromptTemplateId"/>). Materialization is pipelined with
    /// image generation by the Scheduler (Phase 4) to hide latency.
    /// </summary>
    public sealed class LlmVariation : Variation
    {
        /// <summary>
        /// Ollama model name used for generation.
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// The base prompt the LLM is asked to create variations of.
        /// </summary>
        public string BasePrompt { get; set; } = string.Empty;

        /// <summary>
        /// Optional id of a `SystemPromptTemplate` (from the Prompts page) whose messages are used
        /// as the conversation seed. When null, the default <see cref="Services.OllamaService.ExpandPrompt"/>
        /// path is used.
        /// </summary>
        public int? SystemPromptTemplateId { get; set; }

        /// <summary>
        /// Number of variations to produce.
        /// </summary>
        public int Count { get; set; } = 1;

        /// <summary>
        /// When <c>true</c>, generated text is applied to the negative prompt.
        /// </summary>
        public bool IsNegative { get; set; }
    }
}
