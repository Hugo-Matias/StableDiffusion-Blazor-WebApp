using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupGroupExplanationService : ICleanupGroupExplanationService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _repository;
        private readonly VLModelService _vlModelService;
        private readonly IOptions<CleanupGroupExplanationOptions> _options;

        public CleanupGroupExplanationService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository repository,
            VLModelService vlModelService,
            IOptions<CleanupGroupExplanationOptions> options)
        {
            _contextFactory = contextFactory;
            _repository = repository;
            _vlModelService = vlModelService;
            _options = options;
        }

        public Task<CleanupGroupExplanation?> GetCachedExplanationAsync(int groupId, CancellationToken cancellationToken = default)
        {
            return _repository.GetGroupExplanationAsync(groupId, cancellationToken);
        }

        public async Task<CleanupGroupExplanation> GenerateExplanationAsync(
            int groupId,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (groupId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groupId), "Group id must be greater than zero.");
            }

            var options = _options.Value;
            var cached = await _repository.GetGroupExplanationAsync(groupId, cancellationToken);
            if (cached != null && !forceRefresh && !options.ForceRefresh)
            {
                return cached;
            }

            if (!options.Enabled)
            {
                throw new InvalidOperationException("Cleanup vision-language explanations are disabled.");
            }

            if (string.IsNullOrWhiteSpace(options.ModelName))
            {
                throw new InvalidOperationException("Cleanup vision-language ModelName is not configured.");
            }

            var target = await LoadExplanationTargetAsync(groupId, cancellationToken);
            if (target == null)
            {
                throw new InvalidOperationException($"Cleanup group {groupId} could not be found.");
            }

            var result = await _vlModelService.InterrogateAsync(new InterrogationRequest
            {
                ModelName = options.ModelName,
                Style = options.Style,
                ImagePath = target.ImagePath,
                NormalizeToDanbooruTags = false
            }, cancellationToken);

            var explanation = string.IsNullOrWhiteSpace(result.RawResponse) ? result.Prompt : result.RawResponse;
            if (string.IsNullOrWhiteSpace(explanation))
            {
                throw new InvalidOperationException("Vision-language model returned an empty explanation.");
            }

            return await _repository.UpsertGroupExplanationAsync(new CleanupGroupExplanation
            {
                GroupId = groupId,
                RepresentativeImageId = target.ImageId,
                ModelName = options.ModelName,
                Style = options.Style.ToString(),
                Caption = BuildCaption(explanation),
                Explanation = explanation.Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }

        private async Task<ExplanationTarget?> LoadExplanationTargetAsync(int groupId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var group = await context.CleanupGroups
                .AsNoTracking()
                .Where(row => row.Id == groupId)
                .Select(row => new { row.Id, row.RepresentativeImageId })
                .FirstOrDefaultAsync(cancellationToken);

            if (group == null)
            {
                return null;
            }

            var imageId = group.RepresentativeImageId ?? await context.CleanupGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == groupId)
                .OrderBy(member => member.SortOrder)
                .Select(member => (int?)member.ImageId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!imageId.HasValue)
            {
                throw new InvalidOperationException($"Cleanup group {groupId} has no representative or member image.");
            }

            var imagePath = await context.Images
                .AsNoTracking()
                .Where(image => image.Id == imageId.Value)
                .Select(image => image.Path)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(imagePath)
                ? throw new InvalidOperationException($"Image {imageId.Value} could not be found for cleanup explanation.")
                : new ExplanationTarget(imageId.Value, imagePath);
        }

        private static string BuildCaption(string explanation)
        {
            var compact = string.Join(" ", explanation.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return compact.Length <= 240 ? compact : compact[..240].TrimEnd() + "...";
        }

        private sealed record ExplanationTarget(int ImageId, string ImagePath);
    }
}