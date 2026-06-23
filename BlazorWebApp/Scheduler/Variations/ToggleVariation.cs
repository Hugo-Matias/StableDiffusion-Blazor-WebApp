namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// A two-value variation producing <see cref="OnValue"/> for the first iteration and <see cref="OffValue"/> for the second.
    /// Useful for A/B experiments such as toggling a LoRA on/off or switching between two settings.
    /// </summary>
    public sealed class ToggleVariation : Variation
    {
        /// <summary>The value used for the "on" iteration.</summary>
        public object? OnValue { get; set; }

        /// <summary>The value used for the "off" iteration.</summary>
        public object? OffValue { get; set; }
    }
}
