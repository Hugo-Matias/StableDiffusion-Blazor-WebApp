using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Interface for database operations using Entity Framework Core DbContext.
    /// Provides comprehensive CRUD operations for all application entities.
    /// </summary>
    public interface IDatabaseService
    {
        // Properties
        int PageSize { get; set; }

        // Database Initialization
        Task InitializeDatabase();

        // Folder Operations
        Task<List<Folder>> GetFolders();
        Task<List<Folder>> GetFolders(string name);
        Task<Folder> GetFolder(string name);
        Task<Folder> GetFolder(int id);
        Task<Folder> CreateFolder(Folder folder);
        Task DeleteFolder(int folderId);
        Task UpdateFolderSortOrder(int folderId, int newSortOrder);

        // Project Operations
        Task<List<Project>> GetProjects(int folderId = 0);
        Task<Project> GetProject(int id);
        Task<Project> GetProject(string name);
        Task<Project> GetLatestProject();
        Task<List<Project>?> GetLastUsedProjects(int amount, int[] ignoreIds);
        Task<Project> CreateProject(Project project);
        Task<Project> UpdateProject(int projectId, Project data);
        Task<Project> UpdateProject(Project project);
        Task<Project?> DeleteProject(int projectId);
        Task RenameProject(int projectId, string name);
        Task SetProjectCover(int projectId, string imagePath);
        Task SetProjectFolder(int projectId, int folderId);
        Task<string> GetSampleImage(int projectId);

        // Selection Operations
        Task<List<Selection>> GetSelections();
        Task<List<Selection>> GetSelectionsByName(string name);
        Task<Selection> GetSelection(int id);
        Task CreateSelection(Selection selection);
        Task UpdateSelection(int selectionId, List<int> imageIds);
        Task<Selection?> DeleteSelection(int id);

        // Image Operations
        Task<Image> AddImage(Image image);
        Task<List<Image>> GetImages(List<int> imageIds);
        Task<Image?> GetImageById(int id);
        Task<ImagesDto> GetPagedImages(int page);
        Task<ImagesDto> GetPagedImages(int page, int projectId);
        Task<ImagesDto> GetPagedImages(int page, List<int> imageIds);
        Task<ImagesDto> GetSortedImages(int page, int projectId, AppStateGallery state);
        Task<ImagesDto> GetRandomImages(int amount);
        Task<Image> GetRandomFavorite(int projectId);
        Task<List<Image>> GetRecentImagesWithPrompts(int limit = 10000);
        Task<Image> UpdateImage(Image image);
        Task UpdateImages(List<Image> images);
        Task<Image> DeleteImage(Image image);

        // Sampler Operations
        Task<string> GetSampler(int id);
        Task<int> GetSamplerIdByName(string samplerName);

        // Mode Operations
        Task<int> GetMode(ModeType mode);
        Task<ModeType> GetMode(int id);

        // Prompt Operations
        Task<List<Prompt>> GetPrompts(string positive = "", string negative = "");
        Task<List<Prompt>> GetPromptsByCategory(string? category);
        Task<List<Prompt>> GetPromptsByTags(List<string> tags);
        Task<List<Prompt>> GetPinnedPrompts();
        Task<List<Prompt>> GetFavoritePrompts();
        Task<List<string>> GetAllPromptCategories();
        Task<List<string>> GetAllPromptTags();
        Task UpdatePromptUsage(int promptId);
        Task<List<Prompt>> SearchPromptsWithKeywords(string query);
        Task CreatePrompt(Prompt prompt);
        Task UpdatePrompt(PromptResource prompt);
        Task DeletePrompt(int id);

        // Resource Operations
        Task<List<Resource>> GetResources();
        Task<List<Resource>> GetResources(int typeId);
        Task<List<Resource>> GetResources(int? typeId = null, string? baseModel = null);
        Task<Resource> GetResourceById(int id);
        Task<List<string>> GetDistinctBaseModels();
        Task<List<string>> GetDistinctBaseModels(int typeId);
        Task<Resource> GetResourceByCivitaiModelId(int civitaiId);
        Task<Resource> GetResourceByFilename(string filename);
        Task<List<ResourceType>> GetResourceTypes(bool ordered);
        Task<IEnumerable<ResourceSubType>> GetResourceSubTypes(bool ordered);
        Task<IEnumerable<ResourceSubType>> GetResourceSubTypes(string name, bool ordered);
        Task<IEnumerable<string>> GetResourceSubtypeNames(string name);
        Task<bool> CreateResource(Resource resource);
        Task<Resource> UpdateResource(Resource resource);
        Task UpdateResourceLoadedDate(int id);
        Task DeleteResource(int resourceId);
        Task ToggleResourceState(int resourceId);
        Task<bool> CheckResourceExistsByFilename(string filename);
        Task<bool> CheckResourceExistsByModelId(int id);
        Task<bool> CheckResourceExistsByModelVersionId(int id);
        Task<List<int>> GetActiveResourceIds();

        // Resource Image Operations
        Task<bool> CreateResourceImage(ResourceImage image);
        Task<List<ResourceImage>> GetResourceImages(int id);
        Task<ImagesDto> GetRandomResourceImages(int amount);
        Task<int> ResourceImageByModelVersionIdCount(int id);
        Task DeleteResourceImage(int civitaiModelVersionId);

        // State Operations
        Task<State> CreateState(State state);
        Task<List<State>> GetStates(int stateVersion);
        Task<State?> GetState(int id);
        Task<State> UpdateState(State state);
        Task DeleteState(int id);

        // Resource Template Operations
        Task<ResourceTemplate> CreateResourceTemplate(ResourceTemplate template);
        Task<List<ResourceTemplate>> GetResourceTemplates();
        Task<ResourceTemplate?> GetResourceTemplate(int id);
        Task<ResourceTemplate> UpdateResourceTemplate(ResourceTemplate template);
        Task DeleteResourceTemplate(int id);
    }
}
