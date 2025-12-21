using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for loading WanVideo models.
    /// Handles model loading, precision, attention mode, and block swapping.
    /// </summary>
    public class WanLoadModelFragment : FragmentBase
    {
        public override string FragmentFile => "wan/load-wan-model.sbn";

        /// <summary>
        /// The WanVideo model to load.
        /// </summary>
        [JsonPropertyName("model_name")]
        [DynamicSource("WanVideoModelLoader", "model")]
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Base precision for the model.
        /// </summary>
        [JsonPropertyName("base_precision")]
        public string BasePrecision { get; set; } = "fp16_fast";

        /// <summary>
        /// Attention mode for processing.
        /// </summary>
        [JsonPropertyName("attention_mode")]
        public string AttentionMode { get; set; } = "sageattn";

        /// <summary>
        /// Number of blocks to swap to CPU for VRAM optimization.
        /// </summary>
        [JsonPropertyName("blocks_to_swap")]
        [Range(0, 40)]
        [Step(1)]
        public int BlocksToSwap { get; set; } = 35;

        /// <summary>
        /// LoRA model to apply.
        /// </summary>
        [JsonPropertyName("lora_name")]
        [DynamicSource("WanVideoLoraSelect", "lora")]
        public string LoraName { get; set; } = string.Empty;

        /// <summary>
        /// LoRA strength.
        /// </summary>
        [JsonPropertyName("lora_strength")]
        [Range(0, 2)]
        [Step(0.1)]
        public float LoraStrength { get; set; } = 1.0f;

        /// <summary>
        /// Scope prefix for scoped model loading (e.g., "high_" or "low_").
        /// </summary>
        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        /// <summary>
        /// Scope title for node naming.
        /// </summary>
        [JsonPropertyName("scope_title")]
        public string? ScopeTitle { get; set; }

        public override FragmentBase Clone(string newId) => new WanLoadModelFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            ModelName = ModelName,
            BasePrecision = BasePrecision,
            AttentionMode = AttentionMode,
            BlocksToSwap = BlocksToSwap,
            LoraName = LoraName,
            LoraStrength = LoraStrength,
            Scope = Scope,
            ScopeTitle = ScopeTitle
        };
    }
}
