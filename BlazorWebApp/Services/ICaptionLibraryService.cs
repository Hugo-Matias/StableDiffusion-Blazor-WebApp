using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for loading, caching and filtering caption records from the CSV files under Data/Captions/.
    /// </summary>
    public interface ICaptionLibraryService
    {
        /// <summary>
        /// The default source name: "captions" if present, otherwise the first CSV alphabetically.
        /// Null when no CSVs exist in the captions folder.
        /// </summary>
        string? DefaultSource { get; }

        /// <summary>
        /// Returns all discovered caption sources (one per CSV file) with their row counts.
        /// </summary>
        Task<List<CaptionSource>> GetSourcesAsync();

        /// <summary>
        /// Gets the total number of captions in a source.
        /// Defaults to <see cref="DefaultSource"/> when no source is given.
        /// </summary>
        Task<int> GetTotalCountAsync(string? source = null);

        /// <summary>
        /// Returns distinct aspect ratio values found in the caption library.
        /// </summary>
        Task<List<string>> GetAspectRatiosAsync(string? source = null);

        /// <summary>
        /// Returns distinct nudity level values found in the caption library.
        /// </summary>
        Task<List<string>> GetNudityLevelsAsync(string? source = null);

        /// <summary>
        /// Returns distinct clothing item values found in the caption library.
        /// </summary>
        Task<List<string>> GetClothingItemsAsync(string? source = null);

        /// <summary>
        /// Searches captions matching the given filter criteria with pagination.
        /// </summary>
        /// <param name="filter">Filter criteria to apply</param>
        /// <param name="page">1-based page number</param>
        /// <param name="pageSize">Number of results per page</param>
        /// <param name="source">Caption source to query (defaults to the default source)</param>
        /// <returns>Paged list of matching captions and total count</param>
        Task<(List<CaptionRecord> Items, int TotalCount)> SearchAsync(CaptionFilter filter, int page = 1, int pageSize = 24, string? source = null);

        /// <summary>
        /// Returns a random caption matching the active filters.
        /// </summary>
        /// <param name="filter">Filter criteria to apply before selecting</param>
        /// <param name="source">Caption source to query (defaults to the default source)</param>
        /// <returns>A random matching caption or null if none match</returns>
        Task<CaptionRecord?> GetRandomCaptionAsync(CaptionFilter? filter = null, string? source = null);

        /// <summary>
        /// Gets a single caption by its row ID within the given source.
        /// </summary>
        /// <param name="id">1-based row number in the source file</param>
        /// <param name="source">Caption source to query (defaults to the default source)</param>
        Task<CaptionRecord?> GetByIdAsync(int id, string? source = null);
    }
}
