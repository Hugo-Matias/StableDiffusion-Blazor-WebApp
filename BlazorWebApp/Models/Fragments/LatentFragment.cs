using BlazorWebApp.Models.Fragments.Attributes;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for empty latent image generation.
    /// Defines the output resolution and batch size.
    /// 
    /// Note: Constraints (min/max/step) are defined in the empty-latent.sbn #meta block.
    /// This class only defines properties and their default values.
    /// </summary>
    public class LatentFragment : FragmentBase
    {
        public override string FragmentFile => "empty-latent.sbn";

        /// <summary>
        /// Output image width in pixels.
        /// </summary>
        [JsonPropertyName("width")]
        public int Width { get; set; } = 1024;

        /// <summary>
        /// Output image height in pixels.
        /// </summary>
        [JsonPropertyName("height")]
        public int Height { get; set; } = 1024;

        /// <summary>
        /// Number of images to generate in a single batch.
        /// </summary>
        [JsonPropertyName("batch_size")]
        public int BatchSize { get; set; } = 1;

        public override FragmentBase Clone(string newId) => new LatentFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Width = Width,
            Height = Height,
            BatchSize = BatchSize
        };
    }
}
