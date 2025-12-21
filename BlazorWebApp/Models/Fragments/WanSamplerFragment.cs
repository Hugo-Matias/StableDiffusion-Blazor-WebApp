using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for WanVideo sampling.
    /// Handles video generation sampling parameters.
    /// </summary>
    public class WanSamplerFragment : FragmentBase
    {
        public override string FragmentFile => "wan/sampler-wan.sbn";

        /// <summary>
        /// Scheduler for video sampling.
        /// </summary>
        [JsonPropertyName("scheduler")]
        [DynamicSource("Backend.Schedulers")]
        public string Scheduler { get; set; } = "dpm++_sde";

        /// <summary>
        /// Number of sampling steps.
        /// </summary>
        [JsonPropertyName("steps")]
        [Range(1, 100)]
        [Step(1)]
        public int Steps { get; set; } = 30;

        /// <summary>
        /// Random seed for generation (-1 for random).
        /// </summary>
        [JsonPropertyName("seed")]
        public long Seed { get; set; } = -1;

        /// <summary>
        /// CFG scale for video generation.
        /// </summary>
        [JsonPropertyName("cfg")]
        [Range(1, 20)]
        [Step(0.5)]
        public float Cfg { get; set; } = 1.0f;

        /// <summary>
        /// Shift value for the scheduler.
        /// </summary>
        [JsonPropertyName("shift")]
        [Range(1, 15)]
        [Step(0.5)]
        public float Shift { get; set; } = 5.0f;

        /// <summary>
        /// Denoise strength.
        /// </summary>
        [JsonPropertyName("denoise")]
        [Range(0, 1)]
        [Step(0.01)]
        public float Denoise { get; set; } = 1.0f;

        public override FragmentBase Clone(string newId) => new WanSamplerFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            Scheduler = Scheduler,
            Steps = Steps,
            Seed = Seed,
            Cfg = Cfg,
            Shift = Shift,
            Denoise = Denoise
        };
    }
}
