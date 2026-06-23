using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Models;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Executes the generation pipeline for a single Scheduler iteration and returns the saved images.
    /// Implementations are responsible for honoring <paramref name="output"/> routing (project/folder).
    /// </summary>
    public interface IJobGenerationRunner
    {
        Task<ImagesDto> RunAsync(
            GenerationParameters parameters,
            Workflow workflow,
            JobOutputConfig output,
            CancellationToken cancellationToken = default);
    }
}
