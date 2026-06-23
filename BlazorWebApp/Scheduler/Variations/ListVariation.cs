namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces a fixed, user-authored list of values. Count equals <see cref="Values"/>.Count.
    /// </summary>
    public sealed class ListVariation : Variation
    {
        /// <summary>
        /// Values iterated one per iteration index.
        /// </summary>
        public List<object?> Values { get; set; } = new();
    }
}
