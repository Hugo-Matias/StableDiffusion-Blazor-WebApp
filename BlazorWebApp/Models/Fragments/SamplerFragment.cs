using BlazorWebApp.Models.Fragments.Attributes;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for KSampler nodes.
    /// Handles sampling parameters like steps, seed, cfg, scheduler, etc.
    /// 
    /// Note: Constraints (min/max/step) are defined in the sampler.sbn #meta block.
    /// This class only defines properties and their default values.
    /// </summary>
    public class SamplerFragment : FragmentBase
    {
        public override string FragmentFile => "sampler.sbn";

        [JsonPropertyName("sampler_name")]
        [DynamicSource("Backend.Samplers")]
        public string SamplerName { get; set; } = "euler";

        [JsonPropertyName("scheduler")]
        [DynamicSource("Backend.Schedulers")]
        public string Scheduler { get; set; } = "normal";

        [JsonPropertyName("steps")]
        public int Steps { get; set; } = 20;

        [JsonPropertyName("seed")]
        public long Seed { get; set; } = -1;

        [JsonPropertyName("cfg")]
        public float Cfg { get; set; } = 7.0f;

        [JsonPropertyName("denoise")]
        public float Denoise { get; set; } = 1.0f;

        /// <summary>
        /// Guidance value for Flux and other distilled models.
        /// Used instead of CFG for certain model types.
        /// </summary>
        [JsonPropertyName("guidance")]
        public float Guidance { get; set; } = 3.5f;

        /// <summary>
        /// Eta parameter for certain samplers (e.g., DDIM).
        /// </summary>
        [JsonPropertyName("eta")]
        public float Eta { get; set; } = 1.0f;

        public override FragmentBase Clone(string newId) => new SamplerFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            SamplerName = SamplerName,
            Scheduler = Scheduler,
            Steps = Steps,
            Seed = Seed,
            Cfg = Cfg,
            Denoise = Denoise,
            Guidance = Guidance,
            Eta = Eta
        };
    }
}
