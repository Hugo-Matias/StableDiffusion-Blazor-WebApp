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
        private HashSet<int> _selectedImageIdSet = new();

        /// <summary>
        /// Indicates whether gallery filters are currently applied.
        /// </summary>
        public bool IsGalleryFiltered { get; set; }

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
        /// Returns whether an image is selected using the selection lookup cache.
        /// </summary>
        public bool IsImageSelected(int id) => _selectedImageIdSet.Contains(id);

        /// <summary>
        /// Adds an image ID to the selected images list.
        /// </summary>
        public void AddSelectedImage(int id)
        {
            if (_selectedImageIdSet.Add(id))
            {
                SelectedImageIds.Add(id);
                PublishSelectionChanged(new HashSet<int> { id }, true);
            }
        }

        /// <summary>
        /// Adds multiple image IDs and publishes a single selection change event.
        /// </summary>
        public void AddSelectedImages(IEnumerable<int> ids)
        {
            if (ids == null)
            {
                return;
            }

            var addedIds = new HashSet<int>();
            foreach (var id in ids)
            {
                if (_selectedImageIdSet.Add(id))
                {
                    SelectedImageIds.Add(id);
                    addedIds.Add(id);
                }
            }

            if (addedIds.Count > 0)
            {
                PublishSelectionChanged(addedIds, true);
            }
        }

        /// <summary>
        /// Removes an image ID from the selected images list.
        /// </summary>
        public void RemoveSelectedImage(int id)
        {
            if (_selectedImageIdSet.Remove(id))
            {
                SelectedImageIds.Remove(id);
                PublishSelectionChanged(new HashSet<int> { id }, false);
            }
        }

        /// <summary>
        /// Clears all selected image IDs.
        /// </summary>
        public void ClearSelectedImages()
        {
            if (SelectedImageIds.Count > 0)
            {
                var changedIds = new HashSet<int>(_selectedImageIdSet);
                SelectedImageIds.Clear();
                _selectedImageIdSet.Clear();
                PublishSelectionChanged(changedIds, false);
            }
        }

        /// <summary>
        /// Replaces the entire selected images list with a new list.
        /// </summary>
        public void ReplaceSelectedImages(List<int> ids)
        {
            var nextIds = ids?.Distinct().ToList() ?? new List<int>();
            var nextSet = new HashSet<int>(nextIds);
            var changedIds = new HashSet<int>(_selectedImageIdSet);
            changedIds.SymmetricExceptWith(nextSet);

            SelectedImageIds = nextIds;
            _selectedImageIdSet = nextSet;
            PublishSelectionChanged(changedIds, null);
        }

        private void PublishSelectionChanged(IReadOnlySet<int>? changedImageIds, bool? changedSelectionState)
        {
            _events.Publish(new ImageSelectionChangedEventArgs
            {
                SelectedCount = SelectedImageIds.Count,
                SelectedImageIds = new List<int>(SelectedImageIds),
                ChangedImageIds = changedImageIds,
                ChangedSelectionState = changedSelectionState
            });
        }
    }
}
