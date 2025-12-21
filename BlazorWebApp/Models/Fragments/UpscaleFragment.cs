using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for image upscaling with model-based upscalers.
    /// Second-pass upscale with optional re-sampling.
    /// </summary>
    public class UpscaleFragment : FragmentBase
    {
        public override string FragmentFile => "upscale.sbn";

        /// <summary>
        /// The upscale model to use.
        /// </summary>
        [JsonPropertyName("upscale_model")]
        [DynamicSource("Backend.Upscalers")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Target width after upscaling.
        /// </summary>
        [JsonPropertyName("upscale_width")]
        [Range(64, 4096)]
        [Step(8)]
        public int Width { get; set; } = 1744;

        /// <summary>
        /// Target height after upscaling.
        /// </summary>
        [JsonPropertyName("upscale_height")]
        [Range(64, 4096)]
        [Step(8)]
        public int Height { get; set; } = 2496;

        /// <summary>
        /// Scale multiplier (alternative to explicit dimensions).
        /// </summary>
        [JsonPropertyName("upscale_scale")]
        [Range(1, 4)]
        [Step(0.25)]
        public float Scale { get; set; } = 2.0f;

        /// <summary>
        /// Number of sampling steps for the upscale pass.
        /// 0 means use original steps.
        /// </summary>
        [JsonPropertyName("upscale_steps")]
        [Range(0, 150)]
        [Step(1)]
        public int Steps { get; set; } = 20;

        /// <summary>
        /// Denoise strength for the upscale pass.
        /// </summary>
        [JsonPropertyName("upscale_denoise")]
        [Range(0, 1)]
        [Step(0.01)]
        public float Denoise { get; set; } = 0.5f;

        public override FragmentBase Clone(string newId) => new UpscaleFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Model = Model,
            Width = Width,
            Height = Height,
            Scale = Scale,
            Steps = Steps,
            Denoise = Denoise
        };
    }
}
