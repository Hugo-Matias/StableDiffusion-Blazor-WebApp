using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Dtos.WebUI;
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
        private readonly IStateDatabaseService _db;
        private readonly IConfiguration _configuration;
        private readonly IEventService _events;
        private readonly ISettingsService _settings;

        public AppState State { get; private set; }
        public Txt2ImgParameters ParametersTxt2Img { get; private set; }
        public Img2ImgParameters ParametersImg2Img { get; private set; }
        public Models.UpscaleParameters ParametersUpscale { get; private set; }
        public Img2VidParameters ParametersImg2Vid { get; private set; }

        public StateService(
            IStateDatabaseService db,
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
            var stateVersion = int.Parse(_configuration["StateVersion"]);
            var autoSave = await _db.GetState(1); // This will get AutoSave via DatabaseService fallback logic
            
            if (autoSave != null)
            {
                await LoadState(autoSave.Id);
            }
            else
            {
                // No AutoSave exists for this version, create one
                await SaveState();
            }
        }

        public async Task LoadState(int stateId)
        {
            var state = await _db.GetState(stateId);

            // If state doesn't exist, create AutoSave if this is ID 1
            if (state == null)
            {
                if (stateId == 1)
                {
                    // Create new AutoSave state with current values
                    await SaveState();
                }
                return;
            }

            // Load AppState if it exists in the preset
            if (state.AppState != null)
            {
                State = state.AppState;
                State.Generation.IsInterrupted = false;
            }

            // Load parameters if they exist in the preset (selective loading)
            if (state.Txt2ImgParameters != null)
                ParametersTxt2Img = state.Txt2ImgParameters;

            if (state.Img2ImgParameters != null)
                ParametersImg2Img = state.Img2ImgParameters;

            if (state.UpscaleParameters != null)
                ParametersUpscale = state.UpscaleParameters;

            if (state.Img2VidParameters != null)
                ParametersImg2Vid = state.Img2VidParameters;

            // Normalize and publish events
            NormalizeState();
            PublishStateChangedEvents();
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
                HRSecondPassSteps = 0,
                SeedVR2 = new SeedVR2Parameters
                {
                    IsActive = false,
                    Model = "",
                    BlocksToSwap = 1,
                    VaeTileSize = 512,
                    VaeTileOverlap = 64,
                    Resolution = 1024,
                    Scale = 2.0,
                    BatchSize = 1,
                    InputNoiseScale = 0.1f,
                    LatentNoiseScale = 0.1f
                },
                ConditioningVariation = new ConditioningVariationParameters
                {
                    IsActive = false,
                    SwitchPoint = 0.5f
                },
                Scripts = new()
                {
                    ControlNet = new() { new(), new(), new() },
                    Cutoff = new(),
                    DynamicPrompts = new(),
                    MultiDiffusionTiledDiffusion = new(),
                    MultiDiffusionTiledVae = new(),
                    RegionalPrompter = new(),
                    XYZPlot = new(),
                    ADetailer = new(),
                    Incantations = new()
                }
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
                InpaintingMaskInvert = 0,
                Scripts = new()
                {
                    ControlNet = new() { new(), new(), new() },
                    Cutoff = new(),
                    DynamicPrompts = new(),
                    UltimateUpscale = new(),
                    MultiDiffusionTiledDiffusion = new(),
                    MultiDiffusionTiledVae = new(),
                    RegionalPrompter = new(),
                    XYZPlot = new(),
                    ADetailer = new(),
                    Incantations = new()
                }
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
                FrameInterpolation = new FrameInterpolationParameters
                {
                    IsActive = true,
                    ScaleBy = 2.0,
                    Multiplier = 2,
                    RifeModel = "rife49.pth"
                },
                Loras = new List<Lora>()
            };
        }
    }
}
