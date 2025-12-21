using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for WanVideo prompt encoding.
    /// Handles positive and negative prompts for video generation.
    /// </summary>
    public class WanPromptsFragment : FragmentBase
    {
        public override string FragmentFile => "wan/prompts.sbn";

        /// <summary>
        /// The positive prompt (what to generate).
        /// </summary>
        [JsonPropertyName("positive")]
        public string Positive { get; set; } = string.Empty;

        /// <summary>
        /// The negative prompt (what to avoid).
        /// </summary>
        [JsonPropertyName("negative")]
        public string Negative { get; set; } = string.Empty;

        public override FragmentBase Clone(string newId) => new WanPromptsFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Positive = Positive,
            Negative = Negative
        };
    }
}
