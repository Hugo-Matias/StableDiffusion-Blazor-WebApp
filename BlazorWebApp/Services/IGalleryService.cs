using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing gallery folders, projects, and image selection.
    /// Handles folder/project navigation and selected images state.
    /// </summary>
    public interface IGalleryService
    {
        List<Folder>? Folders { get; }
        List<Project>? Projects { get; }
        List<int> SelectedImageIds { get; }
        
        /// <summary>
        /// Indicates whether gallery filters are currently applied.
        /// </summary>
        bool IsGalleryFiltered { get; set; }

        Task GetFolders();
        Task GetProjects(int folderId = 0);
        Task SetCurrentFolder(int id);
        Task SetCurrentProject(int id);

        void AddSelectedImage(int id);
        void RemoveSelectedImage(int id);
        void ClearSelectedImages();
        void ReplaceSelectedImages(List<int> ids);
    }
}
