using BlazorWebApp.Scheduler.Variations;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// A single value to write during one iteration of a <see cref="BlazorWebApp.Scheduler.Models.JobAction"/>.
    /// The owning <see cref="Variation"/> is preserved so the execution engine knows where to write
    /// (via <see cref="Variation.Target"/>) and how to interpret the value (via the concrete variation type,
    /// e.g. <see cref="SearchReplaceVariation"/> which needs prompt-level handling).
    /// </summary>
    /// <param name="Variation">The variation that produced this value.</param>
    /// <param name="Value">The materialized value.</param>
    public sealed record IterationValue(Variation Variation, object? Value);
}
