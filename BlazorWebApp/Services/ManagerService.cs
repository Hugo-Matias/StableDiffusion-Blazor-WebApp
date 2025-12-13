using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
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
        private readonly IProgressService _progress;
        private readonly IConfiguration _configuration;
        private readonly IComfyUIService _capi;
        private readonly IWorkflowService _workflow;
        private readonly IStateService _state;
        private readonly IEventService _events;
        private readonly ISettingsService _settings;
        private readonly IBackendService _backend;
        private readonly IModelService _models;
        private readonly IGalleryService _gallery;
        private readonly ISessionService _session;
        private int _currentProgress;
        private bool _isConverging;

        #region Orchestration Properties
        
        public Options Options { get; set; }
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
                _events.Publish(new ProgressChangedEventArgs(value));
            }
        }
        
        public bool IsConverging
        {
            get => _isConverging;
            set
            {
                _isConverging = value;
                _events.Publish(new ConvergingChangedEventArgs(_isConverging));
            }
        }
        
        #endregion

        public ManagerService(
            IDatabaseService db,
            IIOService io,
            IProgressService progress,
            IConfiguration configuration,
            IComfyUIService capi,
            IWorkflowService workflow,
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

            _db.PageSize = _state.State.Gallery.PageSize;
            _state.State.Gallery.DateRange = new(DateTime.Now.Date.AddDays(-5), DateTime.Now.Date);
        }

        #region Event Publishing
        
        public void InvokeProgressChanged() => _events.Publish(new ProgressChangedEventArgs(_currentProgress));
        public void InvokeParametersChanged(bool isImg2Img) => _events.Publish(new ParametersChangedEventArgs(isImg2Img ? "Img2Img" : "Txt2Img"));
        public void InvokeSessionVideosChanged() => _events.Publish(new SessionVideosChangedEventArgs());
        
        #endregion

        #region Parameter Initialization
        
        public void InitializeParameters(ModeType[] modes) => _state.InitializeParameters(modes);
        
        #endregion

        #region Model Management
        
        public async Task GetWorkflowModels(bool refresh = false) => await _models.GetWorkflowModels(refresh);
        
        public List<SDModel> GetCurrentWorkflowModels()
        {
            var workflow = GetCurrentWorkflow();
            if (workflow?.Assets != null && workflow.Assets.Any(a => a.Type == AssetType.DiffusionModel))
                return _models.DiffusionModels ?? new List<SDModel>();
            return _models.CheckpointModels ?? new List<SDModel>();
        }
        
        public async Task GetSDVAEs() => await _models.GetVAEModels();
        public async Task GetSDADetailerModels() => await _models.GetADetailerModels();
        public List<SDModel> GetModelsForAssetType(AssetType assetType) => _models.GetModelsForAssetType(assetType);
        public async Task<List<string>> GetAssetOptions(AssetType assetType) => await _models.GetAssetOptions(assetType);
        public string GetCurrentModel(ModeType? mode = null) => _models.GetCurrentModel(mode);
        
        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            await _models.SetCurrentModel(modelTitle, mode);
            await SaveState();
        }
        
        public async Task SetSDModel(string modelTitle) => await SetCurrentModel(modelTitle);
        public string? GetCurrentVae(ModeType? mode = null) => GetWorkflowAsset("Vae", mode);
        
        public async Task SetCurrentVae(string vae, ModeType? mode = null)
        {
            SetWorkflowAsset("Vae", vae, mode);
            await SaveState();
        }
        
        #endregion

        #region Workflow Management
        
        public Workflow? GetCurrentWorkflow()
        {
            if (_state.State?.Generation?.Workflows == null || _state.State.Generation.Workflows.Count == 0)
                return null;

            if (_state.State.Generation.CurrentWorkflowId.HasValue)
            {
                var workflow = _state.State.Generation.Workflows.FirstOrDefault(w => w.Id == _state.State.Generation.CurrentWorkflowId.Value);
                if (workflow != null) return workflow;
            }

            if (_state.State.Generation.WorkflowBase != default)
                return _state.State.Generation.Workflows.FirstOrDefault(w => w.Base == _state.State.Generation.WorkflowBase);

            return _state.State.Generation.Workflows.FirstOrDefault();
        }
        
        public Workflow GetWorkflowById(Guid id) => _state.State.Generation.Workflows.FirstOrDefault(w => w.Id == id);
        
        public List<Workflow> GetWorkflowsForMode(ModeType mode)
        {
            if (_state.State?.Generation?.Workflows == null)
                return new List<Workflow>();

            return _state.State.Generation.Workflows.Where(w => w.Mode == mode).OrderBy(w => w.Title).ToList();
        }
        
        public void GetComfyWorkflows() => _state.State.Generation.Workflows = _workflow.GetWorkflows();
        
        public void SetCurrentWorkflow(Guid workflowId, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return;

            _state.State.Generation.CurrentWorkflowId = workflowId;
            _state.State.Generation.WorkflowBase = workflow.Base;

            _events.Publish(new StateChangedEventArgs());
            _events.Publish(new WorkflowChangedEventArgs(workflowId, "Set"));
        }
        
        public async Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver, ModeType? mode = null)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null) return false;

            _state.State.Generation.CurrentWorkflowId = workflowId;
            _state.State.Generation.WorkflowBase = workflow.Base;

            bool assetsInitialized = true;
            if (workflow.Assets != null && workflow.Assets.Count > 0)
            {
                var assets = GetOrCreateWorkflowAssetsForMode(mode);
                assetsInitialized = await assetResolver.InitializeWorkflowAssets(workflow, assets);
            }

            await GetWorkflowModels();
            _events.Publish(new StateChangedEventArgs());
            _events.Publish(new WorkflowChangedEventArgs(workflowId, "SetAsync"));
            await SaveState();
            return assetsInitialized;
        }
        
        public void SetWorkflowBase(ModelBase workflowBase)
        {
            _state.SetWorkflowBase(workflowBase);
            _events.Publish(new StateChangedEventArgs());
            SetDefaultBaseModel();
        }
        
        public void ResetCurrentWorkflow()
        {
            _state.State.Generation.CurrentWorkflowId = null;
            _state.State.Generation.WorkflowBase = default;

            foreach (var mode in new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid, ModeType.Extras })
                GetOrCreateWorkflowAssetsForMode(mode).Clear();
            
            _events.Publish(new WorkflowChangedEventArgs(Guid.Empty, "Reset"));
        }
        
        public void SetDefaultBaseModel()
        {
            var modelKeys = new[] { "ckpt_name", "unet_name" };
            var workflow = _state.State.Generation.Workflows?.FirstOrDefault(w => w.Base == _state.State.Generation.WorkflowBase);
            if (workflow?.Pipeline == null) return;
            
            var defaultModel = workflow.Pipeline
                .Select(s => modelKeys.FirstOrDefault(k => s.Parameters?.ContainsKey(k) == true))
                .Where(k => k != null)
                .Select(k => workflow.Pipeline.FirstOrDefault(s => s.Parameters?.ContainsKey(k) == true)?.Parameters[k]?.ToString())
                .FirstOrDefault()?
                .GetDefaultModelFromWorkflow();

            if (!string.IsNullOrWhiteSpace(defaultModel) && _state.ParametersTxt2Img != null)
            {
                _state.ParametersTxt2Img.WorkflowAssets ??= new Dictionary<string, string>();
                _state.ParametersTxt2Img.WorkflowAssets["Model"] = defaultModel;
                _events.Publish(new ModelsChangedEventArgs());
            }
        }
        
        #endregion

        #region Workflow Assets
        
        public string? GetWorkflowAsset(string parameter, ModeType? mode = null) => GetWorkflowAssetsForMode(mode)?.GetValueOrDefault(parameter);
        
        public void SetWorkflowAsset(string parameter, string value, ModeType? mode = null) => GetOrCreateWorkflowAssetsForMode(mode)[parameter] = value;
        
        public Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
        {
            return mode switch
            {
                ModeType.Img2Img => _state.ParametersImg2Img?.WorkflowAssets,
                ModeType.Img2Vid => _state.ParametersImg2Vid?.WorkflowAssets,
                ModeType.Extras => _state.ParametersUpscale?.WorkflowAssets,
                _ => _state.ParametersTxt2Img?.WorkflowAssets
            };
        }
        
        private Dictionary<string, string> GetOrCreateWorkflowAssetsForMode(ModeType? mode)
        {
            return mode switch
            {
                ModeType.Img2Img => _state.ParametersImg2Img.WorkflowAssets ??= new(),
                ModeType.Img2Vid => _state.ParametersImg2Vid.WorkflowAssets ??= new(),
                ModeType.Extras => _state.ParametersUpscale.WorkflowAssets ??= new(),
                _ => _state.ParametersTxt2Img.WorkflowAssets ??= new()
            };
        }
        
        public List<WorkflowAsset>? GetCurrentWorkflowAssets() => GetCurrentWorkflow()?.Assets?.OrderBy(a => a.Order).ToList();
        
        #endregion

        #region Backend & Options
        
        public async Task GetOptions()
        {
            await _backend.GetOptions();
            Options = _backend.Options;
            _events.Publish(new OptionsChangedEventArgs());
        }
        
        public async Task<string> PostOptions(Options options)
        {
            var response = await _backend.PostOptions(options);
            Options = _backend.Options;
            _events.Publish(new OptionsChangedEventArgs());
            return response;
        }
        
        public async Task LoadBackendDependentResources()
        {
            await _backend.LoadBackendDependentResources();
            await _models.GetADetailerModels();
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

        #region Gallery
        
        public async Task GetFolders() => await _gallery.GetFolders();
        
        public async Task GetProjects()
        {
            await _gallery.GetProjects(_state.State.Gallery.FolderId);
            if (_state.State.Gallery.GalleriesOrderDescending) _gallery.Projects.Reverse();
        }
        
        public async Task SetCurrentFolder(int id)
        {
            if (id > 0)
            {
                await GetFolders();
                _state.State.Gallery.FolderId = id;
                _state.State.Gallery.FolderName = _gallery.Folders.FirstOrDefault(f => f.Id == id)!.Name;
            }
            else
            {
                _state.State.Gallery.FolderId = 0;
                _state.State.Gallery.FolderName = "All";
            }
            SaveState();
            await GetProjects();
        }
        
        public async Task SetCurrentProject(int id)
        {
            await GetFolders();
            await GetProjects();
            await _gallery.SetCurrentProject(id);
        }
        
        public void ReplaceSelectedImages(List<int> ids) => _gallery.ReplaceSelectedImages(ids);
        public void AddSelectedImage(int id) => _gallery.AddSelectedImage(id);
        public void RemoveSelectedImage(int id) => _gallery.RemoveSelectedImage(id);
        public void ClearSelectedImages() => _gallery.ClearSelectedImages();
        
        #endregion

        #region Session
        
        public void ResetImageEditorState() => _session.ResetImageEditorState();
        public void SetImg2ImgInputImage(string imageData, bool resetEditorState) => _session.SetImg2ImgInputImage(imageData, resetEditorState);
        public void AddSessionVideo(GeneratedVideo video) => _session.AddSessionVideo(video);
        public void AddSessionVideos(IEnumerable<GeneratedVideo> videos) => _session.AddSessionVideos(videos);
        public void ClearSessionVideos() => _session.ClearSessionVideos();
        public void RemoveSessionVideo(GeneratedVideo video) => _session.RemoveSessionVideo(video);
        
        #endregion

        #region Styles & Prompts
        
        public void SetLoras(IEnumerable<Lora> loras, bool isImg2Img)
        {
            if (loras == null || _state.State?.Generation == null) return;

            var parametersLoras = isImg2Img ? _state.ParametersImg2Img.Loras : _state.ParametersTxt2Img.Loras;
            parametersLoras ??= [];

            foreach (var l in loras)
            {
                if (string.IsNullOrWhiteSpace(l.Name)) continue;
                if (!parametersLoras.Any(x => string.Equals(x.Name, l.Name, StringComparison.InvariantCultureIgnoreCase)))
                    parametersLoras.Add(new Lora(l));
            }
        }
        
        public string ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img)
        {
            var loras = Parser.ExtractLorasFromPrompt(prompt, out var cleanedFromLoras, isNegative);
            SetLoras(loras, isImg2Img);

            var cleanedFromStyles = cleanedFromLoras;
            if (_state.State?.Generation?.Styles != null)
            {
                foreach (var style in _state.State.Generation.Styles)
                {
                    var styleText = isNegative ? style.NegativePrompt : style.Prompt;
                    if (!string.IsNullOrWhiteSpace(styleText))
                    {
                        var actualStyleText = styleText.Replace("{prompt}", "");
                        if (!string.IsNullOrWhiteSpace(actualStyleText))
                            cleanedFromStyles = Regex.Replace(cleanedFromStyles, $@"{Regex.Escape(actualStyleText)},?\s*", "", RegexOptions.IgnoreCase);
                    }
                }

                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s*,\s*,\s*", ", ");
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"^\s*,\s*|\s*,\s*$", "");
                cleanedFromStyles = Regex.Replace(cleanedFromStyles, @"\s+", " ").Trim();
            }

            return cleanedFromStyles;
        }
        
        #endregion

        #region Parameter Loading
        
        public async Task LoadImageInfoParameters(Image image, ModeType mode)
        {
            bool isImg2Img = mode == ModeType.Img2Img;
            var param = isImg2Img ? (SharedParameters)_state.ParametersImg2Img : _state.ParametersTxt2Img;
            
            param.Prompt = ParseAndCleanCopiedPrompt(image.Prompt ?? string.Empty, false, isImg2Img);
            param.NegativePrompt = ParseAndCleanCopiedPrompt(image.NegativePrompt ?? string.Empty, true, isImg2Img);
            param.SamplerIndex = await _db.GetSampler(image.SamplerId);
            param.Steps = image.Steps;
            param.Seed = image.Seed;
            param.CfgScale = image.CfgScale;
            param.Width = image.Width;
            param.Height = image.Height;
            param.DenoisingStrength = image.DenoisingStrength;
        }
        
        public void SetGenerationParameter(Image source, string parameter, bool isImg2Img)
        {
            SharedParameters param = isImg2Img ? _state.ParametersImg2Img : _state.ParametersTxt2Img;
            
            switch (parameter)
            {
                case nameof(SharedParameters.Prompt):
                    param.Prompt = ParseAndCleanCopiedPrompt(source.Prompt, false, isImg2Img);
                    break;
                case nameof(SharedParameters.NegativePrompt):
                    param.NegativePrompt = ParseAndCleanCopiedPrompt(source.NegativePrompt, true, isImg2Img);
                    break;
                case nameof(SharedParameters.SamplerIndex):
                    param.SamplerIndex = _db.GetSampler(source.SamplerId).Result;
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

            _events.Publish(new ParametersChangedEventArgs(isImg2Img ? "Img2Img" : "Txt2Img"));
        }
        
        #endregion

        #region Path Management
        
        public string GetCurrentSaveFolder(Outdir? outdir)
        {
            string path = outdir switch
            {
                Outdir.Txt2ImgSamples => Options.OutdirSamplesTxt2Img,
                Outdir.Txt2ImgGrid => Options.OutdirGridTxt2Img,
                Outdir.Img2ImgSamples => Options.OutdirSamplesImg2Img,
                Outdir.Img2ImgGrid => Options.OutdirGridImg2Img,
                Outdir.Extras => Options.OutdirSamplesExtras,
                _ => string.Empty
            };

            if (outdir == Outdir.Extras || string.IsNullOrEmpty(path)) return path;

            return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir, Parser.ModeTypeFromOutdir((Outdir)outdir)))
                .Replace('/', Path.DirectorySeparatorChar);
        }
        
        public string ConvertPathPattern(string pattern, ModeType mode)
        {
            var rg = new Regex(@"(\[.+?\])");
            return rg.Replace(pattern, t => ConvertPathTag(t.Value, mode));
        }
        
        private string ConvertPathTag(string tag, ModeType mode)
        {
            if (tag == "[model_hash]") return GetModelHash(Options.SDModelCheckpoint);
            if (tag == "[model_name]")
            {
                var modelAsPath = GetCurrentModel(mode)?.Replace('/', Path.DirectorySeparatorChar) ?? "unknown";
                return Path.Combine(Path.GetDirectoryName(modelAsPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelAsPath));
            }

            return tag switch
            {
                "[sampler]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.SamplerName ?? "euler",
                    ModeType.Img2Img => _state.ParametersImg2Img?.SamplerIndex ?? "euler",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.SamplerName ?? "euler",
                    _ => "euler"
                },
                "[seed]" => _state.State.Generation.Seed.ToString(),
                "[steps]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.Steps?.ToString() ?? "20",
                    ModeType.Img2Img => _state.ParametersImg2Img?.Steps?.ToString() ?? "20",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.Steps?.ToString() ?? "8",
                    _ => "20"
                },
                "[cfg]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.CfgScale?.ToString() ?? "7",
                    ModeType.Img2Img => _state.ParametersImg2Img?.CfgScale?.ToString() ?? "7",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.CfgScale?.ToString() ?? "1",
                    _ => "7"
                },
                _ => string.Empty
            };
        }
        
        private string GetModelHash(string modelName)
        {
            var model = _models.CheckpointModels?.FirstOrDefault(m => m.Title.Contains(modelName))
                     ?? _models.DiffusionModels?.FirstOrDefault(m => m.Title.Contains(modelName));
            return model?.Hash;
        }
        
        #endregion

        #region State & Settings
        
        public void LoadSettings() => _settings.LoadSettings();
        public void SaveSettings() => _settings.SaveSettings();
        
        public async Task LoadState(State? state = null)
        {
            if (state != null)
                await _state.LoadState(state.Id);
            else
                await _state.LoadState();

            _state.MigrateLegacySettings();
            
            var (workflows, suggestedBase, suggestedId) = _workflow.RefreshWorkflows(
                _state.State?.Generation?.WorkflowBase,
                _state.State?.Generation?.CurrentWorkflowId
            );
            
            if (_state.State?.Generation != null)
            {
                _state.State.Generation.Workflows = workflows;
                if (suggestedBase.HasValue) _state.State.Generation.WorkflowBase = suggestedBase.Value;
                if (suggestedId.HasValue) _state.State.Generation.CurrentWorkflowId = suggestedId.Value;
            }

            _events.Publish(new ParametersChangedEventArgs("Txt2Img"));
            _events.Publish(new ParametersChangedEventArgs("Img2Img"));
            _events.Publish(new StateChangedEventArgs());
        }
        
        public async Task SaveState(State? state = null)
        {
            if (state == null)
                await _state.SaveState();
            else
                await _db.UpdateState(state);
        }
        
        #endregion
    }
}
