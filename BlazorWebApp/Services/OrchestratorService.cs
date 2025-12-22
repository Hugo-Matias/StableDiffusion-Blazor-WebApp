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
    /// and manages complex multi-service operations.
    /// </summary>
    public class OrchestratorService : IOrchestratorService
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

        public OrchestratorService(
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

        public void InvokeParametersChanged(bool isImg2Img) => _events.Publish(new ParametersChangedEventArgs(isImg2Img ? "Img2Img" : "Txt2Img"));
        public void InvokeSessionVideosChanged() => _events.Publish(new SessionVideosChangedEventArgs());

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

        /// <summary>
        /// Force refresh workflows from disk, reloading all template files.
        /// Use this after editing workflow template files during development.
        /// </summary>
        public void RefreshWorkflowsFromDisk()
        {
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

            _events.Publish(new WorkflowChangedEventArgs(
                _state.State?.Generation?.CurrentWorkflowId ?? Guid.Empty,
                "RefreshFromDisk"));
        }

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
            
            // Update GenerationParameters.WorkflowId to match
            _state.GenerationParameters.WorkflowId = workflowId;

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
            
            // After setting base, ensure we have a valid workflow ID selected
            // SetWorkflowBase in StateService now updates CurrentWorkflowId, so we just need to sync
            var currentId = _state.State.Generation.CurrentWorkflowId;
            if (currentId.HasValue)
            {
                _events.Publish(new WorkflowChangedEventArgs(currentId.Value, "SetBase"));
            }
            
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

            if (!string.IsNullOrWhiteSpace(defaultModel))
            {
                // Update GenerationParameters.Assets (unified model)
                _state.GenerationParameters.Assets["Model"] = defaultModel;
                _events.Publish(new ModelsChangedEventArgs());
            }
        }

        #endregion

        #region Workflow Assets

        public string? GetWorkflowAsset(string parameter, ModeType? mode = null)
        {
            // Use GenerationParameters.Assets (unified model)
            if (_state.GenerationParameters?.Assets?.TryGetValue(parameter, out var value) == true)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        public void SetWorkflowAsset(string parameter, string value, ModeType? mode = null)
        {
            // Update GenerationParameters.Assets (unified model)
            _state.GenerationParameters.Assets[parameter] = value;
        }

        public Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
        {
            // Return GenerationParameters.Assets (unified model)
            return _state.GenerationParameters?.Assets;
        }

        private Dictionary<string, string> GetOrCreateWorkflowAssetsForMode(ModeType? mode)
        {
            // Return GenerationParameters.Assets (unified model)
            return _state.GenerationParameters.Assets;
        }

        public List<WorkflowAsset>? GetCurrentWorkflowAssets() => GetCurrentWorkflow()?.Assets?.OrderBy(a => a.Order).ToList();

        #endregion

        #region Backend

        public async Task LoadBackendDependentResources()
        {
            await _backend.LoadBackendDependentResources();
            await _models.GetADetailerModels();
            _events.Publish(new SamplersSchedulersChangedEventArgs());
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

            // Update GenerationParameters.Loras (unified model)
            foreach (var l in loras)
            {
                if (string.IsNullOrWhiteSpace(l.Name)) continue;
                if (!_state.GenerationParameters.Loras.Any(x => string.Equals(x.Name, l.Name, StringComparison.InvariantCultureIgnoreCase)))
                    _state.GenerationParameters.Loras.Add(new Lora(l));
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
            // Update GenerationParameters (unified model)
            await _state.LoadGenerationParametersFromImage(image);
        }

        public void SetGenerationParameter(Image source, string parameter, bool isImg2Img)
        {
            switch (parameter)
            {
                case "Prompt":
                    var cleanedPrompt = ParseAndCleanCopiedPrompt(source.Prompt, false, isImg2Img);
                    SetGenerationParameterFragment(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Positive, cleanedPrompt);
                    break;
                case "NegativePrompt":
                    var cleanedNegative = ParseAndCleanCopiedPrompt(source.NegativePrompt, true, isImg2Img);
                    SetGenerationParameterFragment(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Negative, cleanedNegative);
                    break;
                case "SamplerIndex":
                    var sampler = _db.GetSampler(source.SamplerId).Result;
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.SamplerName, sampler);
                    break;
                case "Scheduler":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Scheduler, source.Scheduler);
                    break;
                case "Seed":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Seed, source.Seed);
                    break;
                case "Steps":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Steps, source.Steps);
                    break;
                case "CfgScale":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Cfg, (double?)source.CfgScale);
                    break;
                case "Width":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.Latent, FragmentKeys.Params.Width, source.Width);
                    break;
                case "Height":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.Latent, FragmentKeys.Params.Height, source.Height);
                    break;
                case "DenoisingStrength":
                    SetGenerationParameterFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Denoise, source.DenoisingStrength);
                    break;
            }

            _events.Publish(new ParametersChangedEventArgs(isImg2Img ? "Img2Img" : "Txt2Img"));
        }

        /// <summary>
        /// Sets a value in a GenerationParameters fragment.
        /// Creates the fragment if it doesn't exist.
        /// </summary>
        private void SetGenerationParameterFragment(string fragmentId, string key, object? value)
        {
            var fragment = _state.GenerationParameters.GetOrCreateFragment(fragmentId);
            fragment.SetValue(key, value);
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
