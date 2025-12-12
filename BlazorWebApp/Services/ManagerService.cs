using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras, Img2VidSamples }

    public class ManagerService
    {
        private readonly DatabaseService _db;
        private readonly IOService _io;
        private readonly ProgressService _progress;
        private readonly IConfiguration _configuration;
        private readonly ComfyUIService _capi;
        private readonly WorkflowService _workflow;
        private readonly IStateService _state;
        private readonly IEventService _events;
        private readonly ISettingsService _settings;
        private readonly IBackendService _backend;
        private readonly IModelService _models;
        private readonly IGalleryService _gallery;
        private readonly ISessionService _session;
        private int _currentProgress;
        private bool _isConverging;
        private bool _isWebuiUp;
        private bool _isComfyUIUp;

        public event Action OnSDModelsChange;
        public event Action OnOptionsChange;
        public event Action OnStyleChange;
        public event Action OnConverging;
        public event Action OnFolderChange;
        public event Action OnProjectsChange;
        public event Action OnProjectChange;
        public event Func<Task> OnProjectChangeTask;
        public event Action OnStateHasChanged;
        public event Action OnProgressChanged;
        public event Action OnDownloadCompleted;
        public event Action OnWebuiStateChanged;
        public event Action OnComfyUIStateChanged;
        public event Action OnAppStateChanged;
        public event Action OnTxt2ImgParametersChanged;
        public event Action OnImg2ImgParametersChanged;
        public event Action OnUpscaleParametersChanged;
        public event Action OnImg2VidParametersChanged;
        public event Action OnSelectedImagesChanged;
        public event Action OnRefreshImagesContainer;
        public event Action OnCanvasImageDataChanged;
        public event Action OnImg2VidInputImageChanged;
        public event Action OnImg2ImgInputImageChanged;
        public event Action OnResourcesStateChanged;
        public event Action OnWorkflowBaseChanged;
        public event Action? OnImageEditorStateChanged;
        public event Action? OnSamplersSchedulersChanged;

        /// <summary>
        /// Fired when the current workflow changes (via SetCurrentWorkflow).
        /// Subscribe to this event to update UI components that depend on workflow assets.
        /// </summary>
        public event Action OnCurrentWorkflowChanged;

        /// <summary>
        /// Fired when the current workflow changes. Async version for components that need to await.
        /// </summary>
        public event Func<Workflow?, Task>? OnCurrentWorkflowChangedAsync;

        // Temporary facade - delegates to StateService (will be removed in Phase 8)
        public AppState State => _state.State;
        public Txt2ImgParameters ParametersTxt2Img => _state.ParametersTxt2Img;
        public Img2ImgParameters ParametersImg2Img => _state.ParametersImg2Img;
        public Models.UpscaleParameters ParametersUpscale => _state.ParametersUpscale;
        public Img2VidParameters ParametersImg2Vid => _state.ParametersImg2Vid;

        // Temporary facade - delegates to SettingsService (will be removed in Phase 8)
        public AppSettings Settings => _settings.Settings;

        public Options Options { get; set; }

        public GeneratedImages Images { get; set; }
        public GeneratedImagesInfo ImagesInfo { get; set; }
        public ImagesDto GeneratedImageEntities { get; set; }
        public string? GridImage { get; set; }
        public InferenceProgress Progress { get; set; }

        // Temporary facade - delegates to ModelService (will be removed in Phase 8)
        public List<SDModel> CheckpointModels => _models.CheckpointModels;
        public List<SDModel> DiffusionModels => _models.DiffusionModels;
        public List<string> SDVAEs => _models.VAEModels;
        public List<string> ClipModels => _models.ClipModels;
        public List<string> ClipVisionModels => _models.ClipVisionModels;
        public List<string> SDADetailerModels => _models.ADetailerModels;

        // Temporary facade - delegates to BackendService (will move to ModelService in Phase 5)
        public List<Models.Sampler> Samplers => _backend.Samplers;
        public List<Scheduler> Schedulers => _backend.Schedulers;
        public List<Upscaler> Upscalers => _backend.Upscalers;

        public List<PromptStyle> Styles { get; set; }

        // Temporary facade - delegates to GalleryService (will be removed in Phase 8)
        public List<Folder>? Folders => _gallery.Folders;
        public List<Project>? Projects => _gallery.Projects;
        public List<int> SelectedImageIds => _gallery.SelectedImageIds;

        public int CurrentProgress
        {
            get => _currentProgress; set
            {
                _currentProgress = value;
                OnProgressChanged?.Invoke();
            }
        }

        // Temporary facade - delegates to SessionService (will be removed in Phase 8)
        public List<string> CanvasStates => _session.CanvasStates;

        public string CanvasImageData
        {
            get => _session.CanvasImageData;
            set
            {
                _session.CanvasImageData = value;
                OnCanvasImageDataChanged?.Invoke();
            }
        }

        public string CanvasMaskData
        {
            get => _session.CanvasMaskData;
            set => _session.CanvasMaskData = value;
        }

        public string UpscaleImageData
        {
            get => _session.UpscaleImageData;
            set => _session.UpscaleImageData = value;
        }

        /// <summary>
        /// Input image data for Img2Vid generation (stored separately from parameters due to size)
        /// </summary>
        public string Img2VidInputImage
        {
            get => _session.Img2VidInputImage;
            set
            {
                _session.Img2VidInputImage = value;
                OnImg2VidInputImageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Input image data for Img2Img generation (stored separately from parameters due to size)
        /// </summary>
        public string Img2ImgInputImage
        {
            get => _session.Img2ImgInputImage;
            set
            {
                _session.Img2ImgInputImage = value;
                OnImg2ImgInputImageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Image editor state for session-level persistence.
        /// Survives page navigation within the same browser session.
        /// </summary>
        public ImageEditorState ImageEditorState
        {
            get => _session.ImageEditorState;
            set
            {
                _session.ImageEditorState = value;
                OnImageEditorStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Resets the image editor state, clearing all edits and layers.
        /// Call this when the user manually changes the input image (not from editor output).
        /// </summary>
        public void ResetImageEditorState()
        {
            _session.ResetImageEditorState();
            OnImageEditorStateChanged?.Invoke();
        }

        /// <summary>
        /// Sets the Img2Img input image and optionally resets the editor state.
        /// Use resetEditorState=true when user is loading a new image (not from editor output).
        /// Use resetEditorState=false when setting from editor output.
        /// </summary>
        public void SetImg2ImgInputImage(string imageData, bool resetEditorState)
        {
            _session.SetImg2ImgInputImage(imageData, resetEditorState);
            OnImg2ImgInputImageChanged?.Invoke();
        }

        /// <summary>
        /// Session-persisted generated videos for Img2Vid
        /// </summary>
        public GeneratedVideos SessionGeneratedVideos => _session.SessionGeneratedVideos;

        public event Action OnSessionVideosChanged;

        public void AddSessionVideo(GeneratedVideo video)
        {
            _session.AddSessionVideo(video);
            OnSessionVideosChanged?.Invoke();
        }

        public void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
        {
            _session.AddSessionVideos(videos);
            OnSessionVideosChanged?.Invoke();
        }

        public void ClearSessionVideos()
        {
            _session.ClearSessionVideos();
            OnSessionVideosChanged?.Invoke();
        }

        public void InvokeSessionVideosChanged()
        {
            OnSessionVideosChanged?.Invoke();
        }

        public void RemoveSessionVideo(GeneratedVideo video)
        {
            _session.RemoveSessionVideo(video);
            OnSessionVideosChanged?.Invoke();
        }

        public bool ControlNetEnabled { get; set; }
        public UpscaledImageDto GeneratedUpscaleImage { get; set; }
        public bool IsGalleryFiltered { get; set; }
        public PromptButton ButtonTags { get; set; }
        public CmdFlags CmdFlags { get; set; }
        public CivitaiModelsDto CivitaiModels { get; set; }
        public CivitaiImagesDto CivitaiImages { get; set; }
        public CivitaiCreatorsDto CivitaiCreators { get; set; }
        public string ComfyWSClientId { get; set; }
        public Dictionary<string, string> ResourceTypeDirectories { get; set; }
        public bool IsConverging
        {
            get => _isConverging;
            set
            {
                _isConverging = value;

                // Fire old Action event for backward compatibility (will be removed in Phase 8)
                OnConverging?.Invoke();

                // Publish typed event for migrated components using EventService
                _events.Publish(new ConvergingChangedEventArgs(_isConverging));
            }
        }

        // Temporary facade - delegates to BackendService (will be removed in Phase 8)
        public bool IsWebuiUp
        {
            get => false; // WebUI no longer supported
            set { } // No-op for backward compatibility
        }

        // Temporary facade - delegates to BackendService (will be removed in Phase 8)
        public bool IsComfyUIUp => _backend.IsBackendAvailable;

        public ManagerService(DatabaseService db, IOService io, ProgressService progress, IConfiguration configuration, ComfyUIService capi, WorkflowService workflow, IStateService state, IEventService events, ISettingsService settings, IBackendService backend, IModelService models, IGalleryService gallery, ISessionService session)
        {
            _db = db;
            _io = io;
            _progress = progress;
            _configuration = configuration;
            _capi = capi;
            _workflow = workflow;
            _state = state;
            _events = events;
            _settings = settings;
            _backend = backend;
            _models = models;
            _gallery = gallery;
            _session = session;

            // SettingsService now handles loading settings automatically
            // Note: LoadState() must be called asynchronously after construction
            // The StateService already initializes with defaults in its constructor

            // Initialize to empty lists - these will be populated when the backend comes online
            Images = new();
            Progress = new();
            _db.PageSize = State.Gallery.PageSize;
            State.Gallery.DateRange = new(DateTime.Now.Date.AddDays(-5), DateTime.Now.Date);

            GetButtonTags();
        }

        public void InvokeDownloadComplete() => OnDownloadCompleted?.Invoke();

        public void InvokeRefreshImagesContainer() => OnRefreshImagesContainer?.Invoke();

        public void InvokeResourcesStateChanged() => OnResourcesStateChanged?.Invoke();

        public void InvokeProgressChanged() => OnProgressChanged?.Invoke();

        public void InvokeParametersChanged(bool isImg2Img)
        {
            if (isImg2Img) OnImg2ImgParametersChanged?.Invoke();
            else OnTxt2ImgParametersChanged?.Invoke();
        }

        public void InitializeParameters(ModeType[] modes)
        {
            _state.InitializeParameters(modes);
        }

        /// <summary>
        /// Loads models based on the current workflow's asset requirements.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task GetWorkflowModels(bool refresh = false)
        {
            await _models.GetWorkflowModels(refresh);
            OnSDModelsChange?.Invoke();
        }

        /// <summary>
        /// Gets the models for the current workflow based on its asset types.
        /// Returns DiffusionModels if workflow uses diffusion, otherwise CheckpointModels.
        /// </summary>
        public List<SDModel> GetCurrentWorkflowModels()
        {
            var workflow = GetCurrentWorkflow();
            if (workflow?.Assets != null && workflow.Assets.Any(a => a.Type == AssetType.DiffusionModel))
            {
                return DiffusionModels ?? new List<SDModel>();
            }
            return CheckpointModels ?? new List<SDModel>();
        }

        /// <summary>
        /// Legacy method - calls GetWorkflowModels for backward compatibility.
        /// </summary>
        [Obsolete("Use GetWorkflowModels() instead")]
        public Task GetSDModels(bool refresh = false) => GetWorkflowModels(refresh);

        /// <summary>
        /// Gets the currently active workflow based on CurrentWorkflowId.
        /// Falls back to first workflow matching WorkflowBase if CurrentWorkflowId is not set.
        /// </summary>
        public Workflow? GetCurrentWorkflow()
        {
            if (State?.Generation?.Workflows == null || State.Generation.Workflows.Count == 0)
                return null;

            // Priority 1: Use CurrentWorkflowId if set
            if (State.Generation.CurrentWorkflowId.HasValue)
            {
                var workflow = State.Generation.Workflows.FirstOrDefault(w => w.Id == State.Generation.CurrentWorkflowId.Value);
                if (workflow != null)
                    return workflow;
            }

            // Priority 2: Fallback to first workflow matching WorkflowBase
            if (State.Generation.WorkflowBase != default)
            {
                return State.Generation.Workflows.FirstOrDefault(w => w.Base == State.Generation.WorkflowBase);
            }

            // Priority 3: Return first available workflow
            return State.Generation.Workflows.FirstOrDefault();
        }

        /// <summary>
        /// Loads VAE models from the backend.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task GetSDVAEs()
        {
            await _models.GetVAEModels();
        }

        /// <summary>
        /// Loads ADetailer models from the backend.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task GetSDADetailerModels()
        {
            await _models.GetADetailerModels();
        }

        /// <summary>
        /// Legacy method - use SetCurrentModel instead.
        /// </summary>
        [Obsolete("Use SetCurrentModel() instead")]
        public async Task SetSDModel(string modelTitle)
        {
            await SetCurrentModel(modelTitle);
        }

        /// <summary>
        /// Legacy method - use SetCurrentVae instead.
        /// </summary>
        [Obsolete("Use SetCurrentVae() instead")]
        public async Task SetVae(string vae)
        {
            await SetCurrentVae(vae);
        }

        public async Task GetOptions()
        {
            await _backend.GetOptions();
            Options = _backend.Options;
            OnOptionsChange?.Invoke();
        }

        public async Task GetStyles()
        {
            Styles = new(); // No longer fetching from WebUI
            var promptResources = await _db.GetPrompts();
            foreach (var prompt in promptResources)
            {
                Styles.Add(new(prompt));
            }
            if (State.Generation.Styles == null) State.Generation.Styles = new List<PromptStyle>();
            else
            {
                var currentStyles = State.Generation.Styles.ToList();
                State.Generation.Styles = Styles.Where(s => currentStyles.Any(cs => cs.Name == s.Name));
            }
        }

        public async Task GetFolders()
        {
            await _gallery.GetFolders();
            OnFolderChange?.Invoke();
        }

        public async Task GetProjects()
        {
            await _gallery.GetProjects(State.Gallery.FolderId);
            if (State.Gallery.GalleriesOrderDescending) Projects.Reverse();
            OnProjectsChange?.Invoke();
        }

        public void GetButtonTags() => ButtonTags = JsonSerializer.Deserialize<PromptButton>(_io.GetJsonAsString("Data/example.json"), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        public async Task SetCurrentFolder(int id)
        {
            if (id > 0)
            {
                await GetFolders();
                State.Gallery.FolderId = id;
                State.Gallery.FolderName = Folders.FirstOrDefault(f => f.Id == id)!.Name;
            }
            else
            {
                State.Gallery.FolderId = 0;
                State.Gallery.FolderName = "All";
            }
            SaveState();
            await GetProjects();
            OnFolderChange?.Invoke();
        }

        public async Task SetCurrentProject(int id)
        {
            await GetFolders();
            await GetProjects();

            // Delegate to GalleryService which will update state and fire the new ProjectChangedEventArgs event
            await _gallery.SetCurrentProject(id);

            // Fire old events for backward compatibility (will be removed in Phase 8)
            OnProjectChange?.Invoke();
            OnProjectChangeTask?.Invoke();
        }

        /// <summary>
        /// Loads all backend-dependent resources (samplers, schedulers, upscalers, ADetailer models).
        /// Call this method when the backend comes online.
        /// Temporary facade - delegates to BackendService and ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task LoadBackendDependentResources()
        {
            await _backend.LoadBackendDependentResources();
            await _models.GetADetailerModels();
            OnSamplersSchedulersChanged?.Invoke();
        }

        public void GetComfyWorkflows() => State.Generation.Workflows = _workflow.GetWorkflows();

        /// <summary>
        /// Refreshes workflows from disk template files.
        /// This ensures that any changes to workflow templates are picked up on application restart.
        /// Preserves the current WorkflowBase and CurrentWorkflowId selection if the workflow still exists.
        /// </summary>
        private void RefreshWorkflowsFromDisk()
        {
            try
            {
                var currentWorkflowBase = State?.Generation?.WorkflowBase;
                var currentWorkflowId = State?.Generation?.CurrentWorkflowId;

                // Load workflows from disk
                GetComfyWorkflows();

                // Try to restore the previous workflow selection
                if (State?.Generation?.Workflows != null && State.Generation.Workflows.Count > 0)
                {
                    // First, try to find workflow by ID (most specific)
                    if (currentWorkflowId.HasValue)
                    {
                        var matchById = State.Generation.Workflows.FirstOrDefault(w => w.Id == currentWorkflowId.Value);
                        if (matchById != null)
                        {
                            State.Generation.CurrentWorkflowId = matchById.Id;
                            State.Generation.WorkflowBase = matchById.Base;
                            return;
                        }
                    }

                    // Fallback: try to match by base
                    if (currentWorkflowBase != default)
                    {
                        var matchByBase = State.Generation.Workflows.FirstOrDefault(w => w.Base == currentWorkflowBase);
                        if (matchByBase != null)
                        {
                            State.Generation.CurrentWorkflowId = matchByBase.Id;
                            State.Generation.WorkflowBase = matchByBase.Base;
                            return;
                        }
                    }

                    // Last resort: use first available workflow
                    var firstWorkflow = State.Generation.Workflows.FirstOrDefault();
                    if (firstWorkflow != null)
                    {
                        State.Generation.CurrentWorkflowId = firstWorkflow.Id;
                        State.Generation.WorkflowBase = firstWorkflow.Base;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - workflows will be empty but app should still work
                Console.WriteLine($"Error refreshing workflows from disk: {ex.Message}");
            }
        }

        public Workflow GetWorkflowById(Guid id) => State.Generation.Workflows.FirstOrDefault(w => w.Id == id);

        /// <summary>
        /// Sets the current workflow by ID and fires change events.
        /// Updates CurrentWorkflowId in state and triggers OnCurrentWorkflowChanged.
        /// </summary>
        /// <param name="workflowId">The workflow ID to set as current</param>
        /// <param name="mode">Optional mode to also set WorkflowAssets for (defaults to Txt2Img)</param>
        public void SetCurrentWorkflow(Guid workflowId, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return;

            State.Generation.CurrentWorkflowId = workflowId;
            State.Generation.WorkflowBase = workflow.Base;

            OnWorkflowBaseChanged?.Invoke();
            OnCurrentWorkflowChanged?.Invoke();
        }

        /// <summary>
        /// Sets the current workflow by ID and initializes workflow assets.
        /// Use this when you want to resolve asset values against ComfyUI.
        /// </summary>
        /// <param name="workflowId">The workflow ID to set as current</param>
        /// <param name="assetResolver">The asset resolver service to use for initialization</param>
        /// <param name="mode">The mode to initialize assets for</param>
        /// <returns>True if workflow was set and assets initialized successfully</returns>
        public async Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return false;

            State.Generation.CurrentWorkflowId = workflowId;
            State.Generation.WorkflowBase = workflow.Base;

            // Initialize workflow assets if workflow has assets defined
            bool assetsInitialized = true;
            if (workflow.Assets != null && workflow.Assets.Count > 0)
            {
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assetsInitialized = await assetResolver.InitializeWorkflowAssets(workflow, assets);
            }

            // Load models for this workflow
            await GetWorkflowModels();

            OnWorkflowBaseChanged?.Invoke();
            OnCurrentWorkflowChanged?.Invoke();

            // Fire async event
            if (OnCurrentWorkflowChangedAsync != null)
            {
                await OnCurrentWorkflowChangedAsync.Invoke(workflow);
            }

            await SaveState();
            return assetsInitialized;
        }

        /// <summary>
        /// Gets the list of workflows available for a specific mode.
        /// Filters workflows by the mode type they support.
        /// </summary>
        /// <param name="mode">The mode to filter workflows for</param>
        /// <returns>List of workflows that support the specified mode</returns>
        public List<Workflow> GetWorkflowsForMode(ModeType mode)
        {
            if (State?.Generation?.Workflows == null)
                return new List<Workflow>();

            return State.Generation.Workflows
                .Where(w => w.Mode == mode)
                .OrderBy(w => w.Title)
                .ToList();
        }

        public void SetWorkflowBase(ModelBase workflowBase)
        {
            var previousBase = State.Generation.WorkflowBase;
            State.Generation.WorkflowBase = workflowBase;

            // If base changed, reset workflow assets to use workflow defaults
            if (previousBase != workflowBase)
            {
                ResetWorkflowAssetsToDefaults();
            }

            // Fire legacy Action event for components that haven't been migrated yet
            OnWorkflowBaseChanged?.Invoke();

            // Publish StateChangedEventArgs for migrated components using EventService
            _events.Publish(new StateChangedEventArgs());

            SetDefaultBaseModel();
        }

        /// <summary>
        /// Resets all workflow assets for all modes to use the workflow template defaults.
        /// This is called when WorkflowBase changes to ensure the correct models are loaded.
        /// </summary>
        private void ResetWorkflowAssetsToDefaults()
        {
            // Get the new workflow for each mode and reset assets to its defaults
            var modes = new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid, ModeType.Extras };

            foreach (var mode in modes)
            {
                var workflows = GetWorkflowsForMode(mode);
                var workflow = workflows?.FirstOrDefault(w => w.Base == State.Generation.WorkflowBase);

                if (workflow?.Assets == null || workflow.Assets.Count == 0)
                    continue;

                // Clear existing assets for this mode and set to workflow defaults
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assets.Clear();

                foreach (var asset in workflow.Assets)
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue))
                    {
                        assets[asset.Parameter] = asset.DefaultValue;
                    }
                }
            }
        }

        /// <summary>
        /// Resets the current workflow ID and workflow assets to their default values.
        /// This is typically used when clearing the current generation settings.
        /// </summary>
        public void ResetCurrentWorkflow()
        {
            State.Generation.CurrentWorkflowId = null;
            State.Generation.WorkflowBase = default;

            // Reset all workflow assets to defaults
            var modes = new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid, ModeType.Extras };
            foreach (var mode in modes)
            {
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assets.Clear();
            }

            OnCurrentWorkflowChanged?.Invoke();
        }

        /// <summary>
        /// Sets the default model for the current workflow based on its pipeline configuration.
        /// This is called after setting the workflow base to ensure the correct default model is used.
        /// </summary>
        public void SetDefaultBaseModel()
        {
            var modelKeys = new[] { "ckpt_name", "unet_name" };
            var workflow = State.Generation.Workflows?.FirstOrDefault(w => w.Base == State.Generation.WorkflowBase);
            if (workflow?.Pipeline != null)
            {
                var defaultModel = workflow.Pipeline
                    .Select(s => modelKeys
                        .FirstOrDefault(k => s.Parameters?.ContainsKey(k) == true))
                    .Where(k => k != null)
                    .Select(k => workflow.Pipeline
                        .FirstOrDefault(s => s.Parameters?.ContainsKey(k) == true)?
                        .Parameters[k]?.ToString())
                    .FirstOrDefault()?
                    .GetDefaultModelFromWorkflow();

                if (!string.IsNullOrWhiteSpace(defaultModel))
                {
                    // Set model on Txt2Img parameters (primary mode) using WorkflowAssets
                    if (ParametersTxt2Img != null)
                    {
                        ParametersTxt2Img.WorkflowAssets ??= new Dictionary<string, string>();
                        ParametersTxt2Img.WorkflowAssets["Model"] = defaultModel;
                    }
                    OnSDModelsChange?.Invoke();
                }
            }
        }

        public void SetLoras(IEnumerable<Lora> loras, bool isImg2Img)
        {
            if (loras == null) return;
            if (State == null || State.Generation == null) return;

            var parametersLoras = isImg2Img ? ParametersImg2Img.Loras : ParametersTxt2Img.Loras;

            parametersLoras ??= [];

            foreach (var l in loras)
            {
                if (string.IsNullOrWhiteSpace(l.Name)) continue;
                var exists = parametersLoras.Any(x => string.Equals(x.Name, l.Name, StringComparison.InvariantCultureIgnoreCase));
                if (!exists)
                    parametersLoras.Add(new Lora(l));
            }

            OnAppStateChanged?.Invoke();
        }

        /// <summary>
        /// Parses prompt string, removes lora tags, registers found Loras, removes styles and returns the cleaned prompt.
        /// </summary>
        public string ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img)
        {
            // Extract and register Loras
            var loras = Parser.ExtractLorasFromPrompt(prompt, out var cleanedFromLoras, isNegative);
            SetLoras(loras, isImg2Img);

            // Remove styles
            var cleanedFromStyles = cleanedFromLoras;
            if (State?.Generation?.Styles != null)
            {
                foreach (var style in State.Generation.Styles)
                {
                    var styleText = isNegative ? style.NegativePrompt : style.Prompt;
                    if (!string.IsNullOrWhiteSpace(styleText))
                    {
                        var actualStyleText = styleText.Replace("{prompt}", "");
                        if (!string.IsNullOrWhiteSpace(actualStyleText))
                        {
                            // Remove the style text and any following comma and spaces
                            cleanedFromStyles = Regex.Replace(cleanedFromStyles,
                                $@"{Regex.Escape(actualStyleText)},?\s*",
                                "", RegexOptions.IgnoreCase);
                        }
                    }
                }

                // Clean up the resulting string
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s*,\s*,\s*", ", "); // Fix double commas
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"^\s*,\s*|\s*,\s*$", ""); // Remove leading/trailing commas
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s+", " ").Trim(); // Normalize spaces
            }

            return cleanedFromStyles;
        }

        public async Task LoadImageInfoParameters(Image image, ModeType mode)
        {
            bool isImg2Img = mode == ModeType.Img2Img;

            if (isImg2Img)
            {
                ParametersImg2Img.Prompt = ParseAndCleanCopiedPrompt(image.Prompt ?? string.Empty, false, isImg2Img);
                ParametersImg2Img.NegativePrompt = ParseAndCleanCopiedPrompt(image.NegativePrompt ?? string.Empty, true, isImg2Img);
                ParametersImg2Img.SamplerIndex = await _db.GetSampler(image.SamplerId);
                ParametersImg2Img.Steps = image.Steps;
                ParametersImg2Img.Seed = image.Seed;
                ParametersImg2Img.CfgScale = image.CfgScale;
                ParametersImg2Img.Width = image.Width;
                ParametersImg2Img.Height = image.Height;
                ParametersImg2Img.DenoisingStrength = image.DenoisingStrength;
            }
            else
            {
                ParametersTxt2Img.Prompt = ParseAndCleanCopiedPrompt(image.Prompt ?? string.Empty, false, isImg2Img);
                ParametersTxt2Img.NegativePrompt = ParseAndCleanCopiedPrompt(image.NegativePrompt ?? string.Empty, true, isImg2Img);
                ParametersTxt2Img.SamplerIndex = await _db.GetSampler(image.SamplerId);
                ParametersTxt2Img.Steps = image.Steps;
                ParametersTxt2Img.Seed = image.Seed;
                ParametersTxt2Img.CfgScale = image.CfgScale;
                ParametersTxt2Img.Width = image.Width;
                ParametersTxt2Img.Height = image.Height;
                ParametersTxt2Img.DenoisingStrength = image.DenoisingStrength;
            }
        }

        public string GetCurrentSaveFolder(Outdir? outdir)
        {
            string path;

            switch (outdir)
            {
                case Outdir.Txt2ImgSamples:
                    path = Options.OutdirSamplesTxt2Img;
                    break;
                case Outdir.Txt2ImgGrid:
                    path = Options.OutdirGridTxt2Img;
                    break;
                case Outdir.Img2ImgSamples:
                    path = Options.OutdirSamplesImg2Img;
                    break;
                case Outdir.Img2ImgGrid:
                    path = Options.OutdirGridImg2Img;
                    break;
                case Outdir.Extras:
                    return Options.OutdirSamplesExtras;
                default:
                    return string.Empty;
            }

            return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir, Parser.ModeTypeFromOutdir((Outdir)outdir))).Replace('/', Path.DirectorySeparatorChar);
        }

        public string ConvertPathPattern(string pattern, ModeType mode)
        {
            var rg = new Regex(@"(\[.+?\])");

            return rg.Replace(pattern, (t) => ConvertPathTag(t.Value, mode));
        }

        private string ConvertPathTag(string tag, ModeType mode)
        {
            switch (tag)
            {
                case "[model_hash]":
                    return GetModelHash(Options.SDModelCheckpoint);
                case "[model_name]":
                    if (IsWebuiUp)
                        return GetModelName(Options.SDModelCheckpoint);
                    else
                    {
                        // Transforms this "Base/v1-5-pruned-emaonly.safetensors" into "Base\\v1-5-pruned-emaonly"
                        var modelAsPath = GetCurrentModel(mode)?.Replace('/', Path.DirectorySeparatorChar) ?? "unknown";
                        return Path.Combine(Path.GetDirectoryName(modelAsPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelAsPath));
                    }
                default:
                    break;
            }

            if (mode == ModeType.Txt2Img)
            {
                switch (tag)
                {
                    case "[sampler]":
                        return ParametersTxt2Img.SamplerName;
                    case "[seed]":
                        return State.Generation.Seed.ToString();
                    case "[steps]":
                        return ParametersTxt2Img.Steps.ToString();
                    case "[cfg]":
                        return ParametersTxt2Img.CfgScale.ToString();
                    default: break;
                }
            }
            else if (mode == ModeType.Img2Img)
            {
                switch (tag)
                {
                    case "[sampler]":
                        return ParametersImg2Img.SamplerIndex;
                    case "[seed]":
                        return State.Generation.Seed.ToString();
                    case "[steps]":
                        return ParametersImg2Img.Steps.ToString();
                    case "[cfg]":
                        return ParametersImg2Img.CfgScale.ToString();
                    default: break;
                }
            }
            else if (mode == ModeType.Img2Vid)
            {
                switch (tag)
                {
                    case "[sampler]":
                        return ParametersImg2Vid?.SamplerName ?? "euler";
                    case "[seed]":
                        return State.Generation.Seed.ToString();
                    case "[steps]":
                        return ParametersImg2Vid?.Steps?.ToString() ?? "8";
                    case "[cfg]":
                        return ParametersImg2Vid?.CfgScale?.ToString() ?? "1";
                    default: break;
                }
            }

            return string.Empty;
        }

        private string GetModelHash(string modelName)
        {
            // Search in all model lists
            var model = CheckpointModels?.FirstOrDefault(m => m.Title.Contains(modelName))
                     ?? DiffusionModels?.FirstOrDefault(m => m.Title.Contains(modelName));
            return model?.Hash;
        }

        private string GetModelName(string modelName)
        {
            // Search in all model lists
            var model = CheckpointModels?.FirstOrDefault(m => m.Title.Contains(modelName))
                     ?? DiffusionModels?.FirstOrDefault(m => m.Title.Contains(modelName));
            return model?.Model_name;
        }

        public async Task GetResourceTypeDirectories()
        {
            // ComfyUI only - use configured resources path
            var baseDir = _configuration["ResourcesPath"];
            ResourceTypeDirectories = new()
            {
                {"Checkpoint", Path.Combine(baseDir, "Checkpoint")},
                {"TextualInversion", Path.Combine(baseDir, "TextualInversion")},
                {"Hypernetwork", Path.Combine(baseDir, "Hypernetwork")},
                {"LORA", Path.Combine(baseDir, "LORA")},
                {"LoCon", Path.Combine(baseDir, "LORA")},
                {"VAE", Path.Combine(baseDir, "VAE")}
            };
        }

        public async Task<string> PostOptions(Options options)
        {
            var response = await _backend.PostOptions(options);
            Options = _backend.Options;
            OnOptionsChange?.Invoke();
            return response;
        }

        public void SerializeInfo()
        {
            // ComfyUI only
            if (IsComfyUIUp) ImagesInfo = new() { InfoTexts = new[] { Images.Info } };
        }

        public void ReplaceSelectedImages(List<int> ids)
        {
            _gallery.ReplaceSelectedImages(ids);
            OnSelectedImagesChanged?.Invoke();
        }

        public void AddSelectedImage(int id)
        {
            _gallery.AddSelectedImage(id);
            OnSelectedImagesChanged?.Invoke();
        }

        public void RemoveSelectedImage(int id)
        {
            _gallery.RemoveSelectedImage(id);
            OnSelectedImagesChanged?.Invoke();
        }

        public void ClearSelectedImages()
        {
            _gallery.ClearSelectedImages();
            OnSelectedImagesChanged?.Invoke();
        }

        public void SetGenerationParameter(Image source, string parameter, bool isImg2Img)
        {
            string GetSampler() => _db.GetSampler(source.SamplerId).Result;

            SharedParameters param = isImg2Img ? ParametersImg2Img : ParametersTxt2Img;
            switch (parameter)
            {
                case nameof(SharedParameters.Prompt):
                    param.Prompt = ParseAndCleanCopiedPrompt(source.Prompt, false, isImg2Img);
                    break;
                case nameof(SharedParameters.NegativePrompt):
                    param.NegativePrompt = ParseAndCleanCopiedPrompt(source.NegativePrompt, true, isImg2Img);
                    break;
                case nameof(SharedParameters.SamplerIndex):
                    param.SamplerIndex = GetSampler();
                    break;
                case nameof(SharedParameters.Scheduler):
                    param.Scheduler = source.Scheduler;
                    break;
                case nameof(SharedParameters.Seed):
                    param.Seed = source.Seed;
                    break;
                case nameof(SharedParameters.Steps):
                    param.Steps = source.Steps;
                    break;
                case nameof(SharedParameters.CfgScale):
                    param.CfgScale = source.CfgScale;
                    break;
                case nameof(SharedParameters.Width):
                    param.Width = source.Width;
                    break;
                case nameof(SharedParameters.Height):
                    param.Height = source.Height;
                    break;
                case nameof(SharedParameters.DenoisingStrength):
                    param.DenoisingStrength = source.DenoisingStrength;
                    break;
            }

            if (isImg2Img) OnImg2ImgParametersChanged?.Invoke();
            else OnTxt2ImgParametersChanged?.Invoke();
        }

        public void LoadSettings()
        {
            _settings.LoadSettings();
        }

        public void SaveSettings()
        {
            _settings.SaveSettings();
        }

        public async Task LoadState(State? state = null)
        {
            if (state != null)
            {
                // Load specific state preset by ID
                await _state.LoadState(state.Id);
            }
            else
            {
                // Load autosave state (ID = 1)
                await _state.LoadState();
            }

            // Keep existing migration and workflow refresh logic
            MigrateLegacyModelSettings();
            RefreshWorkflowsFromDisk();

            // Trigger state changed events
            OnAppStateChanged?.Invoke();
            OnTxt2ImgParametersChanged?.Invoke();
            OnImg2ImgParametersChanged?.Invoke();
            OnUpscaleParametersChanged?.Invoke();
            OnImg2VidParametersChanged?.Invoke();
        }

        public async Task SaveState(State? state = null)
        {
            if (state == null)
            {
                await _state.SaveState();
            }
            else
            {
                await _db.UpdateState(state);
            }
        }

        /// <summary>
        /// Gets the appropriate model list based on asset type.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public List<SDModel> GetModelsForAssetType(AssetType assetType)
        {
            return _models.GetModelsForAssetType(assetType);
        }

        /// <summary>
        /// Gets available options for any asset type.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task<List<string>> GetAssetOptions(AssetType assetType)
        {
            return await _models.GetAssetOptions(assetType);
        }
        #region WorkflowAssets Management

        /// <summary>
        /// Gets a workflow asset value for the specified mode.
        /// </summary>
        /// <param name="parameter">The asset parameter name (e.g., "Model", "HighModel", "Vae")</param>
        /// <param name="mode">The generation mode (defaults to Txt2Img)</param>
        /// <returns>The asset value or null if not set</returns>
        public string? GetWorkflowAsset(string parameter, ModeType? mode = null)
        {
            var assets = GetWorkflowAssetsForMode(mode);
            return assets?.GetValueOrDefault(parameter);
        }

        /// <summary>
        /// Sets a workflow asset value for the specified mode.
        /// </summary>
        /// <param name="parameter">The asset parameter name (e.g., "Model", "HighModel", "Vae")</param>
        /// <param name="value">The asset value to set</param>
        /// <param name="mode">The generation mode (defaults to Txt2Img)</param>
        public void SetWorkflowAsset(string parameter, string value, ModeType? mode = null)
        {
            var assets = GetOrCreateWorkflowAssetsForMode(mode);
            assets[parameter] = value;
        }

        /// <summary>
        /// Gets the WorkflowAssets dictionary for the specified mode.
        /// Returns null if the dictionary doesn't exist.
        /// </summary>
        public Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
        {
            return mode switch
            {
                ModeType.Img2Img => ParametersImg2Img?.WorkflowAssets,
                ModeType.Img2Vid => ParametersImg2Vid?.WorkflowAssets,
                ModeType.Extras => ParametersUpscale?.WorkflowAssets,
                _ => ParametersTxt2Img?.WorkflowAssets
            };
        }

        /// <summary>
        /// Gets or creates the WorkflowAssets dictionary for the specified mode.
        /// </summary>
        private Dictionary<string, string> GetOrCreateWorkflowAssetsForMode(ModeType? mode)
        {
            switch (mode)
            {
                case ModeType.Img2Img:
                    ParametersImg2Img.WorkflowAssets ??= new Dictionary<string, string>();
                    return ParametersImg2Img.WorkflowAssets;
                case ModeType.Img2Vid:
                    ParametersImg2Vid.WorkflowAssets ??= new Dictionary<string, string>();
                    return ParametersImg2Vid.WorkflowAssets;
                case ModeType.Extras:
                    ParametersUpscale.WorkflowAssets ??= new Dictionary<string, string>();
                    return ParametersUpscale.WorkflowAssets;
                default:
                    ParametersTxt2Img.WorkflowAssets ??= new Dictionary<string, string>();
                    return ParametersTxt2Img.WorkflowAssets;
            }
        }

        /// <summary>
        /// Gets the assets defined for the current workflow.
        /// Returns DiffusionModels if workflow uses diffusion, otherwise CheckpointModels.
        /// </summary>
        public List<WorkflowAsset>? GetCurrentWorkflowAssets()
        {
            var workflow = GetCurrentWorkflow();
            return workflow?.Assets?.OrderBy(a => a.Order).ToList();
        }

        #endregion

        /// <summary>
        /// Gets the current model name based on mode.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public string GetCurrentModel(ModeType? mode = null)
        {
            return _models.GetCurrentModel(mode);
        }

        /// <summary>
        /// Sets the current model name based on mode.
        /// Temporary facade - delegates to ModelService (will be removed in Phase 8)
        /// </summary>
        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            await _models.SetCurrentModel(modelTitle, mode);
            OnSDModelsChange?.Invoke();
            await SaveState();
        }

        /// <summary>
        /// Gets the current VAE name based on mode using WorkflowAssets.
        /// </summary>
        public string? GetCurrentVae(ModeType? mode = null)
        {
            return GetWorkflowAsset("Vae", mode);
        }

        /// <summary>
        /// Sets the current VAE name based on mode using WorkflowAssets.
        /// </summary>
        public async Task SetCurrentVae(string vae, ModeType? mode = null)
        {
            // No WebUI support - ComfyUI only
            SetWorkflowAsset("Vae", vae, mode);
            await SaveState();
        }

        /// <summary>
        /// Migrates legacy SDModel and Vae from AppState to WorkflowAssets.
        /// Call this after loading state to ensure backward compatibility.
        /// </summary>
        private void MigrateLegacyModelSettings()
        {
#pragma warning disable CS0618 // Suppress obsolete warnings for migration

            // Migrate legacy SDModel from AppState
            if (!string.IsNullOrWhiteSpace(State?.Generation?.SDModel) && State.Generation.SDModel != "Loading...")
            {
                var currentTxt2ImgModel = GetWorkflowAsset("Model", ModeType.Txt2Img);
                if (string.IsNullOrWhiteSpace(currentTxt2ImgModel))
                    SetWorkflowAsset("Model", State.Generation.SDModel, ModeType.Txt2Img);

                var currentImg2ImgModel = GetWorkflowAsset("Model", ModeType.Img2Img);
                if (string.IsNullOrWhiteSpace(currentImg2ImgModel))
                    SetWorkflowAsset("Model", State.Generation.SDModel, ModeType.Img2Img);

                // Clear legacy property after migration
                State.Generation.SDModel = null;
            }

            // Migrate legacy Vae from AppState
            if (!string.IsNullOrWhiteSpace(State?.Generation?.Vae))
            {
                var currentTxt2ImgVae = GetWorkflowAsset("Vae", ModeType.Txt2Img);
                if (string.IsNullOrWhiteSpace(currentTxt2ImgVae))
                    SetWorkflowAsset("Vae", State.Generation.Vae, ModeType.Txt2Img);

                var currentImg2ImgVae = GetWorkflowAsset("Vae", ModeType.Img2Img);
                if (string.IsNullOrWhiteSpace(currentImg2ImgVae))
                    SetWorkflowAsset("Vae", State.Generation.Vae, ModeType.Img2Img);

                // Clear legacy property after migration
                State.Generation.Vae = null;
            }

#pragma warning restore CS0618
        }
    }
}
