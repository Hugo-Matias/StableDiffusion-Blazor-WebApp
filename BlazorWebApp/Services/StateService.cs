using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for managing application state and generation parameters.
    /// Handles loading, saving, initialization, and normalization of state data.
    /// </summary>
    public class StateService : IStateService
    {
        private readonly IDatabaseService _db;
        private readonly IConfiguration _configuration;
        private readonly IEventService _events;
        private readonly ISettingsService _settings;

        public AppState State { get; private set; }
        public Txt2ImgParameters ParametersTxt2Img { get; private set; }
        public Img2ImgParameters ParametersImg2Img { get; private set; }
        public Models.UpscaleParameters ParametersUpscale { get; private set; }
        public Img2VidParameters ParametersImg2Vid { get; private set; }
        
        /// <inheritdoc />
        public GenerationParameters GenerationParameters { get; private set; } = new();
        
        /// <summary>
        /// Initializes GenerationParameters with default values from settings.
        /// Called when starting fresh (no saved state) or resetting parameters.
        /// </summary>
        public void InitializeGenerationParameters()
        {
            var settings = _settings.Settings;
            
            GenerationParameters = new GenerationParameters();
            
            // Create default prompts fragment
            var promptsFragment = new FragmentParameters
            {
                FragmentFile = "prompts.sbn",
                IsActive = true
            };
            promptsFragment.SetValue("positive", "");
            promptsFragment.SetValue("negative", "");
            GenerationParameters.Fragments["prompts"] = promptsFragment;
            
            // Create default main_sampler fragment with settings defaults
            var samplerFragment = new FragmentParameters
            {
                FragmentFile = "sampler.sbn",
                IsActive = true
            };
            samplerFragment.SetValue("seed", (long)settings.Generation.Shared.Seed);
            samplerFragment.SetValue("steps", settings.Generation.Shared.Steps.Value);
            samplerFragment.SetValue("cfg", (double)settings.Generation.Shared.CfgScale.Value);
            samplerFragment.SetValue("sampler_name", settings.Generation.Shared.Sampler);
            samplerFragment.SetValue("scheduler", "normal");
            samplerFragment.SetValue("denoise", settings.Generation.Shared.Denoising.Value);
            GenerationParameters.Fragments["main_sampler"] = samplerFragment;
            
            // Create default latent/resolution fragment
            var latentFragment = new FragmentParameters
            {
                FragmentFile = "latent.sbn",
                IsActive = true
            };
            latentFragment.SetValue("width", settings.Generation.Shared.Resolution.Width);
            latentFragment.SetValue("height", settings.Generation.Shared.Resolution.Height);
            latentFragment.SetValue("batch_size", settings.Generation.Shared.Batch.Size.Value);
            GenerationParameters.Fragments["latent"] = latentFragment;
        }

        public StateService(
            IDatabaseService db,
            IConfiguration configuration,
            IEventService events,
            ISettingsService settings)
        {
            _db = db;
            _configuration = configuration;
            _events = events;
            _settings = settings;

            // Initialize with defaults - will be replaced by LoadState if needed
            State = new AppState(_settings.Settings);
            
            ModeType[] modes = new ModeType[4] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Extras, ModeType.Img2Vid };
            InitializeParameters(modes);
        }

        public void InitializeParameters(ModeType[] modes)
        {
            // Use settings from SettingsService for proper defaults
            var defaultParameters = CreateDefaultParameters();

            if (modes.Contains(ModeType.Txt2Img))
                InitializeTxt2ImgParameters(defaultParameters);

            if (modes.Contains(ModeType.Img2Img))
                InitializeImg2ImgParameters(defaultParameters);

            if (modes.Contains(ModeType.Extras))
                InitializeUpscaleParameters(defaultParameters);

            if (modes.Contains(ModeType.Img2Vid))
                InitializeImg2VidParameters();
        }

        public async Task LoadState()
        {
            // Load the latest AutoSave for current StateVersion
            await LoadStateInternal(1); // ID 1 = AutoSave
        }

        public async Task LoadState(int stateId)
        {
            await LoadStateInternal(stateId);
        }

        /// <summary>
        /// Internal method that actually loads application state from the database.
        /// </summary>
        private async Task LoadStateInternal(int stateId)
        {
            var dbState = await _db.GetState(stateId);
            
            if (dbState != null)
            {
                // Load AppState if present
                if (dbState.AppState != null)
                {
                    State = dbState.AppState;
                    
                    // IMPORTANT: Do NOT restore workflows from database state
                    // Workflows must always be loaded fresh from disk template files
                    // because they may have been updated (e.g., Sources added)
                    // The caller (OrchestratorService.LoadState) will call RefreshWorkflows
                    // to populate State.Generation.Workflows from disk
                    if (State.Generation != null)
                    {
                        State.Generation.Workflows = null;
                    }
                }

                // Load parameters if present
                if (dbState.Txt2ImgParameters != null)
                {
                    ParametersTxt2Img = dbState.Txt2ImgParameters;
                }

                if (dbState.Img2ImgParameters != null)
                {
                    ParametersImg2Img = dbState.Img2ImgParameters;
                }

                if (dbState.UpscaleParameters != null)
                {
                    ParametersUpscale = dbState.UpscaleParameters;
                }

                if (dbState.Img2VidParameters != null)
                {
                    ParametersImg2Vid = dbState.Img2VidParameters;
                }
                
                // Load new GenerationParameters if present
                if (dbState.GenerationParameters != null)
                {
                    GenerationParameters = dbState.GenerationParameters;
                }

                // Normalize state after loading
                NormalizeState();

                // Publish events to notify components that state has changed
                PublishStateChangedEvents();
            }
        }

        public async Task SaveState()
        {
            NormalizeState();

            // Get the latest AutoSave for current StateVersion
            var stateVersion = int.Parse(_configuration["StateVersion"]);
            var entity = await _db.GetState(1); // DatabaseService.GetState(1) has fallback logic
            
            if (entity == null)
            {
                // No AutoSave exists for this version, create new one
                entity = new State
                {
                    Title = "AutoSave",
                    CreationDate = DateTime.Now,
                    Version = stateVersion
                };
            }

            entity.AppState = State;
            entity.Txt2ImgParameters = ParametersTxt2Img;
            entity.Img2ImgParameters = ParametersImg2Img;
            entity.UpscaleParameters = ParametersUpscale;
            entity.Img2VidParameters = ParametersImg2Vid;
            entity.GenerationParameters = GenerationParameters;

            await _db.UpdateState(entity);
        }

        private void NormalizeState()
        {
            StateNormalizer.Normalize(State);
            StateNormalizer.Normalize(ParametersTxt2Img);
            StateNormalizer.Normalize(ParametersImg2Img);
            StateNormalizer.Normalize(ParametersUpscale);
            StateNormalizer.Normalize(ParametersImg2Vid);
        }

        private void PublishStateChangedEvents()
        {
            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.AppState,
                NewValue = State
            });

            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.Txt2ImgParameters,
                NewValue = ParametersTxt2Img
            });

            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.Img2ImgParameters,
                NewValue = ParametersImg2Img
            });

            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.UpscaleParameters,
                NewValue = ParametersUpscale
            });

            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.Img2VidParameters,
                NewValue = ParametersImg2Vid
            });
            
            // Publish GenerationParameters changed event
            _events.Publish(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
        }

        private SharedParameters CreateDefaultParameters()
        {
            // Use settings from SettingsService for proper defaults
            var settings = _settings.Settings;
            
            return new SharedParameters
            {
                Comfy = new() { Workflow = new() },
                Loras = new(),
                Steps = settings.Generation.Shared.Steps.Value,
                SamplerIndex = settings.Generation.Shared.Sampler,
                Seed = settings.Generation.Shared.Seed,
                CfgScale = settings.Generation.Shared.CfgScale.Value,
                Width = settings.Generation.Shared.Resolution.Width,
                Height = settings.Generation.Shared.Resolution.Height,
                NIter = settings.Generation.Shared.Batch.Count.Value,
                BatchSize = settings.Generation.Shared.Batch.Size.Value,
                DenoisingStrength = settings.Generation.Shared.Denoising.Value,
                RestoreFaces = settings.Generation.Shared.FaceRestoration,
                Tiling = settings.Generation.Shared.Tilling
            };
        }

        private void InitializeTxt2ImgParameters(SharedParameters defaultParameters)
        {
            ParametersTxt2Img = new Txt2ImgParameters(defaultParameters)
            {
                EnableHR = false,
                FirstphaseWidth = 0,
                FirstphaseHeight = 0,
                HRUpscaler = "Latent",
                HRScale = 2.0,
                HRWidth = 0,
                HRHeight = 0,
                HRSecondPassSteps = 0
                // SeedVR2, ConditioningVariation, SeedVarianceEnhancer are initialized by UI
                // and copied in ImageService.BuildTxt2ImgParametersAsync
            };
        }

        private void InitializeImg2ImgParameters(SharedParameters defaultParameters)
        {
            ParametersImg2Img = new Img2ImgParameters(defaultParameters)
            {
                MaskBlur = 4,
                ResizeMode = 0,
                InpaintingFill = 1,
                InpaintFullRes = true,
                InpaintFullResPadding = 32,
                InpaintingMaskInvert = 0
                // Scripts system removed - no longer supported
            };
        }

        private void InitializeUpscaleParameters(SharedParameters defaultParameters)
        {
            ParametersUpscale = new Models.UpscaleParameters(defaultParameters)
            {
                ResizeMode = 0,
                ShowResults = true,
                GfpganVisibility = 0,
                CodeformerVisibility = 0,
                CodeformerWeight = 0,
                UpscalingMultiplier = 2,
                UpscalingWidth = 512,
                UpscalingHeight = 512,
                UpscalingCrop = true,
                UpscalerPrimary = "None",
                UpscalerSecondary = "None",
                UpscalerSecondaryVisibility = 0,
                UpscalePriority = false
            };
        }

        private void InitializeImg2VidParameters()
        {
            ParametersImg2Vid = new Img2VidParameters
            {
                WorkflowAssets = new Dictionary<string, string>
                {
                    ["HighModel"] = "",
                    ["LowModel"] = "",
                    ["Clip"] = "",
                    ["ClipVision"] = "",
                    ["Vae"] = ""
                },
                Length = 81,
                FrameRate = 16,
                MotionAmplitude = 1.1f,
                Shift = 5,
                Steps = 8,
                CfgScale = 1.0f,
                SamplerName = "euler",
                Scheduler = "simple",
                Width = 768,
                Height = 768,
                Seed = -1,
                BatchSize = 1,
                FrameInterpolationActive = true,
                FrameInterpolationScaleBy = 2.0,
                FrameInterpolationMultiplier = 2,
                FrameInterpolationRifeModel = "rife49.pth",
                Loras = new List<Lora>()
            };
        }

        #region Parameter Loading from Images

        /// <summary>
        /// Loads all parameters from an image entity into GenerationParameters (new flow).
        /// This populates the fragments with values from the saved image.
        /// </summary>
        public async Task LoadGenerationParametersFromImage(Image image)
        {
            // Ensure fragments exist
            if (!GenerationParameters.Fragments.ContainsKey("prompts"))
            {
                GenerationParameters.Fragments["prompts"] = new FragmentParameters 
                { 
                    FragmentFile = "prompts.sbn", 
                    IsActive = true 
                };
            }
            if (!GenerationParameters.Fragments.ContainsKey("main_sampler"))
            {
                GenerationParameters.Fragments["main_sampler"] = new FragmentParameters 
                { 
                    FragmentFile = "sampler.sbn", 
                    IsActive = true 
                };
            }
            
            var promptsFragment = GenerationParameters.Fragments["prompts"];
            var samplerFragment = GenerationParameters.Fragments["main_sampler"];
            
            // Set prompts
            promptsFragment.SetValue("positive", image.Prompt ?? "");
            promptsFragment.SetValue("negative", image.NegativePrompt ?? "");
            
            // Set sampler values
            var samplerName = await _db.GetSampler(image.SamplerId);
            samplerFragment.SetValue("seed", image.Seed);
            samplerFragment.SetValue("steps", image.Steps);
            samplerFragment.SetValue("cfg", (double)image.CfgScale);
            samplerFragment.SetValue("sampler_name", samplerName ?? "euler");
            samplerFragment.SetValue("scheduler", image.Scheduler ?? "normal");
            samplerFragment.SetValue("denoise", image.DenoisingStrength);
            
            // Set resolution in any fragment that has it (latent, loader, etc.)
            foreach (var fragment in GenerationParameters.Fragments.Values)
            {
                if (fragment.HasValue("width") || fragment.HasValue("height"))
                {
                    fragment.SetValue("width", image.Width);
                    fragment.SetValue("height", image.Height);
                }
            }
            
            // If no fragment has width/height, create/update latent fragment
            if (!GenerationParameters.Fragments.Values.Any(f => f.HasValue("width")))
            {
                if (!GenerationParameters.Fragments.ContainsKey("latent"))
                {
                    GenerationParameters.Fragments["latent"] = new FragmentParameters 
                    { 
                        FragmentFile = "latent.sbn", 
                        IsActive = true 
                    };
                }
                GenerationParameters.Fragments["latent"].SetValue("width", image.Width);
                GenerationParameters.Fragments["latent"].SetValue("height", image.Height);
            }
            
            // Publish event
            _events.Publish(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
        }

        /// <summary>
        /// Loads all parameters from an image entity into the appropriate parameter set.
        /// This is used when loading an existing image's settings to replicate generation.
        /// Note: This method relies on OrchestratorService.ParseAndCleanCopiedPrompt() for prompt cleaning.
        /// </summary>
        public async Task LoadParametersFromImage(Image image, ModeType mode)
        {
            bool isImg2Img = mode == ModeType.Img2Img;

            if (isImg2Img)
            {
                // Note: Prompt cleaning is handled by caller using OrchestratorService.ParseAndCleanCopiedPrompt
                // We can't call it directly here to avoid circular dependency
                ParametersImg2Img.Prompt = image.Prompt ?? string.Empty;
                ParametersImg2Img.NegativePrompt = image.NegativePrompt ?? string.Empty;
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
                ParametersTxt2Img.Prompt = image.Prompt ?? string.Empty;
                ParametersTxt2Img.NegativePrompt = image.NegativePrompt ?? string.Empty;
                ParametersTxt2Img.SamplerIndex = await _db.GetSampler(image.SamplerId);
                ParametersTxt2Img.Steps = image.Steps;
                ParametersTxt2Img.Seed = image.Seed;
                ParametersTxt2Img.CfgScale = image.CfgScale;
                ParametersTxt2Img.Width = image.Width;
                ParametersTxt2Img.Height = image.Height;
                ParametersTxt2Img.DenoisingStrength = image.DenoisingStrength;
            }

            // Publish parameter changed event
            _events.Publish(new ParametersChangedEventArgs 
            { 
                ParametersType = isImg2Img ? "Img2Img" : "Txt2Img",
                Message = "LoadedFromImage"
            });
        }

        /// <summary>
        /// Sets a single parameter from an image entity.
        /// Used for copying individual parameters from images (e.g., just seed, just CFG).
        /// Note: This method relies on OrchestratorService.ParseAndCleanCopiedPrompt() for prompt cleaning.
        /// </summary>
        public void SetParameterFromImage(Image image, string parameter, ModeType mode)
        {
            string GetSampler() => _db.GetSampler(image.SamplerId).Result;

            bool isImg2Img = mode == ModeType.Img2Img;
            SharedParameters param = isImg2Img ? ParametersImg2Img : ParametersTxt2Img;
            
            switch (parameter)
            {
                case nameof(SharedParameters.Prompt):
                    // Note: Caller should handle prompt cleaning via OrchestratorService.ParseAndCleanCopiedPrompt
                    param.Prompt = image.Prompt;
                    break;
                case nameof(SharedParameters.NegativePrompt):
                    param.NegativePrompt = image.NegativePrompt;
                    break;
                case nameof(SharedParameters.SamplerIndex):
                    param.SamplerIndex = GetSampler();
                    break;
                case nameof(SharedParameters.Scheduler):
                    param.Scheduler = image.Scheduler;
                    break;
                case nameof(SharedParameters.Seed):
                    param.Seed = image.Seed;
                    break;
                case nameof(SharedParameters.Steps):
                    param.Steps = image.Steps;
                    break;
                case nameof(SharedParameters.CfgScale):
                    param.CfgScale = image.CfgScale;
                    break;
                case nameof(SharedParameters.Width):
                    param.Width = image.Width;
                    break;
                case nameof(SharedParameters.Height):
                    param.Height = image.Height;
                    break;
                case nameof(SharedParameters.DenoisingStrength):
                    param.DenoisingStrength = image.DenoisingStrength;
                    break;
            }

            // Publish parameter changed event
            _events.Publish(new ParametersChangedEventArgs 
            { 
                ParametersType = isImg2Img ? "Img2Img" : "Txt2Img",
                Message = $"SetParameter:{parameter}"
            });
        }

        #endregion

        #region Workflow Management

        /// <summary>
        /// Sets the workflow base and resets workflow assets to defaults if the base changes.
        /// Publishes StateChangedEventArgs to notify components.
        /// </summary>
        /// <param name="workflowBase">The new workflow base to set</param>
        public void SetWorkflowBase(ModelBase workflowBase)
        {
            var previousBase = State.Generation.WorkflowBase;
            State.Generation.WorkflowBase = workflowBase;

            // If base changed, reset workflow assets to use workflow defaults
            if (previousBase != workflowBase)
            {
                ResetWorkflowAssetsToDefaults();
            }

            // Publish StateChangedEventArgs for components using EventService
            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.WorkflowBase,
                NewValue = workflowBase
            });
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
        /// Migrates legacy SDModel and Vae from AppState to WorkflowAssets.
        /// Call this after loading state to ensure backward compatibility with old save files.
        /// </summary>
        public void MigrateLegacySettings()
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

        /// <summary>
        /// Gets workflows filtered by mode from the current workflows list.
        /// </summary>
        private List<Workflow> GetWorkflowsForMode(ModeType mode)
        {
            if (State?.Generation?.Workflows == null)
                return new List<Workflow>();

            return State.Generation.Workflows
                .Where(w => w.Mode == mode)
                .OrderBy(w => w.Title)
                .ToList();
        }

        /// <summary>
        /// Gets a workflow asset value for the specified mode.
        /// </summary>
        private string? GetWorkflowAsset(string parameter, ModeType? mode = null)
        {
            var assets = GetWorkflowAssetsForMode(mode);
            return assets?.GetValueOrDefault(parameter);
        }

        /// <summary>
        /// Sets a workflow asset value for the specified mode.
        /// </summary>
        private void SetWorkflowAsset(string parameter, string value, ModeType? mode = null)
        {
            var assets = GetOrCreateWorkflowAssetsForMode(mode);
            assets[parameter] = value;
        }

        /// <summary>
        /// Gets the WorkflowAssets dictionary for the specified mode.
        /// </summary>
        private Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
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

        #endregion
    }
}
