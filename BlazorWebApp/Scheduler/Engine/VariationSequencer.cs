using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Variations;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Default <see cref="IVariationSequencer"/>. Builds the plan using an injected
    /// <see cref="IVariationMaterializer"/>, then walks the cartesian product in the requested order.
    /// Sequential mode: leftmost variation is the outer loop (slowest-varying).
    /// Random mode: indices drawn without replacement using a seeded <see cref="Random"/>.
    /// </summary>
    public class VariationSequencer : IVariationSequencer
    {
        private readonly IVariationMaterializer _materializer;

        public VariationSequencer(IVariationMaterializer materializer)
        {
            _materializer = materializer;
        }

        /// <inheritdoc />
        public async Task<VariationPlan> BuildPlanAsync(JobAction action, CancellationToken cancellationToken = default)
        {
            var variations = action.Variations.ToArray();
            var valueLists = new List<IReadOnlyList<object?>>(variations.Length);

            foreach (var v in variations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = await _materializer.MaterializeAsync(v, cancellationToken);
                valueLists.Add(values.Count == 0 ? new object?[] { null } : values);
            }

            int total = 1;
            foreach (var list in valueLists) total = checked(total * list.Count);

            int effective = action.Limit is int lim && lim > 0 ? Math.Min(lim, total) : total;

            return new VariationPlan(
                variations,
                valueLists,
                total,
                effective,
                action.PermutationOrder,
                action.RandomPermutationSeed);
        }

        /// <inheritdoc />
        public IEnumerable<IterationValueSet> Enumerate(VariationPlan plan)
        {
            if (plan.Variations.Count == 0)
            {
                // One iteration, no values. Effective count is 1 but allow Limit=0 to clip.
                if (plan.EffectiveCount >= 1)
                    yield return new IterationValueSet(0, Array.Empty<IterationValue>());
                yield break;
            }

            if (plan.PermutationOrder == PermutationOrder.Random)
            {
                foreach (var set in EnumerateRandom(plan)) yield return set;
                yield break;
            }

            foreach (var set in EnumerateSequential(plan)) yield return set;
        }

        private static IEnumerable<IterationValueSet> EnumerateSequential(VariationPlan plan)
        {
            int count = plan.EffectiveCount;
            for (int i = 0; i < count; i++)
            {
                yield return BuildSet(plan, i, i);
            }
        }

        private static IEnumerable<IterationValueSet> EnumerateRandom(VariationPlan plan)
        {
            var seed = plan.RandomPermutationSeed ?? Random.Shared.Next();
            var rng = new Random(seed);

            // Fisher-Yates over the full index space, then take EffectiveCount.
            var indices = Enumerable.Range(0, plan.TotalCount).ToArray();
            for (int i = indices.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int iter = 0; iter < plan.EffectiveCount; iter++)
            {
                yield return BuildSet(plan, iter, indices[iter]);
            }
        }

        private static IterationValueSet BuildSet(VariationPlan plan, int iterationIndex, int linearIndex)
        {
            // Decompose linearIndex into per-variation indices.
            // Leftmost variation is outer loop -> slowest changing digit (most significant).
            var values = new IterationValue[plan.Variations.Count];
            int remainder = linearIndex;

            // Compute strides for each variation (stride[i] = product of Counts[i+1..]).
            var strides = new int[plan.Variations.Count];
            int stride = 1;
            for (int i = plan.Variations.Count - 1; i >= 0; i--)
            {
                strides[i] = stride;
                stride *= plan.Values[i].Count;
            }

            for (int i = 0; i < plan.Variations.Count; i++)
            {
                int idx = remainder / strides[i];
                remainder %= strides[i];
                values[i] = new IterationValue(plan.Variations[i], plan.Values[i][idx]);
            }

            return new IterationValueSet(iterationIndex, values);
        }
    }
}
