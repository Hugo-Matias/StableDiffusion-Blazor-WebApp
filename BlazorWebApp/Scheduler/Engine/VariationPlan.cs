using BlazorWebApp.Scheduler.Variations;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Materialized result of building an iteration plan for a
    /// <see cref="BlazorWebApp.Scheduler.Models.JobAction"/>. Holds one value list per variation plus
    /// the cartesian counts so the UI can surface truncation and the engine can drive iteration.
    /// </summary>
    public sealed class VariationPlan
    {
        /// <summary>The variations in iteration order (leftmost = outer loop in sequential mode).</summary>
        public IReadOnlyList<Variation> Variations { get; }

        /// <summary>Materialized value lists, aligned by position with <see cref="Variations"/>.</summary>
        public IReadOnlyList<IReadOnlyList<object?>> Values { get; }

        /// <summary>Full cartesian product size. <c>1</c> when no variations exist.</summary>
        public int TotalCount { get; }

        /// <summary>Actual iteration count after applying the action's <c>Limit</c> cap.</summary>
        public int EffectiveCount { get; }

        /// <summary>Permutation strategy in effect.</summary>
        public PermutationOrder PermutationOrder { get; }

        /// <summary>Seed used for random permutation, if applicable.</summary>
        public int? RandomPermutationSeed { get; }

        /// <summary><c>true</c> when <see cref="EffectiveCount"/> is less than <see cref="TotalCount"/>.</summary>
        public bool WasTruncated => EffectiveCount < TotalCount;

        public VariationPlan(
            IReadOnlyList<Variation> variations,
            IReadOnlyList<IReadOnlyList<object?>> values,
            int totalCount,
            int effectiveCount,
            PermutationOrder permutationOrder,
            int? randomPermutationSeed)
        {
            Variations = variations;
            Values = values;
            TotalCount = totalCount;
            EffectiveCount = effectiveCount;
            PermutationOrder = permutationOrder;
            RandomPermutationSeed = randomPermutationSeed;
        }
    }
}
