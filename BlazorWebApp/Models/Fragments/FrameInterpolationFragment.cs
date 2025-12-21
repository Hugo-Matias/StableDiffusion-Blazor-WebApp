using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for frame interpolation using RIFE.
    /// Increases video frame rate by interpolating between frames.
    /// </summary>
    public class FrameInterpolationFragment : FragmentBase
    {
        public override string FragmentFile => "wan/frame-interpolation.sbn";

        /// <summary>
        /// The RIFE model to use for interpolation.
        /// </summary>
        [JsonPropertyName("rife_model")]
        [DynamicSource("RIFE VFI", "ckpt_name")]
        public string RifeModel { get; set; } = "rife49.pth";

        /// <summary>
        /// Frame rate multiplier (2x, 3x, 4x, etc.).
        /// </summary>
        [JsonPropertyName("frame_multiplier")]
        [Range(2, 8)]
        [Step(1)]
        public int Multiplier { get; set; } = 2;

        /// <summary>
        /// Scale factor for upscaling frames before interpolation.
        /// </summary>
        [JsonPropertyName("scale_by")]
        [Range(1, 4)]
        [Step(0.5)]
        public float ScaleBy { get; set; } = 2.0f;

        public override FragmentBase Clone(string newId) => new FrameInterpolationFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            RifeModel = RifeModel,
            Multiplier = Multiplier,
            ScaleBy = ScaleBy
        };
    }
}
