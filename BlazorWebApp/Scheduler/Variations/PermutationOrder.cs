namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Controls how iteration indices are mapped to concrete value combinations for a
    /// <see cref="BlazorWebApp.Scheduler.Models.JobAction"/>.
    /// </summary>
    public enum PermutationOrder
    {
        /// <summary>Nested loops in variation order (leftmost is the outer loop).</summary>
        Sequential,

        /// <summary>Indices drawn at random from the full cartesian product using the action's seed.</summary>
        Random
    }
}
