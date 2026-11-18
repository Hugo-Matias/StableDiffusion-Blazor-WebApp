using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Data.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="ISavedDanbooruMediaRepository"/>.
    /// Each call creates a fresh scoped <see cref="AppDbContext"/> via <see cref="IDbContextFactory{TContext}"/>.
    /// </summary>
    public class SavedDanbooruMediaRepository : ISavedDanbooruMediaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SavedDanbooruMediaRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <inheritdoc />
        public async Task<SavedDanbooruMedia> AddAsync(SavedDanbooruMedia entity, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.SavedDanbooruMedia.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            return entity;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByPostIdAsync(int danbooruPostId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.SavedDanbooruMedia
                .AsNoTracking()
                .AnyAsync(m => m.DanbooruPostId == danbooruPostId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SavedDanbooruMedia?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.SavedDanbooruMedia
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<SavedDanbooruMedia?> GetByPostIdAsync(int danbooruPostId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.SavedDanbooruMedia
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DanbooruPostId == danbooruPostId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<List<SavedDanbooruMedia>> GetPagedAsync(SavedDanbooruMediaFilter filter, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var query = context.SavedDanbooruMedia.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Rating))
            {
                var ratingLower = filter.Rating.ToLowerInvariant();
                query = query.Where(m => m.Rating != null && m.Rating.ToLower() == ratingLower);
            }

            if (filter.MinScore.HasValue)
            {
                query = query.Where(m => m.Score >= filter.MinScore.Value);
            }

            if (filter.VideoOnly.HasValue && filter.VideoOnly.Value)
            {
                query = query.Where(m => m.IsVideo);
            }

            // When TagSearch is empty, use SQL-side pagination (LIMIT/OFFSET).
            if (string.IsNullOrWhiteSpace(filter.TagSearch))
            {
                return await query
                    .OrderByDescending(m => m.DateCreated)
                    .Skip(filter.Skip)
                    .Take(filter.Take)
                    .ToListAsync(cancellationToken);
            }

            // Tag search requires inspecting JSON-list columns, which EF Core 6 cannot translate.
            // Materialise the scalar-filtered set (bounded) and apply tag filter + pagination in-memory.
            const int tagSearchScanLimit = 5000;
            var scanned = await query
                .OrderByDescending(m => m.DateCreated)
                .Take(tagSearchScanLimit + 1)
                .ToListAsync(cancellationToken);

            if (scanned.Count > tagSearchScanLimit)
            {
                // Log when the cap is hit so operators are aware of the limitation.
                // Drop the sentinel extra row.
                scanned.RemoveAt(tagSearchScanLimit);
            }

            var search = filter.TagSearch.ToLowerInvariant();
            var filtered = scanned.Where(m => m.TagsBundle.General.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || m.TagsBundle.Artist.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || m.TagsBundle.Character.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || m.TagsBundle.Copyright.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase))
                || m.TagsBundle.Meta.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return filtered.Skip(filter.Skip).Take(filter.Take).ToList();
        }

        /// <inheritdoc />
        public async Task UpdateAsync(SavedDanbooruMedia entity, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            context.Update(entity);
            // Ensure the TagsBundle column is re-serialized when callers mutate the in-place object graph.
            context.Entry(entity).Property(e => e.TagsBundle).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.SavedDanbooruMedia
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (existing == null) return;
            context.SavedDanbooruMedia.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
