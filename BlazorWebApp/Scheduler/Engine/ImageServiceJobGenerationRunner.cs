using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Services;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Default <see cref="IJobGenerationRunner"/> that delegates to <see cref="IImageService.GenerateImagesAsync"/>.
    /// Implements output routing by swapping <c>IStateService.State.Gallery.ProjectId</c> for the duration of
    /// the call; the original project id is restored in a finally block even on failure.
    /// Access to the state swap is serialized with a semaphore so concurrent scheduler runs do not stomp each other.
    /// </summary>
    public class ImageServiceJobGenerationRunner : IJobGenerationRunner
    {
        private static readonly SemaphoreSlim _stateLock = new(1, 1);

        private readonly IImageService _imageService;
        private readonly IDatabaseService _database;
        private readonly IStateService _state;
        private readonly ILogger<ImageServiceJobGenerationRunner> _logger;

        public ImageServiceJobGenerationRunner(
            IImageService imageService,
            IDatabaseService database,
            IStateService state,
            ILogger<ImageServiceJobGenerationRunner> logger)
        {
            _imageService = imageService;
            _database = database;
            _state = state;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ImagesDto> RunAsync(
            GenerationParameters parameters,
            Workflow workflow,
            JobOutputConfig output,
            CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            int? originalProjectId = null;
            bool projectSwapped = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(output.ProjectName))
                {
                    var project = await _database.GetProject(output.ProjectName);
                    if (project is not null)
                    {
                        originalProjectId = _state.State.Gallery.ProjectId;
                        _state.State.Gallery.ProjectId = project.Id;
                        projectSwapped = true;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Scheduler output project '{Project}' not found; using current project.",
                            output.ProjectName);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                return await _imageService.GenerateImagesAsync(parameters, workflow);
            }
            finally
            {
                if (projectSwapped && originalProjectId.HasValue)
                    _state.State.Gallery.ProjectId = originalProjectId.Value;
                _stateLock.Release();
            }
        }
    }
}
