using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing gallery operations including folders, projects, and image selection.
    /// Coordinates with StateService for persistence and EventService for change notifications.
    /// </summary>
    public class GalleryService : IGalleryService
    {
        private readonly IDatabaseService _db;
        private readonly IStateService _state;
        private readonly IEventService _events;

        public List<Folder>? Folders { get; private set; }
        public List<Project>? Projects { get; private set; }
        public List<int> SelectedImageIds { get; private set; } = new();

        public GalleryService(IDatabaseService db, IStateService state, IEventService events)
        {
            _db = db;
            _state = state;
            _events = events;
        }

        /// <summary>
        /// Loads all folders from the database.
        /// </summary>
        public async Task GetFolders()
        {
            Folders = await _db.GetFolders();
        }

        /// <summary>
        /// Loads all projects from the database, optionally filtered by folderId.
        /// </summary>
        public async Task GetProjects(int folderId = 0)
        {
            Projects = await _db.GetProjects(folderId);
        }

        /// <summary>
        /// Sets the current folder and updates the application state.
        /// Publishes a FolderChangedEventArgs event.
        /// </summary>
        public async Task SetCurrentFolder(int id)
        {
            if (id == 0)
            {
                // "All" folders selected
                _state.State.Gallery.FolderId = 0;
                _state.State.Gallery.FolderName = "All";
                await _state.SaveState();

                _events.Publish(new FolderChangedEventArgs
                {
                    FolderId = 0,
                    FolderName = "All"
                });
            }
            else
            {
                var folder = Folders?.FirstOrDefault(f => f.Id == id);
                if (folder != null)
                {
                    _state.State.Gallery.FolderId = folder.Id;
                    _state.State.Gallery.FolderName = folder.Name;
                    await _state.SaveState();

                    _events.Publish(new FolderChangedEventArgs
                    {
                        FolderId = folder.Id,
                        FolderName = folder.Name
                    });
                }
            }
        }

        /// <summary>
        /// Sets the current project and updates the application state.
        /// Publishes a ProjectChangedEventArgs event.
        /// </summary>
        public async Task SetCurrentProject(int id)
        {
            var project = Projects?.FirstOrDefault(p => p.Id == id);
            if (project != null)
            {
                _state.State.Gallery.ProjectId = project.Id;
                _state.State.Gallery.ProjectName = project.Name;
                await _state.SaveState();

                _events.Publish(new ProjectChangedEventArgs
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name
                });
            }
        }

        /// <summary>
        /// Adds an image ID to the selected images list.
        /// </summary>
        public void AddSelectedImage(int id)
        {
            if (!SelectedImageIds.Contains(id))
            {
                SelectedImageIds.Add(id);
                PublishSelectionChanged();
            }
        }

        /// <summary>
        /// Removes an image ID from the selected images list.
        /// </summary>
        public void RemoveSelectedImage(int id)
        {
            if (SelectedImageIds.Remove(id))
            {
                PublishSelectionChanged();
            }
        }

        /// <summary>
        /// Clears all selected image IDs.
        /// </summary>
        public void ClearSelectedImages()
        {
            if (SelectedImageIds.Count > 0)
            {
                SelectedImageIds.Clear();
                PublishSelectionChanged();
            }
        }

        /// <summary>
        /// Replaces the entire selected images list with a new list.
        /// </summary>
        public void ReplaceSelectedImages(List<int> ids)
        {
            SelectedImageIds = ids ?? new List<int>();
            PublishSelectionChanged();
        }

        private void PublishSelectionChanged()
        {
            _events.Publish(new ImageSelectionChangedEventArgs
            {
                SelectedCount = SelectedImageIds.Count,
                SelectedImageIds = new List<int>(SelectedImageIds)
            });
        }
    }
}
