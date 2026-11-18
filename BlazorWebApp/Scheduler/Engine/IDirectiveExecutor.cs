using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Applies a single <see cref="Directive"/> to a cloned <see cref="GenerationParameters"/> instance
    /// and the iteration's <see cref="JobOutputConfig"/>.
    /// Disabled directives are no-ops (callers may short-circuit earlier).
    /// </summary>
    public interface IDirectiveExecutor
    {
        void Apply(GenerationParameters parameters, JobOutputConfig output, Directive directive);
    }
}
