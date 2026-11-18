using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Writes a materialized value onto the portion of <see cref="GenerationParameters"/>
    /// described by a <see cref="ParameterTarget"/>. For <see cref="OutputTarget"/>s the
    /// write is routed to a <see cref="JobOutputConfig"/> supplied by the execution engine.
    /// </summary>
    public interface IParameterApplier
    {
        void Apply(GenerationParameters parameters, JobOutputConfig output, ParameterTarget target, object? value);
    }
}
