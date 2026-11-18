using BlazorWebApp.Data.Dtos.HuggingFace;

namespace BlazorWebApp.Services
{
    public interface IHuggingFaceService
    {
        /// <summary>
        /// Performs a fast keyword search across models, datasets, and spaces.
        /// Calls GET /api/quicksearch?q={query}.
        /// </summary>
        Task<HuggingFaceQuickSearchDto?> QuickSearchAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Lists models using the /api/models endpoint with full filter support and cursor-based pagination.
        /// </summary>
        Task<(List<HuggingFaceModelSummaryDto> Models, string? NextCursor)> GetModelsAsync(
            HuggingFaceModelsRequest request, CancellationToken ct = default);

        /// <summary>
        /// Retrieves full detail for a single model repository.
        /// Calls GET /api/models/{author}/{repo}.
        /// </summary>
        Task<HuggingFaceModelDetailDto?> GetModelAsync(string author, string repo, CancellationToken ct = default);

        /// <summary>
        /// Lists file-tree entries for a repository at the given revision and path.
        /// Calls GET /api/models/{author}/{repo}/tree/{revision}/{path}.
        /// When <paramref name="recursive"/> is true, returns all descendants in one call.
        /// Pagination is handled automatically when <paramref name="recursive"/> is false.
        /// </summary>
        Task<List<HuggingFaceTreeEntryDto>> GetTreeAsync(
            string author, string repo,
            string revision = "main", string path = "",
            bool recursive = false,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves size and LFS metadata for specific paths in a repository.
        /// Calls POST /api/models/{author}/{repo}/paths-info/{revision}.
        /// </summary>
        Task<List<HuggingFacePathInfoDto>> GetPathsInfoAsync(
            string author, string repo, IEnumerable<string> paths,
            string revision = "main",
            CancellationToken ct = default);

        /// <summary>
        /// Fetches the raw README.md text for a repository.
        /// Returns null when the file does not exist or the request fails.
        /// </summary>
        Task<string?> GetReadmeAsync(string author, string repo, string revision = "main", CancellationToken ct = default);

        /// <summary>
        /// Constructs the resolve URL for downloading a specific file from a repository.
        /// Format: https://huggingface.co/{author}/{repo}/resolve/{revision}/{filePath}
        /// No network request is made.
        /// </summary>
        string GetResolveUrl(string author, string repo, string filePath, string revision = "main");

        /// <summary>
        /// Downloads a set of files from a HuggingFace repository, creates local resource DB rows
        /// for files marked as resources, optionally saves a preview from the README, and publishes
        /// <c>ResourcesChangedEventArgs</c> and <c>DownloadCompletedEventArgs</c> when complete.
        /// </summary>
        /// <param name="request">Fully configured download request with confirmed mapping.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<HuggingFaceDownloadResult> DownloadAsync(HuggingFaceDownloadRequest request, CancellationToken ct = default);
    }
}
