using BlazorWebApp.Models.Fragments.Attributes;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for SeedVR2 upscaling.
    /// High-quality image upscaling using the SeedVR2 model.
    /// 
    /// Note: Constraints (min/max/step) are defined in the upscale-seedvr2.sbn #meta block.
    /// This class only defines properties and their default values.
    /// </summary>
    public class SeedVR2Fragment : FragmentBase
    {
        public override string FragmentFile => "upscale-seedvr2.sbn";

        /// <summary>
        /// The SeedVR2 DiT model to use.
        /// </summary>
        [JsonPropertyName("seedvr2_model")]
        [DynamicSource("SeedVR2LoadDiTModel", "model")]
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The VAE model for SeedVR2.
        /// </summary>
        [JsonPropertyName("seedvr2_vae_model")]
        [DynamicSource("SeedVR2LoadVAEModel", "model")]
        public string VaeModel { get; set; } = string.Empty;

        /// <summary>
        /// Target resolution (smallest side).
        /// </summary>
        [JsonPropertyName("seedvr2_resolution")]
        public int Resolution { get; set; } = 2048;

        /// <summary>
        /// Batch size for processing.
        /// </summary>
        [JsonPropertyName("seedvr2_batch_size")]
        public int BatchSize { get; set; } = 1;

        /// <summary>
        /// Random seed for generation (-1 for random).
        /// </summary>
        [JsonPropertyName("seedvr2_seed")]
        public long Seed { get; set; } = -1;

        /// <summary>
        /// Number of transformer blocks to swap to CPU for VRAM optimization.
        /// Higher values use less VRAM but are slower.
        /// </summary>
        [JsonPropertyName("blocks_to_swap")]
        public int BlocksToSwap { get; set; } = 36;

        /// <summary>
        /// VAE tile size for processing large images.
        /// </summary>
        [JsonPropertyName("vae_tile_size")]
        public int VaeTileSize { get; set; } = 1024;

        /// <summary>
        /// Overlap between VAE tiles.
        /// </summary>
        [JsonPropertyName("vae_tile_overlap")]
        public int VaeTileOverlap { get; set; } = 128;

        /// <summary>
        /// Noise scale applied to input image.
        /// </summary>
        [JsonPropertyName("seedvr2_input_noise_scale")]
        public float InputNoiseScale { get; set; } = 0f;

        /// <summary>
        /// Noise scale applied in latent space.
        /// </summary>
        [JsonPropertyName("seedvr2_latent_noise_scale")]
        public float LatentNoiseScale { get; set; } = 0f;

        public override FragmentBase Clone(string newId) => new SeedVR2Fragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Model = Model,
            VaeModel = VaeModel,
            Resolution = Resolution,
            BatchSize = BatchSize,
            Seed = Seed,
            BlocksToSwap = BlocksToSwap,
            VaeTileSize = VaeTileSize,
            VaeTileOverlap = VaeTileOverlap,
            InputNoiseScale = InputNoiseScale,
            LatentNoiseScale = LatentNoiseScale
        };
    }
}
