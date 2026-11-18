namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces values pulled from a wildcard collection at run time.
    /// When <see cref="AllowRepeats"/> is <c>false</c> and <see cref="Count"/> is null, all entries are used exactly once.
    /// When <see cref="Weighted"/> is <c>true</c>, sampling uses collection entry weights.
    /// </summary>
    public sealed class WildcardVariation : Variation
    {
        /// <summary>
        /// Name of the wildcard collection to draw from (matches the <c>__collection__</c> token body).
        /// </summary>
        public string CollectionName { get; set; } = string.Empty;

        /// <summary>
        /// Number of values to produce. When null with <c>AllowRepeats=false</c>, produces every entry exactly once.
        /// </summary>
        public int? Count { get; set; }

        /// <summary>Whether the same entry may be selected multiple times.</summary>
        public bool AllowRepeats { get; set; }

        /// <summary>Whether selection uses entry weights.</summary>
        public bool Weighted { get; set; }
    }
}
