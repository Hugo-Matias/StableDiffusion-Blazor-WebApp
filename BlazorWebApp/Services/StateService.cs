using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using BlazorWebApp.Models.Fragments;
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
            
            // Create default prompts fragment using typed class
            var prompts = new PromptsFragment
            {
                Id = FragmentKeys.Fragments.Prompts,
                IsActive = true,
                Positive = "",
                Negative = ""
            };
            GenerationParameters.Fragments[FragmentKeys.Fragments.Prompts] = prompts;
            
            // Create default main_sampler fragment with settings defaults using typed class
            var sampler = new SamplerFragment
            {
                Id = FragmentKeys.Fragments.MainSampler,
                IsActive = true,
                Seed = settings.Generation.Shared.Seed,
                Steps = settings.Generation.Shared.Steps.Value,
                Cfg = settings.Generation.Shared.CfgScale.Value,
                SamplerName = settings.Generation.Shared.Sampler,
                Scheduler = "normal",
                Denoise = (float)settings.Generation.Shared.Denoising.Value
            };
            GenerationParameters.Fragments[FragmentKeys.Fragments.MainSampler] = sampler;
            
            // Create default latent/resolution fragment using typed class
            var latent = new LatentFragment
            {
                Id = FragmentKeys.Fragments.Latent,
                IsActive = true,
                Width = settings.Generation.Shared.Resolution.Width,
                Height = settings.Generation.Shared.Resolution.Height,
                BatchSize = settings.Generation.Shared.Batch.Size.Value
            };
            GenerationParameters.Fragments[FragmentKeys.Fragments.Latent] = latent;
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
            InitializeGenerationParameters();
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
                
                // Load GenerationParameters if present
                if (dbState.GenerationParameters != null && dbState.GenerationParameters.Fragments.Count > 0)
                {
                    GenerationParameters = dbState.GenerationParameters;
                }
                else
                {
                    // No saved GenerationParameters, initialize with defaults
                    InitializeGenerationParameters();
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
            entity.GenerationParameters = GenerationParameters;

            await _db.UpdateState(entity);
        }

        private void NormalizeState()
        {
            StateNormalizer.Normalize(State);
        }

        private void PublishStateChangedEvents()
        {
            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.AppState,
                NewValue = State
            });
            
            // Publish GenerationParameters changed event
            _events.Publish(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
        }

        #region Parameter Loading from Images

        /// <summary>
        /// Loads all parameters from an image entity into GenerationParameters.
        /// This populates the fragments with values from the saved image.
        /// </summary>
        public async Task LoadGenerationParametersFromImage(Image image)
        {
            // Ensure fragments exist using typed access
            var prompts = GenerationParameters.GetOrCreateFragment<PromptsFragment>(FragmentKeys.Fragments.Prompts);
            var sampler = GenerationParameters.GetOrCreateFragment<SamplerFragment>(FragmentKeys.Fragments.MainSampler);
            
            // Set prompts
            prompts.Positive = image.Prompt ?? "";
            prompts.Negative = image.NegativePrompt ?? "";
            
            // Set sampler values
            var samplerName = await _db.GetSampler(image.SamplerId);
            sampler.Seed = image.Seed;
            sampler.Steps = image.Steps;
            sampler.Cfg = image.CfgScale;
            sampler.SamplerName = samplerName ?? "euler";
            sampler.Scheduler = image.Scheduler ?? "normal";
            sampler.Denoise = (float)image.DenoisingStrength;
            
            // Set resolution - try to find latent fragment or create one
            var latent = GenerationParameters.GetFragment<LatentFragment>(FragmentKeys.Fragments.Latent);
            if (latent == null)
            {
                // Check other fragments by type
                latent = GenerationParameters.Fragments.Values.OfType<LatentFragment>().FirstOrDefault();
            }
            
            if (latent != null)
            {
                latent.Width = image.Width;
                latent.Height = image.Height;
            }
            else
            {
                // Create new latent fragment
                latent = new LatentFragment
                {
                    Id = FragmentKeys.Fragments.Latent,
                    IsActive = true,
                    Width = image.Width,
                    Height = image.Height,
                    BatchSize = 1
                };
                GenerationParameters.Fragments[FragmentKeys.Fragments.Latent] = latent;
            }
            
            // Publish event
            _events.Publish(new GenerationParametersChangedEventArgs(GenerationParameterChangeType.ParametersLoaded));
        }

        #endregion

        #region Workflow Management

        /// <summary>
        /// Sets the workflow base and resets workflow assets to defaults if the base changes.
        /// </summary>
        public void SetWorkflowBase(ModelBase workflowBase)
        {
            var previousBase = State.Generation.WorkflowBase;
            State.Generation.WorkflowBase = workflowBase;

            // If base changed, reset workflow assets to use workflow defaults
            if (previousBase != workflowBase)
            {
                ResetWorkflowAssetsToDefaults();

                // Update CurrentWorkflowId to the first workflow matching the new base
                var defaultWorkflow = State.Generation.Workflows?.FirstOrDefault(w => w.Base == workflowBase);
                if (defaultWorkflow != null)
                {
                    State.Generation.CurrentWorkflowId = defaultWorkflow.Id;
                    GenerationParameters.WorkflowId = defaultWorkflow.Id;
                }
                else
                {
                    State.Generation.CurrentWorkflowId = null;
                    GenerationParameters.WorkflowId = null;
                }
            }

            // Publish StateChangedEventArgs for components using EventService
            _events.Publish(new StateChangedEventArgs
            {
                ChangeType = StateChangeType.WorkflowBase,
                NewValue = workflowBase
            });
        }

        /// <summary>
        /// Resets all workflow assets to use the workflow template defaults.
        /// </summary>
        private void ResetWorkflowAssetsToDefaults()
        {
            // Clear existing assets first
            GenerationParameters.Assets.Clear();
            
            // Get the new workflow for each mode and set assets to its defaults
            var modes = new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid, ModeType.Extras };

            foreach (var mode in modes)
            {
                var workflows = GetWorkflowsForMode(mode);
                var workflow = workflows?.FirstOrDefault(w => w.Base == State.Generation.WorkflowBase);

                if (workflow?.Assets == null || workflow.Assets.Count == 0)
                    continue;

                // Set to workflow defaults (merge, don't overwrite existing)
                foreach (var asset in workflow.Assets)
                {
                    if (!string.IsNullOrWhiteSpace(asset.DefaultValue) && 
                        !GenerationParameters.Assets.ContainsKey(asset.Parameter))
                    {
                        GenerationParameters.Assets[asset.Parameter] = asset.DefaultValue;
                    }
                }
            }
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
        /// Migrates legacy SDModel and Vae from AppState to GenerationParameters.Assets.
        /// </summary>
        public void MigrateLegacySettings()
        {
#pragma warning disable CS0618 // Suppress obsolete warnings for migration

            // Migrate legacy SDModel from AppState
            if (!string.IsNullOrWhiteSpace(State?.Generation?.SDModel) && State.Generation.SDModel != "Loading...")
            {
                var currentModel = GenerationParameters.Assets.GetValueOrDefault(FragmentKeys.Assets.Model);
                if (string.IsNullOrWhiteSpace(currentModel))
                    GenerationParameters.Assets[FragmentKeys.Assets.Model] = State.Generation.SDModel;

                // Clear legacy property after migration
                State.Generation.SDModel = null;
            }

            // Migrate legacy Vae from AppState
            if (!string.IsNullOrWhiteSpace(State?.Generation?.Vae))
            {
                var currentVae = GenerationParameters.Assets.GetValueOrDefault(FragmentKeys.Assets.Vae);
                if (string.IsNullOrWhiteSpace(currentVae))
                    GenerationParameters.Assets[FragmentKeys.Assets.Vae] = State.Generation.Vae;

                // Clear legacy property after migration
                State.Generation.Vae = null;
            }

#pragma warning restore CS0618
        }

        #endregion
    }
}
