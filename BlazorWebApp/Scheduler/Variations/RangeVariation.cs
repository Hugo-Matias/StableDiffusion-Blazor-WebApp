namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces an inclusive numeric range [<see cref="Start"/>, <see cref="End"/>] stepped by <see cref="Step"/>.
    /// </summary>
    public sealed class RangeVariation : Variation
    {
        /// <summary>Inclusive lower bound.</summary>
        public double Start { get; set; }

        /// <summary>Inclusive upper bound.</summary>
        public double End { get; set; }

        /// <summary>Step size. Must be greater than zero.</summary>
        public double Step { get; set; } = 1.0;

        /// <summary>When <c>true</c> materialized values are coerced to <see cref="long"/>.</summary>
        public bool IsInteger { get; set; }
    }
}
