namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces <see cref="Count"/> random samples drawn uniformly from [<see cref="Min"/>, <see cref="Max"/>].
    /// When <see cref="Seed"/> is set the sequence is deterministic.
    /// </summary>
    public sealed class RandomVariation : Variation
    {
        /// <summary>Inclusive lower bound of the random range.</summary>
        public double Min { get; set; }

        /// <summary>Exclusive upper bound of the random range.</summary>
        public double Max { get; set; }

        /// <summary>Number of samples to draw.</summary>
        public int Count { get; set; } = 1;

        /// <summary>Optional seed for reproducible sequences.</summary>
        public int? Seed { get; set; }

        /// <summary>When <c>true</c> materialized values are coerced to <see cref="long"/>.</summary>
        public bool IsInteger { get; set; }
    }
}
