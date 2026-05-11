using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Orchestrates generation workflows, coordinates between specialized services,
    /// and manages complex multi-service operations.
    /// </summary>
    public interface IOrchestratorService
    {
        #region Event Publishing

        /// <summary>
        /// Publishes a ParametersChangedEventArgs for the specified mode.
        /// </summary>
        void InvokeParametersChanged(bool isImg2Img);

        /// <summary>
        /// Publishes a SessionVideosChangedEventArgs event.
        /// </summary>
        void InvokeSessionVideosChanged();

        #endregion

        #region Model Management

        /// <summary>
        /// Loads workflow models from the backend.
        /// </summary>
        Task GetWorkflowModels(bool refresh = false);

        /// <summary>
        /// Gets the models available for the current workflow.
        /// </summary>
        List<SDModel> GetCurrentWorkflowModels();

        /// <summary>
        /// Loads VAE models from the backend.
        /// </summary>
        Task GetSDVAEs();

        /// <summary>
        /// Loads ADetailer models from the backend.
        /// </summary>
        Task GetSDADetailerModels();

        /// <summary>
        /// Gets models available for the specified asset type.
        /// </summary>
        List<SDModel> GetModelsForAssetType(AssetType assetType);

        /// <summary>
        /// Gets available options for the specified asset type.
        /// </summary>
        Task<List<string>> GetAssetOptions(AssetType assetType);

        /// <summary>
        /// Gets the current model for the specified mode.
        /// </summary>
        string GetCurrentModel(ModeType? mode = null);

        /// <summary>
        /// Sets the current model and saves state.
        /// </summary>
        Task SetCurrentModel(string modelTitle, ModeType? mode = null);

        /// <summary>
        /// Sets the current SD model (alias for SetCurrentModel).
        /// </summary>
        Task SetSDModel(string modelTitle);

        /// <summary>
        /// Gets the current VAE for the specified mode.
        /// </summary>
        string? GetCurrentVae(ModeType? mode = null);

        /// <summary>
        /// Sets the current VAE and saves state.
        /// </summary>
        Task SetCurrentVae(string vae, ModeType? mode = null);

        #endregion

        #region Workflow Management

        /// <summary>
        /// Gets the currently active workflow.
        /// </summary>
        Workflow? GetCurrentWorkflow();

        /// <summary>
        /// Gets a workflow by its ID.
        /// </summary>
        Workflow GetWorkflowById(Guid id);

        /// <summary>
        /// Gets all workflows available for the specified mode.
        /// </summary>
        List<Workflow> GetWorkflowsForMode(ModeType mode);

        /// <summary>
        /// Gets enabled workflows for the specified (or current) base.
        /// </summary>
        List<Workflow> GetEnabledWorkflowsForBase(ModelBase? baseModel = null);

        /// <summary>
        /// Returns whether the workflow is currently disabled for Generate-page visibility.
        /// </summary>
        bool IsWorkflowDisabled(Guid workflowId);

        /// <summary>
        /// Enables or disables a workflow and persists the choice to application state.
        /// </summary>
        Task SetWorkflowEnabledAsync(Guid workflowId, bool enabled);

        /// <summary>
        /// Resolves the best workflow id to route to for the specified (or current) base.
        /// Prefers the last-used workflow for that base, falling back to the first available.
        /// Returns null if no workflow exists for the base.
        /// </summary>
        Guid? ResolveWorkflowForBase(ModelBase? baseModel = null);

        /// <summary>
        /// Loads workflows from the workflow service.
        /// </summary>
        void GetComfyWorkflows();

        /// <summary>
        /// Force refresh workflows from disk, reloading all template files.
        /// Use this after editing workflow template files during development.
        /// </summary>
        void RefreshWorkflowsFromDisk();

        /// <summary>
        /// Sets the current workflow by ID.
        /// </summary>
        void SetCurrentWorkflow(Guid workflowId);

        /// <summary>
        /// Sets the current workflow asynchronously with asset initialization.
        /// </summary>
        Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver);

        /// <summary>
        /// Sets the workflow base model type.
        /// </summary>
        void SetWorkflowBase(ModelBase workflowBase);

        /// <summary>
        /// Resets the current workflow selection.
        /// </summary>
        void ResetCurrentWorkflow();

        /// <summary>
        /// Sets the default model based on the current workflow.
        /// </summary>
        void SetDefaultBaseModel();

        #endregion

        #region Workflow Assets

        /// <summary>
        /// Gets a workflow asset value for the specified parameter.
        /// </summary>
        string? GetWorkflowAsset(string parameter);

        /// <summary>
        /// Sets a workflow asset value for the specified parameter.
        /// </summary>
        void SetWorkflowAsset(string parameter, string value);

        /// <summary>
        /// Gets the current workflow's assets dictionary (shared across modes).
        /// </summary>
        Dictionary<string, string>? GetWorkflowAssets();

        /// <summary>
        /// Gets the assets defined in the current workflow.
        /// </summary>
        List<WorkflowAsset>? GetCurrentWorkflowAssets();

        #endregion

        #region Backend

        /// <summary>
        /// Loads resources that depend on backend availability.
        /// </summary>
        Task LoadBackendDependentResources();

        #endregion

        #region Gallery

        /// <summary>
        /// Loads folders from the gallery service.
        /// </summary>
        Task GetFolders();

        /// <summary>
        /// Loads projects for the current folder.
        /// </summary>
        Task GetProjects();

        /// <summary>
        /// Sets the current folder and loads its projects.
        /// </summary>
        Task SetCurrentFolder(int id);

        /// <summary>
        /// Sets the current project.
        /// </summary>
        Task SetCurrentProject(int id);

        /// <summary>
        /// Replaces the selected images with the specified IDs.
        /// </summary>
        void ReplaceSelectedImages(List<int> ids);

        /// <summary>
        /// Adds an image to the selection.
        /// </summary>
        void AddSelectedImage(int id);

        /// <summary>
        /// Removes an image from the selection.
        /// </summary>
        void RemoveSelectedImage(int id);

        /// <summary>
        /// Clears all selected images.
        /// </summary>
        void ClearSelectedImages();

        #endregion

        #region Session

        /// <summary>
        /// Resets the image editor state.
        /// </summary>
        void ResetImageEditorState();

        /// <summary>
        /// Adds a video to the session.
        /// </summary>
        void AddSessionVideo(GeneratedVideo video);

        /// <summary>
        /// Adds multiple videos to the session.
        /// </summary>
        void AddSessionVideos(IEnumerable<GeneratedVideo> videos);

        /// <summary>
        /// Clears all session videos.
        /// </summary>
        void ClearSessionVideos();

        /// <summary>
        /// Removes a video from the session.
        /// </summary>
        void RemoveSessionVideo(GeneratedVideo video);

        #endregion

        #region Styles & Prompts

        /// <summary>
        /// Adds LoRAs to the current parameters.
        /// </summary>
        void SetLoras(IEnumerable<Lora> loras, bool isImg2Img);

        /// <summary>
        /// Parses a prompt, extracts LoRAs, resolves their relative paths against the
        /// available LoRAs on the backend, and cleans style text.
        /// </summary>
        Task<string> ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img);

        #endregion

        #region Parameter Loading

        /// <summary>
        /// Loads parameters from an image entity.
        /// </summary>
        Task LoadImageInfoParameters(Image image, ModeType mode);

        /// <summary>
        /// Sets a single generation parameter from an image (applies immediately).
        /// Use for same-page parameter updates.
        /// </summary>
        Task SetGenerationParameter(Image source, string parameter, bool isImg2Img);

        /// <summary>
        /// Queues a generation parameter override from an image.
        /// Use when navigating to a different workflow � overrides are applied after InitializeFromWorkflowAsync.
        /// </summary>
        Task QueueGenerationParameter(Image source, string parameter, bool isImg2Img);

        #endregion

        #region State & Settings

        /// <summary>
        /// Loads application settings.
        /// </summary>
        void LoadSettings();

        /// <summary>
        /// Saves application settings.
        /// </summary>
        void SaveSettings();

        /// <summary>
        /// Loads application state.
        /// </summary>
        Task LoadState(State? state = null);

        /// <summary>
        /// Saves application state.
        /// </summary>
        Task SaveState(State? state = null);

        #endregion
    }
}
