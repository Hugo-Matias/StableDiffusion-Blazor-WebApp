using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service that downloads Danbooru posts to disk, persists metadata, and raises events.
    /// </summary>
    public class DanbooruLibraryService : IDanbooruLibraryService
    {
        private readonly HttpClient _httpClient;
        private readonly ISavedDanbooruMediaRepository _repository;
        private readonly IEventService _eventService;
        private readonly DanbooruOptions _options;
        private readonly ILogger<DanbooruLibraryService> _logger;

        public DanbooruLibraryService(
            HttpClient httpClient,
            ISavedDanbooruMediaRepository repository,
            IEventService eventService,
            IOptions<DanbooruOptions> options,
            ILogger<DanbooruLibraryService> logger)
        {
            _httpClient = httpClient;
            _repository = repository;
            _eventService = eventService;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<DanbooruSaveResult> SaveAsync(DanbooruPost post, CancellationToken cancellationToken = default)
        {
            // Dedup check
            if (await _repository.ExistsByPostIdAsync(post.Id, cancellationToken))
            {
                _logger.LogDebug("Post {PostId} already saved, skipping.", post.Id);
                return DanbooruSaveResult.AlreadySaved;
            }

            // Validate configuration
            if (string.IsNullOrWhiteSpace(_options.SavedMediaPath))
            {
                _logger.LogWarning("SavedMediaPath is not configured. Cannot save post {PostId}.", post.Id);
                return DanbooruSaveResult.Error;
            }

            if (string.IsNullOrWhiteSpace(post.Url))
            {
                _logger.LogWarning("Post {PostId} has no URL. Cannot download.", post.Id);
                return DanbooruSaveResult.Error;
            }

            try
            {
                // Resolve target path
                var ratingFolder = ResolveRatingFolder(post.Rating);
                var scoreBucket = ResolveScoreBucket(post.Score);
                var fileName = $"{post.Id}.{post.Extension}";
                var relativePath = $"{ratingFolder}/{scoreBucket}/{fileName}";
                var fullPath = Path.Combine(_options.SavedMediaPath, relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Download file using streaming to avoid buffering entire payload in memory
                _logger.LogInformation("Downloading post {PostId} to {Path}", post.Id, fullPath);
                var downloadContent = await _httpClient.GetStreamAsync(post.Url, cancellationToken);
                await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await downloadContent.CopyToAsync(fileStream, cancellationToken);

                // Create entity and persist (clean up orphan file on failure)
                var entity = new SavedDanbooruMedia(post)
                {
                    FilePath = relativePath
                };
                try
                {
                    await _repository.AddAsync(entity, cancellationToken);
                }
                catch
                {
                    // Orphan cleanup: remove the downloaded file if DB insert fails
                    try { File.Delete(fullPath); } catch { /* best-effort */ }
                    throw;
                }

                // Raise event (entity.Id populated by EF Core after SaveChanges)
                _eventService.Publish(new DanbooruMediaSavedEventArgs(entity));

                _logger.LogInformation("Saved post {PostId} to library.", post.Id);
                return DanbooruSaveResult.Saved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save post {PostId}.", post.Id);
                return DanbooruSaveResult.Error;
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(int id, bool deleteFile, CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(id, cancellationToken);
            if (entity == null) return;

            // Delete file from disk if requested
            if (deleteFile && !string.IsNullOrEmpty(entity.FilePath))
            {
                var fullPath = Path.Combine(_options.SavedMediaPath, entity.FilePath);
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        _logger.LogInformation("Deleted file {Path} for post {PostId}.", fullPath, entity.DanbooruPostId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete file {Path} for post {PostId}.", fullPath, entity.DanbooruPostId);
                    // Continue with DB deletion even if file delete fails
                }
            }

            // Delete DB row
            await _repository.DeleteAsync(id, cancellationToken);

            // Raise event
            _eventService.Publish(new DanbooruMediaDeletedEventArgs(id, entity.DanbooruPostId, deleteFile));

            _logger.LogInformation("Deleted library entry {Id} (post {PostId}, fileDeleted={DeleteFile}).", id, entity.DanbooruPostId, deleteFile);
        }

        /// <inheritdoc />
        public Task<List<SavedDanbooruMedia>> GetPagedAsync(SavedDanbooruMediaFilter filter, CancellationToken cancellationToken = default)
        {
            return _repository.GetPagedAsync(filter, cancellationToken);
        }

        /// <summary>
        /// Resolve the rating folder name from the post rating value.
        /// </summary>
        internal static string ResolveRatingFolder(string? rating)
        {
            // Danbooru API returns single-letter codes: g, s, q, e
            // Full names are also accepted for compatibility
            return rating?.ToLowerInvariant() switch
            {
                "g" or "general" => "general",
                "s" or "sensitive" => "sensitive",
                "q" or "questionable" => "questionable",
                "e" or "explicit" => "explicit",
                _ => "none"
            };
        }

        /// <summary>
        /// Resolve the score bucket folder name from the post score value.
        /// Buckets: <100 -> score_0, 100-199 -> score_100, 200-299 -> score_200, >=300 -> score_300.
        /// </summary>
        internal static string ResolveScoreBucket(int score)
        {
            return score switch
            {
                < 100 => "score_0",
                < 200 => "score_100",
                < 300 => "score_200",
                _ => "score_300"
            };
        }
    }
}
