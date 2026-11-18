using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using ImageMagick;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Services
{
    public class WildcardBrowserService : IWildcardBrowserService
    {
        private readonly IStateService _state;
        private readonly IRouterService _router;
        private readonly IWorkflowService _workflowService;
        private readonly ILogger<WildcardBrowserService> _logger;
        private readonly string _wwwrootPath;

        public WildcardEntryPreviewProgress PreviewProgress { get; } = new();

        public WildcardBrowserService(
            IStateService state,
            IRouterService router,
            IWorkflowService workflowService,
            IWebHostEnvironment env,
            ILogger<WildcardBrowserService> logger)
        {
            _state = state;
            _router = router;
            _workflowService = workflowService;
            _logger = logger;
            _wwwrootPath = env.WebRootPath;
        }

        public string GetPreviewImagePath(WildcardBrowserEntry entry) =>
            Path.Combine(_wwwrootPath, "images", "wildcards", entry.CollectionId.ToString(), $"{entry.EntryId}.webp");

        public string GetPreviewImageSource(WildcardBrowserEntry entry, long cacheBust = 0)
        {
            var source = $"images/wildcards/{entry.CollectionId}/{entry.EntryId}.webp";
            return cacheBust > 0 ? $"{source}?v={cacheBust}" : source;
        }

        public bool HasPreviewImage(WildcardBrowserEntry entry) => File.Exists(GetPreviewImagePath(entry));

        public async Task GenerateBatchPreviewsAsync(
            List<WildcardBrowserEntry> entries,
            int batchSize,
            bool skipExisting,
            CancellationToken ct,
            Func<Task>? onProgress = null)
        {
            PreviewProgress.IsRunning = true;
            PreviewProgress.Current = 0;
            PreviewProgress.Skipped = 0;
            PreviewProgress.Failed = 0;
            PreviewProgress.CurrentEntry = string.Empty;

            var effectiveBatch = batchSize <= 0 ? entries.Count : batchSize;
            PreviewProgress.Total = Math.Min(effectiveBatch, entries.Count);

            var workflowId = _state.State.Generation.CurrentWorkflowId;
            if (workflowId == null)
            {
                _logger.LogError("No workflow selected for wildcard preview generation");
                PreviewProgress.IsRunning = false;
                return;
            }

            var workflow = _workflowService.GetWorkflowById(workflowId.Value);
            if (workflow == null)
            {
                _logger.LogError("Workflow {Id} not found", workflowId);
                PreviewProgress.IsRunning = false;
                return;
            }

            var generated = 0;
            try
            {
                foreach (var entry in entries)
                {
                    if (ct.IsCancellationRequested || generated >= effectiveBatch)
                    {
                        break;
                    }

                    PreviewProgress.CurrentEntry = entry.Value;

                    if (skipExisting && HasPreviewImage(entry))
                    {
                        PreviewProgress.Skipped++;
                        if (onProgress != null) await onProgress.Invoke();
                        continue;
                    }

                    try
                    {
                        var prepared = _state.GenerationParameters.Clone();

                        var promptsFragment = prepared.GetFragment(Fragments.Prompts);
                        if (promptsFragment != null)
                        {
                            var currentPrompt = promptsFragment.GetValueOrDefault<string>(Params.Positive, string.Empty) ?? string.Empty;
                            promptsFragment.SetValue(Params.Positive, currentPrompt.InjectPreviewTag(entry.Value.EscapeParenthesis()));
                        }

                        var samplerFragment = prepared.GetFragment(Fragments.MainSampler)
                            ?? prepared.GetFragment(Fragments.SamplerAdvanced);
                        if (samplerFragment != null)
                        {
                            var seed = samplerFragment.GetValueOrDefault(Params.Seed, -1L);
                            if (seed <= 0)
                            {
                                samplerFragment.SetValue(Params.Seed, (long)Random.Shared.Next(0, int.MaxValue));
                            }
                        }

                        var result = await _router.PostGenerationAsync(prepared, workflow);
                        if (result?.Images != null && result.Images.Count > 0)
                        {
                            var imageBytes = Convert.FromBase64String(result.Images[0]);
                            var outputPath = GetPreviewImagePath(entry);
                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }

                            using var image = new MagickImage(imageBytes);
                            await image.WriteAsync(outputPath, MagickFormat.WebP);
                        }
                        else
                        {
                            PreviewProgress.Failed++;
                            _logger.LogWarning("No image returned for wildcard entry {EntryId}", entry.EntryId);
                        }
                    }
                    catch (Exception ex)
                    {
                        PreviewProgress.Failed++;
                        _logger.LogError(ex, "Failed to generate preview for wildcard entry {EntryId}", entry.EntryId);
                    }

                    generated++;
                    PreviewProgress.Current = generated;
                    if (onProgress != null) await onProgress.Invoke();
                }
            }
            finally
            {
                PreviewProgress.IsRunning = false;
                PreviewProgress.CurrentEntry = string.Empty;
                if (onProgress != null) await onProgress.Invoke();
            }
        }
    }
}