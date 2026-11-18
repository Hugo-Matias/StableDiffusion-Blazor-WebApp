using BlazorWebApp.Scheduler.Variations;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Resolves a <see cref="Variation"/> into a concrete list of values.
    /// Deterministic variations (List/Range/Toggle/SearchReplace) evaluate synchronously;
    /// service-backed variations (Wildcard, LLM) require async resolution.
    /// </summary>
    public interface IVariationMaterializer
    {
        /// <summary>
        /// Produce the ordered value sequence for the given variation.
        /// Throws <see cref="NotSupportedException"/> for unknown variation types.
        /// </summary>
        Task<IReadOnlyList<object?>> MaterializeAsync(Variation variation, CancellationToken cancellationToken = default);
    }
}
