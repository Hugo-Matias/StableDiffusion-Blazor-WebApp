using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupIndexingService : ICleanupIndexingService
    {
        private const int IndexVersion = 1;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _cleanupRepository;
        private readonly ICleanupPromptIndexService _promptIndex;
        private readonly ICleanupImageHashService _imageHash;
        private readonly ILogger<CleanupIndexingService> _logger;

        public CleanupIndexingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository cleanupRepository,
            ICleanupPromptIndexService promptIndex,
            ICleanupImageHashService imageHash,
            ILogger<CleanupIndexingService> logger)
        {
            _contextFactory = contextFactory;
            _cleanupRepository = cleanupRepository;
            _promptIndex = promptIndex;
            _imageHash = imageHash;
            _logger = logger;
        }

        public async Task<CleanupIndexingResult> IndexImagesAsync(
            CleanupIndexingOptions options,
            IProgress<CleanupIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var batchSize = Math.Clamp(options.BatchSize, 1, 500);
            var lastImageId = options.StartAfterImageId;
            var indexed = 0;
            var skipped = 0;
            var missing = 0;
            var failed = 0;
            var totalCandidates = await CountSourcesAsync(options, lastImageId, cancellationToken);
            var remaining = totalCandidates;

            progress?.Report(new CleanupIndexingProgress
            {
                LastImageId = lastImageId,
                TotalCandidates = totalCandidates,
                Indexed = indexed,
                Skipped = skipped,
                MissingFiles = missing,
                Failed = failed
            });

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var take = Math.Min(batchSize, remaining);
                var sources = await LoadSourcesAsync(options, lastImageId, take, cancellationToken);
                if (sources.Count == 0)
                {
                    break;
                }

                remaining -= sources.Count;
                lastImageId = sources[^1].ImageId;
                var existing = await LoadExistingIndexesAsync(sources.Select(source => source.ImageId).ToArray(), cancellationToken);

                foreach (var source in sources)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = await IndexSourceAsync(source, existing.GetValueOrDefault(source.ImageId), options.Force, cancellationToken);
                    switch (result)
                    {
                        case ImageIndexResult.Indexed:
                            indexed++;
                            break;
                        case ImageIndexResult.Skipped:
                            skipped++;
                            break;
                        case ImageIndexResult.MissingFile:
                            missing++;
                            break;
                        case ImageIndexResult.Failed:
                            failed++;
                            break;
                    }
                }

                progress?.Report(new CleanupIndexingProgress
                {
                    LastImageId = lastImageId,
                    TotalCandidates = totalCandidates,
                    Indexed = indexed,
                    Skipped = skipped,
                    MissingFiles = missing,
                    Failed = failed
                });
            }

            return new CleanupIndexingResult
            {
                LastImageId = indexed + skipped + missing + failed == 0 ? null : lastImageId,
                TotalCandidates = totalCandidates,
                Indexed = indexed,
                Skipped = skipped,
                MissingFiles = missing,
                Failed = failed
            };
        }

        public async Task<CleanupIndexingResult> IndexImageAsync(
            int imageId,
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            if (imageId <= 0)
            {
                return new CleanupIndexingResult();
            }

            var source = await LoadSourceByIdAsync(imageId, cancellationToken);
            if (source == null)
            {
                return new CleanupIndexingResult();
            }

            var existing = await LoadExistingIndexesAsync(new[] { imageId }, cancellationToken);
            var result = await IndexSourceAsync(source, existing.GetValueOrDefault(imageId), force, cancellationToken);

            return new CleanupIndexingResult
            {
                LastImageId = imageId,
                TotalCandidates = 1,
                Indexed = result == ImageIndexResult.Indexed ? 1 : 0,
                Skipped = result == ImageIndexResult.Skipped ? 1 : 0,
                MissingFiles = result == ImageIndexResult.MissingFile ? 1 : 0,
                Failed = result == ImageIndexResult.Failed ? 1 : 0
            };
        }

        private async Task<int> CountSourcesAsync(
            CleanupIndexingOptions options,
            int lastImageId,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var count = await BuildSourceQuery(context, options, lastImageId)
                .CountAsync(cancellationToken);

            return options.MaxImages.HasValue
                ? Math.Min(count, Math.Max(0, options.MaxImages.Value))
                : count;
        }

        private async Task<ImageIndexResult> IndexSourceAsync(
            CleanupImageSource source,
            CleanupImageIndex? existing,
            bool force,
            CancellationToken cancellationToken)
        {
            var prompt = _promptIndex.BuildPromptIndex(source.Prompt);
            var path = ResolveImagePath(source.ImagePath);
            var now = DateTime.UtcNow;
            var index = new CleanupImageIndex
            {
                ImageId = source.ImageId,
                ProjectId = source.ProjectId,
                ModeId = source.ModeId,
                ResourceId = source.ResourceId,
                WorkflowId = source.WorkflowId,
                ImagePath = path,
                PromptNormalized = prompt.NormalizedPrompt,
                PromptFingerprint = prompt.Fingerprint,
                PromptTokenSignature = prompt.TokenSignature,
                IndexVersion = IndexVersion,
                MetadataIndexedAtUtc = now
            };

            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    index.FileExists = false;
                    index.Status = CleanupIndexStatus.MissingFile;
                    index.IndexedAtUtc = now;
                    await _cleanupRepository.UpsertImageIndexAsync(index, cancellationToken);
                    return ImageIndexResult.MissingFile;
                }

                var file = new FileInfo(path);
                index.FileExists = true;
                index.FileSizeBytes = file.Length;
                index.FileLastWriteUtc = file.LastWriteTimeUtc;

                if (!force && existing != null && IsCurrent(existing, index))
                {
                    return ImageIndexResult.Skipped;
                }

                index.ExactHash = await _imageHash.ComputeExactHashAsync(path, cancellationToken);
                index.PerceptualHash = _imageHash.ComputePerceptualHash(path);
                index.HashIndexedAtUtc = now;
                index.IndexedAtUtc = now;
                index.Status = CleanupIndexStatus.Indexed;
                await _cleanupRepository.UpsertImageIndexAsync(index, cancellationToken);
                return ImageIndexResult.Indexed;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to index cleanup image {ImageId} at {ImagePath}", source.ImageId, path);
                index.Status = CleanupIndexStatus.Error;
                index.ErrorMessage = ex.Message;
                index.IndexedAtUtc = now;
                await _cleanupRepository.UpsertImageIndexAsync(index, cancellationToken);
                return ImageIndexResult.Failed;
            }
        }

        private static bool IsCurrent(CleanupImageIndex existing, CleanupImageIndex candidate)
        {
            return existing.Status == CleanupIndexStatus.Indexed
                && existing.IndexVersion == IndexVersion
                && existing.FileExists == candidate.FileExists
                && existing.ImagePath == candidate.ImagePath
                && existing.ProjectId == candidate.ProjectId
                && existing.ModeId == candidate.ModeId
                && existing.ResourceId == candidate.ResourceId
                && existing.WorkflowId == candidate.WorkflowId
                && existing.FileSizeBytes == candidate.FileSizeBytes
                && existing.FileLastWriteUtc == candidate.FileLastWriteUtc
                && existing.PromptNormalized == candidate.PromptNormalized;
        }

        private async Task<List<CleanupImageSource>> LoadSourcesAsync(
            CleanupIndexingOptions options,
            int lastImageId,
            int take,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await BuildSourceQuery(context, options, lastImageId)
                .OrderBy(image => image.Id)
                .Take(take)
                .Select(image => new CleanupImageSource(
                    image.Id,
                    image.ProjectId,
                    image.ModeId,
                    image.ResourceId,
                    image.WorkflowId,
                    image.Path,
                    image.Prompt))
                .ToListAsync(cancellationToken);
        }

        private async Task<CleanupImageSource?> LoadSourceByIdAsync(
            int imageId,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Images
                .AsNoTracking()
                .Where(image => image.Id == imageId)
                .Select(image => new CleanupImageSource(
                    image.Id,
                    image.ProjectId,
                    image.ModeId,
                    image.ResourceId,
                    image.WorkflowId,
                    image.Path,
                    image.Prompt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static IQueryable<Image> BuildSourceQuery(
            AppDbContext context,
            CleanupIndexingOptions options,
            int lastImageId)
        {
            var query = context.Images
                .AsNoTracking()
                .Where(image => image.Id > lastImageId);

            if (options.ProjectId.HasValue)
            {
                query = query.Where(image => image.ProjectId == options.ProjectId.Value);
            }

            if (!options.IncludeHidden)
            {
                query = query.Where(image => !image.IsHidden);
            }

            return query;
        }

        private async Task<Dictionary<int, CleanupImageIndex>> LoadExistingIndexesAsync(
            IReadOnlyCollection<int> imageIds,
            CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupImageIndexes
                .AsNoTracking()
                .Where(index => imageIds.Contains(index.ImageId))
                .ToDictionaryAsync(index => index.ImageId, cancellationToken);
        }

        private static string ResolveImagePath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return string.Empty;
            }

            var normalized = imagePath.Trim().Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                return Path.GetFullPath(normalized);
            }

            normalized = normalized.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
        }

        private enum ImageIndexResult
        {
            Indexed,
            Skipped,
            MissingFile,
            Failed
        }

        private sealed record CleanupImageSource(
            int ImageId,
            int ProjectId,
            int ModeId,
            int? ResourceId,
            string? WorkflowId,
            string ImagePath,
            string? Prompt);
    }
}