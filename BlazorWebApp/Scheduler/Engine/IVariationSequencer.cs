using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Turns a <see cref="JobAction"/> into a deterministic iteration plan and sequence.
    /// Stage 1: build the plan (materializes all variations, computes counts, applies limit).
    /// Stage 2: enumerate the plan yielding one <see cref="IterationValueSet"/> per iteration.
    /// </summary>
    public interface IVariationSequencer
    {
        /// <summary>
        /// Materializes variation values for the action and returns a <see cref="VariationPlan"/>.
        /// </summary>
        Task<VariationPlan> BuildPlanAsync(JobAction action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates iteration value sets according to the plan's permutation order and limit.
        /// Always yields at least one set (an empty set when the action has no variations).
        /// </summary>
        IEnumerable<IterationValueSet> Enumerate(VariationPlan plan);
    }
}
