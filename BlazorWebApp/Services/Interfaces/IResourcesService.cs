using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service interface for managing local resources (models, LoRAs, embeddings, etc.).
    /// Handles resource file operations, state toggling, and prompt loading.
    /// </summary>
    public interface IResourcesService
    {
        /// <summary>
        /// Creates a list of local resources for a specific resource type.
        /// Groups resources by title and handles VAE special cases.
        /// </summary>
        /// <param name="typeId">The resource type ID to load</param>
        /// <returns>List of local resources sorted by title</returns>
        Task<List<LocalResource>> CreateLocalResourcesByType(int typeId);

        /// <summary>
        /// Creates a LocalResource from a database Resource entity.
        /// Also updates the entity's CreatedDate if not set.
        /// </summary>
        /// <param name="entity">The resource entity from database</param>
        /// <returns>LocalResource with file information populated</returns>
        Task<LocalResource> CreateLocalResourceByEntity(Resource entity);

        /// <summary>
        /// Gets file information for a resource file, checking both enabled and storage paths.
        /// </summary>
        /// <param name="resourceType">The resource type name (e.g., "LORA", "Checkpoint")</param>
        /// <param name="resourceSubtype">Optional subtype directory</param>
        /// <param name="file">The local resource file to populate</param>
        /// <returns>The populated file info, or null if file not found</returns>
        Task<LocalResourceFile?> GetResourceFileInfo(string resourceType, string? resourceSubtype, LocalResourceFile file);

        /// <summary>
        /// Loads a resource's prompt/keywords into the current generation parameters.
        /// Handles different resource types (TextualInversion, Hypernetwork, LoRA, etc.).
        /// </summary>
        /// <param name="file">The resource file to load</param>
        /// <param name="resourceType">The resource type name</param>
        /// <param name="target">Tuple of (ModeType, IsPositivePrompt)</param>
        Task LoadPrompt(LocalResourceFile file, string resourceType, ValueTuple<ModeType, bool> target);

        /// <summary>
        /// Updates a resource's file locations (move between enabled/disabled paths).
        /// Also updates associated preview files.
        /// </summary>
        /// <param name="resource">The resource entity to update</param>
        /// <param name="directory">Current directory of the resource files</param>
        /// <param name="filename">Base filename (without extension)</param>
        /// <param name="resourceId">The resource ID</param>
        /// <param name="isEnabled">Whether the resource should be enabled</param>
        Task UpdateResource(Resource resource, string directory, string filename, int resourceId, bool isEnabled);

        /// <summary>
        /// Deletes a resource from the database and optionally from disk.
        /// Also cleans up associated preview images and model version images.
        /// </summary>
        /// <param name="resource">The resource to delete</param>
        /// <param name="deleteFiles">Whether to delete files from disk</param>
        /// <param name="directory">Directory containing the resource files</param>
        /// <param name="filename">Base filename (without extension)</param>
        Task DeleteResource(Resource resource, bool deleteFiles, string directory, string filename);

        /// <summary>
        /// Toggles a resource's enabled state, moving files between enabled and storage directories.
        /// </summary>
        /// <param name="resource">The local resource</param>
        /// <param name="file">The specific file to toggle</param>
        Task ToggleResource(LocalResource resource, LocalResourceFile file);
    }
}
