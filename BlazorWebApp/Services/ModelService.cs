using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing models and assets (checkpoints, VAEs, samplers, etc.).
    /// Handles model loading, selection, and asset resolution.
    /// </summary>
    public class ModelService : IModelService
    {
        private readonly IComfyUIService _comfyUI;
        private readonly SDAPIService _sdapi;
        private readonly IBackendService _backend;
        private readonly IStateService _state;
        private readonly IEventService _events;
        private readonly ProgressService _progress;
        private readonly IOService _io;
        private readonly IConfiguration _configuration;

        public List<SDModel> CheckpointModels { get; private set; } = new();
        public List<SDModel> DiffusionModels { get; private set; } = new();
        public List<string> VAEModels { get; private set; } = new();
        public List<string> ClipModels { get; private set; } = new();
        public List<string> ClipVisionModels { get; private set; } = new();
        public List<string> ADetailerModels { get; private set; } = new();

        // Delegate to BackendService for these (will be extracted in future iterations if needed)
        public List<Models.Sampler> Samplers => _backend.Samplers;
        public List<Scheduler> Schedulers => _backend.Schedulers;
        public List<Upscaler> Upscalers => _backend.Upscalers;

        public ModelService(
            IComfyUIService comfyUI,
            SDAPIService sdapi,
            IBackendService backend,
            IStateService state,
            IEventService events,
            ProgressService progress,
            IOService io,
            IConfiguration configuration)
        {
            _comfyUI = comfyUI;
            _sdapi = sdapi;
            _backend = backend;
            _state = state;
            _events = events;
            _progress = progress;
            _io = io;
            _configuration = configuration;
        }

        /// <summary>
        /// Loads models based on the current workflow's asset requirements.
        /// For ComfyUI: loads models based on workflow asset types (Checkpoint, Diffusion, VAE, CLIP, etc.)
        /// </summary>
        public async Task GetWorkflowModels(bool refresh = false)
        {
            if (!_backend.IsBackendAvailable)
                return;

            var currentWorkflow = GetCurrentWorkflow();
            if (currentWorkflow?.Assets == null || currentWorkflow.Assets.Count == 0)
            {
                // No assets defined, load checkpoints as fallback
                CheckpointModels = await _comfyUI.GetCheckpoints();
                PublishModelChanged();
                return;
            }

            // Load models based on asset types defined in workflow
            var assetTypes = currentWorkflow.Assets.Select(a => a.Type).Distinct().ToList();

            foreach (var assetType in assetTypes)
            {
                switch (assetType)
                {
                    case AssetType.CheckpointModel:
                        CheckpointModels = await _comfyUI.GetCheckpoints();
                        break;

                    case AssetType.DiffusionModel:
                        DiffusionModels = await _comfyUI.GetDiffusionModels();
                        break;

                    case AssetType.Vae:
                        VAEModels = await _comfyUI.GetVAEModels();
                        break;

                    case AssetType.Clip:
                        ClipModels = await _comfyUI.GetClipModels();
                        break;

                    case AssetType.ClipVision:
                        ClipVisionModels = await _comfyUI.GetClipVisionModels();
                        break;
                }
            }

            PublishModelChanged();
        }

        /// <summary>
        /// Loads VAE models from the backend.
        /// </summary>
        public async Task GetVAEModels()
        {
            if (_backend.IsBackendAvailable)
            {
                VAEModels = await _comfyUI.GetVAEModels();
            }
        }

        /// <summary>
        /// Loads ADetailer models from the backend.
        /// </summary>
        public async Task GetADetailerModels()
        {
            if (_backend.IsBackendAvailable)
            {
                ADetailerModels = await _comfyUI.GetBBoxDetailers();
            }
        }

        /// <summary>
        /// Sets the current model name based on mode using WorkflowAssets.
        /// </summary>
        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            if (!_backend.IsBackendAvailable)
                return;

            var currentWorkflow = GetCurrentWorkflow();

            if (currentWorkflow?.Assets != null && currentWorkflow.Assets.Count > 0)
            {
                var modelAssetTypes = currentWorkflow.Assets
                    .Where(a => a.Type == AssetType.CheckpointModel || a.Type == AssetType.DiffusionModel)
                    .Select(a => a.Type)
                    .Distinct()
                    .ToList();

                foreach (var assetType in modelAssetTypes)
                {
                    var models = GetModelsForAssetType(assetType);
                    var match = models.FirstOrDefault(m => m.Model_name.Contains(modelTitle, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        modelTitle = match.Model_name;
                        break;
                    }
                }
            }

            // Set model using WorkflowAssets
            var modelKey = mode == ModeType.Img2Vid ? "HighModel" : "Model";
            SetWorkflowAsset(modelKey, modelTitle, mode);

            PublishModelChanged();
            await _state.SaveState();
        }

        /// <summary>
        /// Sets the current VAE name based on mode using WorkflowAssets.
        /// </summary>
        public async Task SetCurrentVae(string vae, ModeType? mode = null)
        {
            SetWorkflowAsset("Vae", vae, mode);
            await _state.SaveState();
        }

        /// <summary>
        /// Gets the current model name based on mode using WorkflowAssets.
        /// For Txt2Img/Img2Img: returns "Model" asset
        /// For Img2Vid: returns "HighModel" asset
        /// </summary>
        public string GetCurrentModel(ModeType? mode = null)
        {
            var modelKey = mode == ModeType.Img2Vid ? "HighModel" : "Model";
            var value = GetWorkflowAsset(modelKey, mode);
            return !string.IsNullOrWhiteSpace(value) ? value : "Loading...";
        }

        /// <summary>
        /// Gets the current VAE name based on mode using WorkflowAssets.
        /// </summary>
        public string? GetCurrentVae(ModeType? mode = null)
        {
            return GetWorkflowAsset("Vae", mode);
        }

        /// <summary>
        /// Gets the appropriate model list based on asset type
        /// </summary>
        public List<SDModel> GetModelsForAssetType(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.CheckpointModel => CheckpointModels ?? new List<SDModel>(),
                AssetType.DiffusionModel => DiffusionModels ?? new List<SDModel>(),
                _ => new List<SDModel>()
            };
        }

        /// <summary>
        /// Gets available options for any asset type.
        /// Returns a list of filenames/model names that can be selected for the given asset type.
        /// </summary>
        public async Task<List<string>> GetAssetOptions(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.CheckpointModel => (CheckpointModels ?? await _comfyUI.GetCheckpoints())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                AssetType.DiffusionModel => (DiffusionModels ?? await _comfyUI.GetDiffusionModels())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                AssetType.Vae => VAEModels ?? await _comfyUI.GetVAEModels(),
                AssetType.Clip => ClipModels ?? await _comfyUI.GetClipModels(),
                AssetType.ClipVision => ClipVisionModels ?? await _comfyUI.GetClipVisionModels(),
                _ => new List<string>()
            };
        }

        #region Private Helper Methods

        private Workflow? GetCurrentWorkflow()
        {
            if (_state.State?.Generation?.Workflows == null || _state.State.Generation.Workflows.Count == 0)
                return null;

            // Priority 1: Use CurrentWorkflowId if set
            if (_state.State.Generation.CurrentWorkflowId.HasValue)
            {
                var workflow = _state.State.Generation.Workflows.FirstOrDefault(w => w.Id == _state.State.Generation.CurrentWorkflowId.Value);
                if (workflow != null)
                    return workflow;
            }

            // Priority 2: Fallback to first workflow matching WorkflowBase
            if (_state.State.Generation.WorkflowBase != default)
            {
                return _state.State.Generation.Workflows.FirstOrDefault(w => w.Base == _state.State.Generation.WorkflowBase);
            }

            // Priority 3: Return first available workflow
            return _state.State.Generation.Workflows.FirstOrDefault();
        }

        private string? GetWorkflowAsset(string parameter, ModeType? mode)
        {
            var assets = GetWorkflowAssetsForMode(mode);
            return assets?.GetValueOrDefault(parameter);
        }

        private void SetWorkflowAsset(string parameter, string value, ModeType? mode)
        {
            var assets = GetOrCreateWorkflowAssetsForMode(mode);
            assets[parameter] = value;
        }

        private Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
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
            switch (mode)
            {
                case ModeType.Img2Img:
                    _state.ParametersImg2Img.WorkflowAssets ??= new Dictionary<string, string>();
                    return _state.ParametersImg2Img.WorkflowAssets;
                case ModeType.Img2Vid:
                    _state.ParametersImg2Vid.WorkflowAssets ??= new Dictionary<string, string>();
                    return _state.ParametersImg2Vid.WorkflowAssets;
                case ModeType.Extras:
                    _state.ParametersUpscale.WorkflowAssets ??= new Dictionary<string, string>();
                    return _state.ParametersUpscale.WorkflowAssets;
                default:
                    _state.ParametersTxt2Img.WorkflowAssets ??= new Dictionary<string, string>();
                    return _state.ParametersTxt2Img.WorkflowAssets;
            }
        }

        private void PublishModelChanged()
        {
            _events.Publish(new ModelChangedEventArgs
            {
                PreviousModel = string.Empty, // TODO: Track previous model if needed
                NewModel = GetCurrentModel(),
                Mode = ModeType.Txt2Img // TODO: Track actual mode if needed
            });
        }

        #endregion
    }
}
