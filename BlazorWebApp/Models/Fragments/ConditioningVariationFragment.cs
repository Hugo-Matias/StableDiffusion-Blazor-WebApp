using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for conditioning variation.
    /// Adds variety by switching from empty prompt to actual prompt mid-generation.
    /// </summary>
    public class ConditioningVariationFragment : FragmentBase
    {
        public override string FragmentFile => "conditioning-variation.sbn";

        /// <summary>
        /// Point at which to switch from empty prompt to actual prompt.
        /// Value of 0.2 means empty prompt for first 20% of steps.
        /// </summary>
        [JsonPropertyName("switch_point")]
        [Range(0, 1)]
        [Step(0.05)]
        public float SwitchPoint { get; set; } = 0.2f;

        public override FragmentBase Clone(string newId) => new ConditioningVariationFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            SwitchPoint = SwitchPoint
        };
    }
}
