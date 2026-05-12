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
        private readonly IGenerationParameterService _parameterService;

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
            ISessionService session,
            IGenerationParameterService parameterService)
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
            _parameterService = parameterService;

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
        public string? GetCurrentModel(ModeType? mode = null) => _models.GetCurrentModel(mode);

        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            await _models.SetCurrentModel(modelTitle, mode);
            await SaveState();
        }

        public async Task SetSDModel(string modelTitle) => await SetCurrentModel(modelTitle);
        public string? GetCurrentVae(ModeType? mode = null) => GetWorkflowAsset("Vae");

        public async Task SetCurrentVae(string vae, ModeType? mode = null)
        {
            SetWorkflowAsset("Vae", vae);
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
                if (workflow != null && !IsWorkflowDisabled(workflow.Id)) return workflow;
            }

            if (_state.State.Generation.WorkflowBase != default)
                return _state.State.Generation.Workflows.FirstOrDefault(w => w.Base == _state.State.Generation.WorkflowBase && !IsWorkflowDisabled(w.Id));

            return _state.State.Generation.Workflows.FirstOrDefault(w => !IsWorkflowDisabled(w.Id));
        }

        public Workflow GetWorkflowById(Guid id) => _state.State.Generation.Workflows.FirstOrDefault(w => w.Id == id);

        public List<Workflow> GetWorkflowsForMode(ModeType mode)
        {
            if (_state.State?.Generation?.Workflows == null)
                return new List<Workflow>();

            return _state.State.Generation.Workflows.Where(w => w.Mode == mode).OrderBy(w => w.Title).ToList();
        }

        public List<Workflow> GetEnabledWorkflowsForBase(ModelBase? baseModel = null)
        {
            var gen = _state.State?.Generation;
            if (gen?.Workflows == null || gen.Workflows.Count == 0)
                return new List<Workflow>();

            var target = baseModel ?? gen.WorkflowBase;
            var disabled = new HashSet<Guid>(gen.DisabledWorkflowIds ?? new List<Guid>());

            return gen.Workflows
                .Where(w => w.Base == target && !disabled.Contains(w.Id))
                .OrderBy(w => (int)w.Mode)
                .ThenBy(w => w.Title)
                .ToList();
        }

        public bool IsWorkflowDisabled(Guid workflowId)
        {
            var disabled = _state.State?.Generation?.DisabledWorkflowIds;
            return disabled?.Contains(workflowId) == true;
        }

        public async Task SetWorkflowEnabledAsync(Guid workflowId, bool enabled)
        {
            var gen = _state.State?.Generation;
            var workflow = gen?.Workflows?.FirstOrDefault(w => w.Id == workflowId);
            if (gen == null || workflow == null) return;

            gen.DisabledWorkflowIds ??= new List<Guid>();
            var wasDisabled = gen.DisabledWorkflowIds.Contains(workflowId);
            if (enabled == !wasDisabled) return;

            if (enabled)
            {
                gen.DisabledWorkflowIds.RemoveAll(id => id == workflowId);
            }
            else
            {
                gen.DisabledWorkflowIds.Add(workflowId);
            }

            var currentWorkflowChanged = false;
            if (!enabled)
            {
                var replacementId = ResolveWorkflowForBase(workflow.Base);

                if (gen.CurrentWorkflowId == workflowId)
                {
                    gen.CurrentWorkflowId = replacementId;
                    currentWorkflowChanged = true;
                }

                if (gen.LastWorkflowByBase != null
                    && gen.LastWorkflowByBase.TryGetValue(workflow.Base, out var lastId)
                    && lastId == workflowId)
                {
                    if (replacementId.HasValue)
                        gen.LastWorkflowByBase[workflow.Base] = replacementId.Value;
                    else
                        gen.LastWorkflowByBase.Remove(workflow.Base);
                }
            }
            else if (gen.CurrentWorkflowId == null && gen.WorkflowBase == workflow.Base)
            {
                gen.CurrentWorkflowId = workflowId;
                gen.LastWorkflowByBase[workflow.Base] = workflowId;
                currentWorkflowChanged = true;
            }

            await SaveState();

            _events.Publish(new WorkflowAvailabilityChangedEventArgs
            {
                WorkflowId = workflowId,
                WorkflowBase = workflow.Base,
                IsEnabled = enabled
            });

            if (currentWorkflowChanged)
            {
                _events.Publish(new WorkflowChangedEventArgs(gen.CurrentWorkflowId ?? Guid.Empty, "WorkflowAvailability"));
            }

            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.WorkflowAvailability,
                NewValue = workflowId
            });
        }

        /// <summary>
        /// Resolves the best workflow id to route to for the current (or specified) base.
        /// Prefers the last-used workflow for that base (persisted in <see cref="AppStateGeneration.LastWorkflowByBase"/>);
        /// falls back to the first workflow available for the base; returns null if none exist.
        /// Used by the global Generate navigation button.
        /// </summary>
        public Guid? ResolveWorkflowForBase(ModelBase? baseModel = null)
        {
            var gen = _state.State?.Generation;
            if (gen?.Workflows == null || gen.Workflows.Count == 0) return null;

            var target = baseModel ?? gen.WorkflowBase;
            var enabledWorkflows = GetEnabledWorkflowsForBase(target);
            if (enabledWorkflows.Count == 0) return null;

            if (gen.LastWorkflowByBase != null
                && gen.LastWorkflowByBase.TryGetValue(target, out var lastId)
                && enabledWorkflows.Any(w => w.Id == lastId))
            {
                return lastId;
            }

            return enabledWorkflows[0].Id;
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

        public void SetCurrentWorkflow(Guid workflowId)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null || IsWorkflowDisabled(workflowId)) return;

            _state.State.Generation.CurrentWorkflowId = workflowId;
            _state.State.Generation.WorkflowBase = workflow.Base;
            _state.State.Generation.LastWorkflowByBase[workflow.Base] = workflowId;

            _events.Publish(new StateChangedEventArgs());
            _events.Publish(new WorkflowChangedEventArgs(workflowId, "Set"));
        }

        public async Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver)
        {
            var workflow = GetWorkflowById(workflowId);
            if (workflow == null || IsWorkflowDisabled(workflowId)) return false;

            _state.State.Generation.CurrentWorkflowId = workflowId;
            _state.State.Generation.WorkflowBase = workflow.Base;
            _state.State.Generation.LastWorkflowByBase[workflow.Base] = workflowId;

            // Note: Do NOT set GenerationParameters.WorkflowId here.
            // It will be updated by InitializeFromWorkflowAsync/InitializeFromWorkflowInternal
            // after the previous workflow's state has been saved.

            bool assetsInitialized = true;
            if (workflow.Assets != null && workflow.Assets.Count > 0)
            {
                var assets = GetOrCreateWorkflowAssets();
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

            // NOTE: Do NOT call SetDefaultBaseModel() here. It would mutate
            // GenerationParameters.Assets["Model"] directly while the downstream
            // InitializeFromWorkflowAsync (invoked by Generate.OnStateChanged) is
            // racing to save the previous workflow's state and restore the new one,
            // corrupting both. Asset defaults are owned by InitializeFromWorkflowAsync
            // via the workflow's metadata, so nothing needs to be written here.
        }

        public void ResetCurrentWorkflow()
        {
            _state.State.Generation.CurrentWorkflowId = null;
            _state.State.Generation.WorkflowBase = default;

            GetOrCreateWorkflowAssets().Clear();

            _events.Publish(new WorkflowChangedEventArgs(Guid.Empty, "Reset"));
        }

        public void SetDefaultBaseModel()
        {
            var workflow = _state.State.Generation.Workflows?.FirstOrDefault(w => w.Base == _state.State.Generation.WorkflowBase);
            if (workflow?.Assets == null || workflow.Assets.Count == 0) return;

            // Get the default model from workflow assets (C# workflows define defaults in metadata)
            var modelAsset = workflow.Assets.FirstOrDefault(a =>
                a.Type == AssetType.DiffusionModel || a.Type == AssetType.CheckpointModel);

            if (modelAsset != null && !string.IsNullOrWhiteSpace(modelAsset.DefaultValue))
            {
                // Update GenerationParameters.Assets (unified model)
                _state.GenerationParameters.Assets["Model"] = modelAsset.DefaultValue;
                _events.Publish(new ModelsChangedEventArgs());
            }
        }

        #endregion

        #region Workflow Assets

        public string? GetWorkflowAsset(string parameter)
        {
            if (_state.GenerationParameters?.Assets?.TryGetValue(parameter, out var value) == true)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        public void SetWorkflowAsset(string parameter, string value)
        {
            _state.GenerationParameters.Assets[parameter] = value;
        }

        public Dictionary<string, string>? GetWorkflowAssets()
        {
            return _state.GenerationParameters?.Assets;
        }

        private Dictionary<string, string> GetOrCreateWorkflowAssets()
        {
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

        public async Task<string> ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img)
        {
            var loras = Parser.ExtractLorasFromPrompt(prompt, out var cleanedFromLoras, isNegative);
            await ResolveLoraPathsAsync(loras);
            SetLoras(loras, isImg2Img);

            return CleanStylesFromPrompt(cleanedFromLoras, isNegative);
        }

        /// <summary>
        /// Variant of <see cref="ParseAndCleanCopiedPrompt"/> used when the caller needs to
        /// either apply loras immediately or queue them for a pending workflow switch.
        /// </summary>
        private async Task<string> ParseAndCleanCopiedPromptInternal(string prompt, bool isNegative, bool isImg2Img, bool queueLoras)
        {
            var loras = Parser.ExtractLorasFromPrompt(prompt, out var cleanedFromLoras, isNegative);
            await ResolveLoraPathsAsync(loras);

            if (queueLoras)
            {
                // When navigating to a different workflow, InitializeFromWorkflowAsync clears
                // Current.Loras before ApplyPendingOverrides is invoked. Queue them so they
                // survive the workflow switch and land in Current.Loras afterwards.
                foreach (var lora in loras)
                {
                    if (string.IsNullOrWhiteSpace(lora.Name)) continue;
                    _parameterService.QueuePendingLora(lora);
                }
            }
            else
            {
                SetLoras(loras, isImg2Img);
            }

            return CleanStylesFromPrompt(cleanedFromLoras, isNegative);
        }

        private string CleanStylesFromPrompt(string cleanedFromLoras, bool isNegative)
        {
            var cleanedFromStyles = cleanedFromLoras;
            if (_state.GenerationParameters?.Styles != null)
            {
                foreach (var style in _state.GenerationParameters.Styles)
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

            // Resolve full relative paths for LoRAs extracted from the prompt
            // (inline <lora:filename:strength> only captures the filename).
            await ResolveLoraPathsAsync(_state.GenerationParameters?.Loras);
        }

        public Task SetGenerationParameter(Image source, string parameter, bool isImg2Img)
        {
            return SetOrQueueGenerationParameter(source, parameter, isImg2Img, queue: false);
        }

        public Task QueueGenerationParameter(Image source, string parameter, bool isImg2Img)
        {
            return SetOrQueueGenerationParameter(source, parameter, isImg2Img, queue: true);
        }

        private async Task SetOrQueueGenerationParameter(Image source, string parameter, bool isImg2Img, bool queue)
        {
            switch (parameter)
            {
                case "Prompt":
                    var cleanedPrompt = await ParseAndCleanCopiedPromptInternal(source.Prompt, false, isImg2Img, queueLoras: queue);
                    ApplyOrQueue(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Positive, cleanedPrompt, queue);
                    break;
                case "NegativePrompt":
                    var cleanedNegative = await ParseAndCleanCopiedPromptInternal(source.NegativePrompt, true, isImg2Img, queueLoras: queue);
                    ApplyOrQueue(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Negative, cleanedNegative, queue);
                    break;
                case "SamplerIndex":
                    var sampler = _db.GetSampler(source.SamplerId).Result;
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.SamplerName, sampler, queue);
                    break;
                case "Scheduler":
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Scheduler, source.Scheduler, queue);
                    break;
                case "Seed":
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Seed, source.Seed, queue);
                    break;
                case "Steps":
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Steps, source.Steps, queue);
                    break;
                case "CfgScale":
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Cfg, (double?)source.CfgScale, queue);
                    break;
                case "Width":
                    ApplyOrQueue(FragmentKeys.Fragments.Latent, FragmentKeys.Params.Width, source.Width, queue);
                    break;
                case "Height":
                    ApplyOrQueue(FragmentKeys.Fragments.Latent, FragmentKeys.Params.Height, source.Height, queue);
                    break;
                case "DenoisingStrength":
                    ApplyOrQueue(FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Denoise, source.DenoisingStrength, queue);
                    break;
            }

            if (!queue)
            {
                _events.Publish(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
            }
        }

        private void ApplyOrQueue(string fragmentId, string key, object? value, bool queue)
        {
            if (queue)
            {
                _parameterService.QueuePendingOverride(fragmentId, key, value);
            }
            else
            {
                SetGenerationParameterFragment(fragmentId, key, value);
            }
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

        /// <summary>
        /// Resolves the full relative path (including subfolders) for each LoRA by matching
        /// its Name against the list of available LoRAs reported by ComfyUI. The inline
        /// prompt syntax <c>&lt;lora:filename:strength&gt;</c> only captures the filename,
        /// but ComfyUI's LoraLoader requires the relative path (e.g. <c>style/anime.safetensors</c>).
        /// Only LoRAs missing a Path are updated; existing Path values are preserved.
        /// </summary>
        private async Task ResolveLoraPathsAsync(IEnumerable<Lora>? loras)
        {
            if (loras == null) return;
            var pending = loras.Where(l => l != null && !string.IsNullOrWhiteSpace(l.Name) && string.IsNullOrWhiteSpace(l.Path)).ToList();
            if (pending.Count == 0) return;

            List<string> available;
            try
            {
                available = await _capi.GetLoras() ?? new List<string>();
            }
            catch
            {
                // If the backend is unreachable we keep the original Name-only value;
                // the workflow builder will still attempt it as a fallback.
                return;
            }
            if (available.Count == 0) return;

            var comp = StringComparison.InvariantCultureIgnoreCase;
            foreach (var lora in pending)
            {
                var match = available.FirstOrDefault(a => string.Equals(Path.GetFileNameWithoutExtension(a), lora.Name, comp))
                            ?? available.FirstOrDefault(a => string.Equals(Path.GetFileName(a), lora.Name, comp));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    lora.Path = match;
                    lora.Name = Path.GetFileNameWithoutExtension(match);
                }
            }
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
