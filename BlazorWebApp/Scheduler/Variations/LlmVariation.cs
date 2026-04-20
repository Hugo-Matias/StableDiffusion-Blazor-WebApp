namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces <see cref="Count"/> prompt variations by asking Ollama to generate alternatives of
    /// <see cref="BasePrompt"/>, optionally guided by <see cref="SystemPrompt"/>. Materialization
    /// is pipelined with image generation by the Scheduler (Phase 4) to hide latency.
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
        /// Optional system prompt guiding the LLM. When null, a default is used.
        /// </summary>
        public string? SystemPrompt { get; set; }

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
