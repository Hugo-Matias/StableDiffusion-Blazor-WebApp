using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Data.Repositories
{
    /// <summary>
    /// Simple filter record for paged queries against <see cref="SavedDanbooruMedia"/>.
    /// </summary>
    public record SavedDanbooruMediaFilter
    {
        /// <summary>Optional tag search (searches all tag categories).</summary>
        public string? TagSearch { get; init; }

        /// <summary>Optional rating filter (general, sensitive, questionable, explicit, none).</summary>
        public string? Rating { get; init; }

        /// <summary>Minimum score filter.</summary>
        public int? MinScore { get; init; }

        /// <summary>Whether to include only videos.</summary>
        public bool? VideoOnly { get; init; }

        /// <summary>Number of rows to skip.</summary>
        public int Skip { get; init; }

        /// <summary>Maximum number of rows to return.</summary>
        public int Take { get; init; } = 50;
    }

    /// <summary>
    /// Repository contract for <see cref="SavedDanbooruMedia"/> persistence operations.
    /// </summary>
    public interface ISavedDanbooruMediaRepository
    {
        /// <summary>Add a new saved media row.</summary>
        Task<SavedDanbooruMedia> AddAsync(SavedDanbooruMedia entity, CancellationToken cancellationToken = default);

        /// <summary>Check whether a row with the given Danbooru post ID already exists.</summary>
        Task<bool> ExistsByPostIdAsync(int danbooruPostId, CancellationToken cancellationToken = default);

        /// <summary>Get a single entity by its database primary key.</summary>
        Task<SavedDanbooruMedia?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Get a paged, filtered list of saved media (ordered by creation date descending).</summary>
        Task<List<SavedDanbooruMedia>> GetPagedAsync(SavedDanbooruMediaFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Update an existing entity. Marks <c>TagsBundle</c> as modified so EF re-serializes it.</summary>
        Task UpdateAsync(SavedDanbooruMedia entity, CancellationToken cancellationToken = default);

        /// <summary>Delete an entity by its database primary key.</summary>
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
