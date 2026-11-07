using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for loading, caching and filtering caption records from captions.csv.
    /// </summary>
    public interface ICaptionLibraryService
    {
        /// <summary>
        /// Gets the total number of captions loaded in the cache.
        /// Returns 0 if the CSV has not been loaded yet or is unavailable.
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// Returns distinct aspect ratio values found in the caption library.
        /// </summary>
        Task<List<string>> GetAspectRatiosAsync();

        /// <summary>
        /// Returns distinct nudity level values found in the caption library.
        /// </summary>
        Task<List<string>> GetNudityLevelsAsync();

        /// <summary>
        /// Returns distinct clothing item values found in the caption library.
        /// </summary>
        Task<List<string>> GetClothingItemsAsync();

        /// <summary>
        /// Searches captions matching the given filter criteria with pagination.
        /// </summary>
        /// <param name="filter">Filter criteria to apply</param>
        /// <param name="page">1-based page number</param>
        /// <param name="pageSize">Number of results per page</param>
        /// <returns>Paged list of matching captions and total count</param>
        Task<(List<CaptionRecord> Items, int TotalCount)> SearchAsync(CaptionFilter filter, int page = 1, int pageSize = 24);

        /// <summary>
        /// Returns a random caption matching the active filters.
        /// </summary>
        /// <param name="filter">Filter criteria to apply before selecting</param>
        /// <returns>A random matching caption or null if none match</returns>
        Task<CaptionRecord?> GetRandomCaptionAsync(CaptionFilter? filter = null);

        /// <summary>
        /// Gets a single caption by its row ID.
        /// </summary>
        Task<CaptionRecord?> GetByIdAsync(int id);
    }
}
