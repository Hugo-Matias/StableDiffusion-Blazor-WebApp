namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Ordered set of values produced for one iteration of a
    /// <see cref="BlazorWebApp.Scheduler.Models.JobAction"/>. Order matches the action's
    /// <c>Variations</c> list so the execution engine can apply values in deterministic sequence.
    /// When an action has no variations a single empty set represents one iteration.
    /// </summary>
    public sealed class IterationValueSet
    {
        /// <summary>Zero-based index of this iteration within the plan.</summary>
        public int Index { get; }

        /// <summary>Values, aligned by position with the action's <c>Variations</c>.</summary>
        public IReadOnlyList<IterationValue> Values { get; }

        public IterationValueSet(int index, IReadOnlyList<IterationValue> values)
        {
            Index = index;
            Values = values;
        }
    }
}
