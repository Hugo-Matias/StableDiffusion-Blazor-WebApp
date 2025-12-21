using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for seed variance enhancement.
    /// Adds noise to prompt embeddings to create variation while maintaining concept.
    /// </summary>
    public class SeedVarianceEnhancerFragment : FragmentBase
    {
        public override string FragmentFile => "seed-variance-enhancer.sbn";

        /// <summary>
        /// Percentage of embedding values to randomize.
        /// </summary>
        [JsonPropertyName("randomize_percent")]
        [Range(0, 100)]
        [Step(5)]
        public int RandomizePercent { get; set; } = 50;

        /// <summary>
        /// Strength of the noise applied.
        /// </summary>
        [JsonPropertyName("strength")]
        [Range(0, 100)]
        [Step(1)]
        public int Strength { get; set; } = 20;

        /// <summary>
        /// When to insert noise during generation.
        /// Options: "noise on beginning steps", "noise on ending steps", "noise on all steps", "disabled"
        /// </summary>
        [JsonPropertyName("noise_insert")]
        public string NoiseInsert { get; set; } = "noise on beginning steps";

        /// <summary>
        /// Percentage of steps after which noise is removed.
        /// </summary>
        [JsonPropertyName("steps_switchover_percent")]
        [Range(0, 100)]
        [Step(5)]
        public int StepsSwitchoverPercent { get; set; } = 20;

        /// <summary>
        /// Random seed for the enhancer.
        /// </summary>
        [JsonPropertyName("seed")]
        public long Seed { get; set; } = 0;

        /// <summary>
        /// Where masking starts: "beginning" or "end".
        /// </summary>
        [JsonPropertyName("mask_starts_at")]
        public string MaskStartsAt { get; set; } = "beginning";

        /// <summary>
        /// Percentage of embeddings to mask.
        /// </summary>
        [JsonPropertyName("mask_percent")]
        [Range(0, 100)]
        [Step(5)]
        public int MaskPercent { get; set; } = 0;

        /// <summary>
        /// Whether to log debug info to console.
        /// </summary>
        [JsonPropertyName("log_to_console")]
        public bool LogToConsole { get; set; } = false;

        public override FragmentBase Clone(string newId) => new SeedVarianceEnhancerFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            RandomizePercent = RandomizePercent,
            Strength = Strength,
            NoiseInsert = NoiseInsert,
            StepsSwitchoverPercent = StepsSwitchoverPercent,
            Seed = Seed,
            MaskStartsAt = MaskStartsAt,
            MaskPercent = MaskPercent,
            LogToConsole = LogToConsole
        };
    }
}
