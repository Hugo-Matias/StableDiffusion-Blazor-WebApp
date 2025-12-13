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

    /// <summary>
    /// Orchestrates generation workflows, coordinates between specialized services,
    /// and manages shared generation state.
    /// </summary>
    public class ManagerService
    {
        private readonly IDatabaseService _db;
        private readonly IIOService _io;
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

        #region Events
        
        public event Action OnSDModelsChange;
        public event Action OnOptionsChange;
        public event Action OnConverging;
        public event Action OnFolderChange;
        public event Action OnProjectsChange;
        public event Action OnProjectChange;
        public event Func<Task> OnProjectChangeTask;
        public event Action OnStateHasChanged;
        public event Action OnProgressChanged;
        public event Action OnComfyUIStateChanged;
        public event Action OnTxt2ImgParametersChanged;
        public event Action OnImg2ImgParametersChanged;
        public event Action OnUpscaleParametersChanged;
        public event Action OnImg2VidParametersChanged;
        public event Action OnSelectedImagesChanged;
        public event Action OnCanvasImageDataChanged;
        public event Action OnWorkflowBaseChanged;
        public event Action? OnSamplersSchedulersChanged;
        public event Action OnCurrentWorkflowChanged;
        public event Action OnSessionVideosChanged;
        
        #endregion

        #region State Facades
        
        public AppState State => _state.State;
        public Txt2ImgParameters ParametersTxt2Img => _state.ParametersTxt2Img;
        public Img2ImgParameters ParametersImg2Img => _state.ParametersImg2Img;
        public Models.UpscaleParameters ParametersUpscale => _state.ParametersUpscale;
        public Img2VidParameters ParametersImg2Vid => _state.ParametersImg2Vid;
        public AppSettings Settings => _settings.Settings;
        
        #endregion

        #region Orchestration Properties
        
        public Options Options { get; set; }
        public GeneratedImages Images { get; set; }
        public GeneratedImagesInfo ImagesInfo { get; set; }
        public ImagesDto GeneratedImageEntities { get; set; }
        public string? GridImage { get; set; }
        public InferenceProgress Progress { get; set; }
        public List<PromptStyle> Styles { get; set; }
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
        
        public int CurrentProgress
        {
            get => _currentProgress;
            set
            {
                _currentProgress = value;
                OnProgressChanged?.Invoke();
            }
        }
        
        public bool IsConverging
        {
            get => _isConverging;
            set
            {
                _isConverging = value;
                OnConverging?.Invoke();
                _events.Publish(new ConvergingChangedEventArgs(_isConverging));
            }
        }
        
        #endregion

        #region Model Service Facades
        
        public List<SDModel> CheckpointModels => _models.CheckpointModels;
        public List<SDModel> DiffusionModels => _models.DiffusionModels;
        public List<string> SDVAEs => _models.VAEModels;
        public List<string> ClipModels => _models.ClipModels;
        public List<string> ClipVisionModels => _models.ClipVisionModels;
        public List<string> SDADetailerModels => _models.ADetailerModels;
        
        #endregion

        #region Backend Service Facades
        
        public List<Models.Sampler> Samplers => _backend.Samplers;
        public List<Scheduler> Schedulers => _backend.Schedulers;
        public List<Upscaler> Upscalers => _backend.Upscalers;
        public bool IsComfyUIUp => _backend.IsBackendAvailable;
        
        #endregion

        #region Gallery Service Facades
        
        public List<Folder>? Folders => _gallery.Folders;
        public List<Project>? Projects => _gallery.Projects;
        public List<int> SelectedImageIds => _gallery.SelectedImageIds;
        
        #endregion

        #region Session Service Facades
        
        public List<string> CanvasStates => _session.CanvasStates;
        public GeneratedVideos SessionGeneratedVideos => _session.SessionGeneratedVideos;
        
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
        
        public string Img2VidInputImage
        {
            get => _session.Img2VidInputImage;
            set => _session.Img2VidInputImage = value;
        }
        
        public string Img2ImgInputImage
        {
            get => _session.Img2ImgInputImage;
            set => _session.Img2ImgInputImage = value;
        }
        
        public ImageEditorState ImageEditorState
        {
            get => _session.ImageEditorState;
            set => _session.ImageEditorState = value;
        }
        
        #endregion

        public ManagerService(
            IDatabaseService db,
            IIOService io,
            ProgressService progress,
            IConfiguration configuration,
            ComfyUIService capi,
            WorkflowService workflow,
            IStateService state,
            IEventService events,
            ISettingsService settings,
            IBackendService backend,
            IModelService models,
            IGalleryService gallery,
            ISessionService session)
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

            Images = new();
            Progress = new();
            _db.PageSize = State.Gallery.PageSize;
            State.Gallery.DateRange = new(DateTime.Now.Date.AddDays(-5), DateTime.Now.Date);

            GetButtonTags();
        }

        #region Event Helpers
        
        public void InvokeProgressChanged() => OnProgressChanged?.Invoke();
        
        public void InvokeParametersChanged(bool isImg2Img)
        {
            if (isImg2Img) OnImg2ImgParametersChanged?.Invoke();
            else OnTxt2ImgParametersChanged?.Invoke();
        }
        
        public void InvokeSessionVideosChanged() => OnSessionVideosChanged?.Invoke();
        
        #endregion

        #region Parameter Initialization
        
        public void InitializeParameters(ModeType[] modes)
        {
            _state.InitializeParameters(modes);
        }
        
        #endregion

        #region Model Management
        
        public async Task GetWorkflowModels(bool refresh = false)
        {
            await _models.GetWorkflowModels(refresh);
            OnSDModelsChange?.Invoke();
        }
        
        public List<SDModel> GetCurrentWorkflowModels()
        {
            var workflow = GetCurrentWorkflow();
            if (workflow?.Assets != null && workflow.Assets.Any(a => a.Type == AssetType.DiffusionModel))
            {
                return DiffusionModels ?? new List<SDModel>();
            }
            return CheckpointModels ?? new List<SDModel>();
        }
        
        public async Task GetSDVAEs()
        {
            await _models.GetVAEModels();
        }
        
        public async Task GetSDADetailerModels()
        {
            await _models.GetADetailerModels();
        }
        
        public List<SDModel> GetModelsForAssetType(AssetType assetType)
        {
            return _models.GetModelsForAssetType(assetType);
        }
        
        public async Task<List<string>> GetAssetOptions(AssetType assetType)
        {
            return await _models.GetAssetOptions(assetType);
        }
        
        public string GetCurrentModel(ModeType? mode = null)
        {
            return _models.GetCurrentModel(mode);
        }
        
        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            await _models.SetCurrentModel(modelTitle, mode);
            OnSDModelsChange?.Invoke();
            await SaveState();
        }
        
        public async Task SetSDModel(string modelTitle)
        {
            await SetCurrentModel(modelTitle);
        }
        
        public string? GetCurrentVae(ModeType? mode = null)
        {
            return GetWorkflowAsset("Vae", mode);
        }
        
        public async Task SetCurrentVae(string vae, ModeType? mode = null)
        {
            SetWorkflowAsset("Vae", vae, mode);
            await SaveState();
        }
        
        #endregion

        #region Workflow Management
        
        public Workflow? GetCurrentWorkflow()
        {
            if (State?.Generation?.Workflows == null || State.Generation.Workflows.Count == 0)
                return null;

            if (State.Generation.CurrentWorkflowId.HasValue)
            {
                var workflow = State.Generation.Workflows.FirstOrDefault(w => w.Id == State.Generation.CurrentWorkflowId.Value);
                if (workflow != null)
                    return workflow;
            }

            if (State.Generation.WorkflowBase != default)
            {
                return State.Generation.Workflows.FirstOrDefault(w => w.Base == State.Generation.WorkflowBase);
            }

            return State.Generation.Workflows.FirstOrDefault();
        }
        
        public Workflow GetWorkflowById(Guid id) => State.Generation.Workflows.FirstOrDefault(w => w.Id == id);
        
        public List<Workflow> GetWorkflowsForMode(ModeType mode)
        {
            if (State?.Generation?.Workflows == null)
                return new List<Workflow>();

            return State.Generation.Workflows
                .Where(w => w.Mode == mode)
                .OrderBy(w => w.Title)
                .ToList();
        }
        
        public void GetComfyWorkflows() => State.Generation.Workflows = _workflow.GetWorkflows();
        
        public void SetCurrentWorkflow(Guid workflowId, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return;

            State.Generation.CurrentWorkflowId = workflowId;
            State.Generation.WorkflowBase = workflow.Base;

            OnWorkflowBaseChanged?.Invoke();
            OnCurrentWorkflowChanged?.Invoke();
            _events.Publish(new StateChangedEventArgs());
            _events.Publish(new WorkflowChangedEventArgs(workflowId, "Set"));
        }
        
        public async Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return false;

            State.Generation.CurrentWorkflowId = workflowId;
            State.Generation.WorkflowBase = workflow.Base;

            bool assetsInitialized = true;
            if (workflow.Assets != null && workflow.Assets.Count > 0)
            {
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assetsInitialized = await assetResolver.InitializeWorkflowAssets(workflow, assets);
            }

            await GetWorkflowModels();

            OnWorkflowBaseChanged?.Invoke();
            OnCurrentWorkflowChanged?.Invoke();
            _events.Publish(new StateChangedEventArgs());
            _events.Publish(new WorkflowChangedEventArgs(workflowId, "SetAsync"));

            await SaveState();
            return assetsInitialized;
        }
        
        public void SetWorkflowBase(ModelBase workflowBase)
        {
            _state.SetWorkflowBase(workflowBase);
            OnWorkflowBaseChanged?.Invoke();
            SetDefaultBaseModel();
        }
        
        public void ResetCurrentWorkflow()
        {
            State.Generation.CurrentWorkflowId = null;
            State.Generation.WorkflowBase = default;

            var modes = new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid, ModeType.Extras };
            foreach (var mode in modes)
            {
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assets.Clear();
            }
            
            OnCurrentWorkflowChanged?.Invoke();
        }
        
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
                    if (ParametersTxt2Img != null)
                    {
                        ParametersTxt2Img.WorkflowAssets ??= new Dictionary<string, string>();
                        ParametersTxt2Img.WorkflowAssets["Model"] = defaultModel;
                    }
                    OnSDModelsChange?.Invoke();
                }
            }
        }
        
        #endregion

        #region Workflow Assets Management
        
        public string? GetWorkflowAsset(string parameter, ModeType? mode = null)
        {
            var assets = GetWorkflowAssetsForMode(mode);
            return assets?.GetValueOrDefault(parameter);
        }
        
        public void SetWorkflowAsset(string parameter, string value, ModeType? mode = null)
        {
            var assets = GetOrCreateWorkflowAssetsForMode(mode);
            assets[parameter] = value;
        }
        
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
        
        public List<WorkflowAsset>? GetCurrentWorkflowAssets()
        {
            var workflow = GetCurrentWorkflow();
            return workflow?.Assets?.OrderBy(a => a.Order).ToList();
        }
        
        #endregion

        #region Backend & Options Management
        
        public async Task GetOptions()
        {
            await _backend.GetOptions();
            Options = _backend.Options;
            OnOptionsChange?.Invoke();
            _events.Publish(new OptionsChangedEventArgs());
        }
        
        public async Task<string> PostOptions(Options options)
        {
            var response = await _backend.PostOptions(options);
            Options = _backend.Options;
            OnOptionsChange?.Invoke();
            _events.Publish(new OptionsChangedEventArgs());
            return response;
        }
        
        public async Task LoadBackendDependentResources()
        {
            await _backend.LoadBackendDependentResources();
            await _models.GetADetailerModels();
            OnSamplersSchedulersChanged?.Invoke();
            _events.Publish(new SamplersSchedulersChangedEventArgs());
        }
        
        public async Task GetResourceTypeDirectories()
        {
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
        
        #endregion

        #region Gallery Management
        
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
            await _gallery.SetCurrentProject(id);
            OnProjectChange?.Invoke();
            OnProjectChangeTask?.Invoke();
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
        
        #endregion

        #region Session Management
        
        public void ResetImageEditorState()
        {
            _session.ResetImageEditorState();
        }
        
        public void SetImg2ImgInputImage(string imageData, bool resetEditorState)
        {
            _session.SetImg2ImgInputImage(imageData, resetEditorState);
        }
        
        public void AddSessionVideo(GeneratedVideo video)
        {
            _session.AddSessionVideo(video);
        }
        
        public void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
        {
            _session.AddSessionVideos(videos);
        }
        
        public void ClearSessionVideos()
        {
            _session.ClearSessionVideos();
        }
        
        public void RemoveSessionVideo(GeneratedVideo video)
        {
            _session.RemoveSessionVideo(video);
        }
        
        #endregion

        #region Styles & Prompts
        
        public async Task GetStyles()
        {
            Styles = new();
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
            _events.Publish(new StylesChangedEventArgs { ChangeType = "Loaded" });
        }
        
        public void GetButtonTags() => ButtonTags = JsonSerializer.Deserialize<PromptButton>(
            _io.GetJsonAsString("Data/example.json"),
            new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        
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
        }
        
        public string ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img)
        {
            var loras = Parser.ExtractLorasFromPrompt(prompt, out var cleanedFromLoras, isNegative);
            SetLoras(loras, isImg2Img);

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
                            cleanedFromStyles = Regex.Replace(cleanedFromStyles,
                                $@"{Regex.Escape(actualStyleText)},?\s*",
                                "", RegexOptions.IgnoreCase);
                        }
                    }
                }

                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s*,\s*,\s*", ", ");
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"^\s*,\s*|\s*,\s*$", "");
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s+", " ").Trim();
            }

            return cleanedFromStyles;
        }
        
        #endregion

        #region Parameter Management
        
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
        
        #endregion

        #region Image Info & Path Management
        
        public void SerializeInfo()
        {
            ImagesInfo = new() { InfoTexts = new[] { Images.Info } };
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

            return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir, Parser.ModeTypeFromOutdir((Outdir)outdir)))
                .Replace('/', Path.DirectorySeparatorChar);
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
                    var modelAsPath = GetCurrentModel(mode)?.Replace('/', Path.DirectorySeparatorChar) ?? "unknown";
                    return Path.Combine(Path.GetDirectoryName(modelAsPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelAsPath));
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
            var model = CheckpointModels?.FirstOrDefault(m => m.Title.Contains(modelName))
                     ?? DiffusionModels?.FirstOrDefault(m => m.Title.Contains(modelName));
            return model?.Hash;
        }
        
        #endregion

        #region Settings & State Management
        
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
                await _state.LoadState(state.Id);
            }
            else
            {
                await _state.LoadState();
            }

            _state.MigrateLegacySettings();
            
            var (workflows, suggestedBase, suggestedId) = _workflow.RefreshWorkflows(
                State?.Generation?.WorkflowBase,
                State?.Generation?.CurrentWorkflowId
            );
            
            if (State?.Generation != null)
            {
                State.Generation.Workflows = workflows;
                if (suggestedBase.HasValue)
                    State.Generation.WorkflowBase = suggestedBase.Value;
                if (suggestedId.HasValue)
                    State.Generation.CurrentWorkflowId = suggestedId.Value;
            }

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
        
        #endregion
    }
}
