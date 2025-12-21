using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for prompt encoding nodes.
    /// Handles positive and negative prompts.
    /// </summary>
    public class PromptsFragment : FragmentBase
    {
        public override string FragmentFile => "prompts.sbn";

        /// <summary>
        /// The positive prompt (what to generate).
        /// </summary>
        [JsonPropertyName("positive")]
        public string Positive { get; set; } = string.Empty;

        /// <summary>
        /// The negative prompt (what to avoid).
        /// Note: Some models (Flux, Qwen) don't use negative prompts.
        /// </summary>
        [JsonPropertyName("negative")]
        public string Negative { get; set; } = string.Empty;

        public override FragmentBase Clone(string newId) => new PromptsFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Positive = Positive,
            Negative = Negative
        };
    }
}
