using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Variations;

namespace BlazorWebApp.Scheduler.Models
{
    /// <summary>
    /// An isolated unit of work within a <see cref="Job"/>.
    /// Each action starts from a fresh clone of <see cref="Job.BaseParameters"/>, then applies its
    /// directives and variations to produce a cartesian iteration plan.
    /// </summary>
    public sealed class JobAction
    {
        /// <summary>Sort order within the owning job.</summary>
        public int Order { get; set; }

        /// <summary>User-facing label.</summary>
        public string? Label { get; set; }

        /// <summary>
        /// Optional maximum number of iterations; when non-null, excess iterations from the cartesian
        /// product are truncated (Sequential) or sampled (Random).
        /// </summary>
        public int? Limit { get; set; }

        /// <summary>How iteration indices are mapped to variation value combinations.</summary>
        public PermutationOrder PermutationOrder { get; set; } = PermutationOrder.Sequential;

        /// <summary>Seed used when <see cref="PermutationOrder"/> is <see cref="PermutationOrder.Random"/>.</summary>
        public int? RandomPermutationSeed { get; set; }

        /// <summary>Optional per-action output override.</summary>
        public JobOutputConfig? OutputOverride { get; set; }

        /// <summary>Directives applied on every iteration before variation values.</summary>
        public List<Directive> Directives { get; set; } = new();

        /// <summary>Variations whose cartesian product defines the action's iterations.</summary>
        public List<Variation> Variations { get; set; } = new();
    }
}
