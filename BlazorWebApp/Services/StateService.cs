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

        public AppState State { get; private set; }
        public Txt2ImgParameters ParametersTxt2Img { get; private set; }
        public Img2ImgParameters ParametersImg2Img { get; private set; }
        public Models.UpscaleParameters ParametersUpscale { get; private set; }
        public Img2VidParameters ParametersImg2Vid { get; private set; }

        public StateService(
            IStateDatabaseService db,
            IConfiguration configuration,
            IEventService events)
        {
            _db = db;
            _configuration = configuration;
            _events = events;

            // Initialize with defaults - will be replaced by LoadState if needed
            // TODO: Phase 3 will inject SettingsService for proper defaults
            State = new AppState(new AppSettings());
            
            ModeType[] modes = new ModeType[4] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Extras, ModeType.Img2Vid };
            InitializeParameters(modes);
        }

        public void InitializeParameters(ModeType[] modes)
        {
            // Note: Settings dependency will be injected in Phase 3
            // For now, we use default values
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
            var state = await _db.GetState(1);

            if (state != null && state.AppState != null)
            {
                State = state.AppState;
                State.Generation.IsInterrupted = false;

                if (state.Txt2ImgParameters != null)
                    ParametersTxt2Img = state.Txt2ImgParameters;

                if (state.Img2ImgParameters != null)
                    ParametersImg2Img = state.Img2ImgParameters;

                if (state.UpscaleParameters != null)
                    ParametersUpscale = state.UpscaleParameters;

                if (state.Img2VidParameters != null)
                    ParametersImg2Vid = state.Img2VidParameters;

                NormalizeState();
                PublishStateChangedEvents();
            }
            else
            {
                // Initialize with defaults if no state exists
                await SaveState();
            }
        }

        public async Task SaveState()
        {
            NormalizeState();

            var entity = await _db.GetState(1);
            if (entity == null)
            {
                entity = new State
                {
                    Title = "AutoSave",
                    CreationDate = DateTime.Now,
                    Version = int.Parse(_configuration["StateVersion"])
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
            // TODO: Phase 3 will inject SettingsService here
            // For now, use hardcoded defaults
            return new SharedParameters
            {
                Comfy = new() { Workflow = new() },
                Loras = new(),
                Steps = 20,
                SamplerIndex = "Euler",
                Seed = -1,
                CfgScale = 7.0f,
                DistilledCfgScale = 7.0f,
                Width = 512,
                Height = 512,
                NIter = 1,
                BatchSize = 1,
                DenoisingStrength = 0.7f,
                RestoreFaces = false,
                Tiling = false
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
