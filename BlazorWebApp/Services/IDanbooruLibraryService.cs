using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Result of a save operation.
    /// </summary>
    public enum DanbooruSaveResult
    {
        /// <summary>Media was successfully saved.</summary>
        Saved,

        /// <summary>Media was already saved (duplicate).</summary>
        AlreadySaved,

        /// <summary>Save failed (network error, disk error, etc.).</summary>
        Error
    }

    /// <summary>
    /// Service contract for managing the local Danbooru media library.
    /// </summary>
    public interface IDanbooruLibraryService
    {
        /// <summary>
        /// Download a Danbooru post to disk and persist the metadata.
        /// </summary>
        Task<DanbooruSaveResult> SaveAsync(DanbooruPost post, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a library entry, optionally removing the file from disk.
        /// </summary>
        Task DeleteAsync(int id, bool deleteFile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a paged, filtered list of saved media.
        /// </summary>
        Task<List<SavedDanbooruMedia>> GetPagedAsync(Data.Repositories.SavedDanbooruMediaFilter filter, CancellationToken cancellationToken = default);
    }
}
