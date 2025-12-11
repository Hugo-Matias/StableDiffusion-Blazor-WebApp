using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Dtos.WebUI;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using MudBlazor;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras, Img2VidSamples }

    public class ManagerService
    {
        private readonly SDAPIService _sdapi;
        private readonly DatabaseService _db;
        private readonly IOService _io;
        private readonly ProgressService _progress;
        private readonly IConfiguration _configuration;
        private readonly ComfyUIService _capi;
        private readonly WorkflowService _workflow;
        private readonly IStateService _state;
        private readonly IEventService _events;
        private readonly ISettingsService _settings;
        private int _currentProgress;
        private bool _isConverging;
        private bool _isWebuiUp;
        private string _canvasImageData;
        private string _img2VidInputImage;
        private string _img2ImgInputImage;
        private bool _isComfyUIUp;
        private ImageEditorState _imageEditorState = new();

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
        public List<SDModel> CheckpointModels { get; set; } = new();
        public List<SDModel> DiffusionModels { get; set; } = new();
        public List<string> SDVAEs { get; set; } = new();
        public List<string> ClipModels { get; set; } = new();
        public List<string> ClipVisionModels { get; set; } = new();
        public List<string> SDADetailerModels { get; set; } = new();
        public List<Models.Sampler> Samplers { get; set; }
        public List<Scheduler> Schedulers { get; set; }
        public List<PromptStyle> Styles { get; set; }
        public List<Upscaler> Upscalers { get; set; }
        public List<Folder>? Folders { get; set; }
        public List<Project>? Projects { get; set; }
        public List<int> SelectedImageIds { get; set; }
        public int CurrentProgress
        {
            get => _currentProgress; set
            {
                _currentProgress = value;
                OnProgressChanged?.Invoke();
            }
        }
        public List<string> CanvasStates { get; set; } = new();
        public string CanvasImageData
        {
            get => _canvasImageData; set
            {
                _canvasImageData = value;
                OnCanvasImageDataChanged?.Invoke();
            }
        }
        public string CanvasMaskData { get; set; }
        public string UpscaleImageData { get; set; }

        /// <summary>
        /// Input image data for Img2Vid generation (stored separately from parameters due to size)
        /// </summary>
        public string Img2VidInputImage
        {
            get => _img2VidInputImage;
            set
            {
                _img2VidInputImage = value;
                OnImg2VidInputImageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Input image data for Img2Img generation (stored separately from parameters due to size)
        /// </summary>
        public string Img2ImgInputImage
        {
            get => _img2ImgInputImage;
            set
            {
                _img2ImgInputImage = value;
                OnImg2ImgInputImageChanged?.Invoke();
            }
        }

        /// <summary>
        /// Image editor state for session-level persistence.
        /// Survives page navigation within the same browser session.
        /// </summary>
        public ImageEditorState ImageEditorState
        {
            get => _imageEditorState; set
            {
                _imageEditorState = value;
                OnImageEditorStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Resets the image editor state, clearing all edits and layers.
        /// Call this when the user manually changes the input image (not when editor outputs a result).
        /// </summary>
        public void ResetImageEditorState()
        {
            _imageEditorState = new ImageEditorState();
            OnImageEditorStateChanged?.Invoke();
        }

        /// <summary>
        /// Sets the Img2Img input image and optionally resets the editor state.
        /// Use resetEditorState=true when user is loading a new image (not from editor output).
        /// Use resetEditorState=false when setting from editor output.
        /// </summary>
        public void SetImg2ImgInputImage(string imageData, bool resetEditorState)
        {
            if (resetEditorState && _img2ImgInputImage != imageData)
            {
                _imageEditorState = new ImageEditorState();
            }
            _img2ImgInputImage = imageData;
            OnImg2ImgInputImageChanged?.Invoke();
        }

        /// <summary>
        /// Session-persisted generated videos for Img2Vid
        /// </summary>
        public GeneratedVideos SessionGeneratedVideos { get; set; } = new();

        public event Action OnSessionVideosChanged;

        public void AddSessionVideo(GeneratedVideo video)
        {
            SessionGeneratedVideos.Videos.Insert(0, video);
            OnSessionVideosChanged?.Invoke();
        }

        public void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
        {
            foreach (var video in videos)
            {
                SessionGeneratedVideos.Videos.Insert(0, video);
            }
            OnSessionVideosChanged?.Invoke();
        }

        public void ClearSessionVideos()
        {
            SessionGeneratedVideos.Videos.Clear();
            OnSessionVideosChanged?.Invoke();
        }

        public void InvokeSessionVideosChanged()
        {
            OnSessionVideosChanged?.Invoke();
        }

        public void RemoveSessionVideo(GeneratedVideo video)
        {
            SessionGeneratedVideos.Videos.Remove(video);
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
                OnConverging?.Invoke();
            }
        }
        public bool IsWebuiUp
        {
            get => _isWebuiUp;
            set
            {
                _isWebuiUp = value;
                OnWebuiStateChanged?.Invoke();
            }
        }
        public bool IsComfyUIUp
        {
            get => _isComfyUIUp;
            set
            {
                _isComfyUIUp = value;
                OnComfyUIStateChanged?.Invoke();
            }
        }

        public ManagerService(SDAPIService sdapi, DatabaseService db, IOService io, ProgressService progress, IConfiguration configuration, ComfyUIService capi, WorkflowService workflow, IStateService state, IEventService events, ISettingsService settings)
        {
            _sdapi = sdapi;
            _db = db;
            _io = io;
            _progress = progress;
            _configuration = configuration;
            _capi = capi;
            _workflow = workflow;
            _state = state;
            _events = events;
            _settings = settings;
            
            // SettingsService now handles loading settings automatically
            // Note: LoadState() must be called asynchronously after construction
            // The StateService already initializes with defaults in its constructor

            // Initialize to empty lists - these will be populated when the backend comes online
            Upscalers = new();
            Samplers = new();
            Schedulers = new();
            Images = new();
            Progress = new();
            Folders = new();
            Projects = new();
            SelectedImageIds = new();
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

        #region Script Initializers
        public ScriptParametersControlNet CreateControlNet()
        {
            return new ScriptParametersControlNet()
            {
                Preprocessor = Settings.Scripts.ControlNet.Preprocessor,
                Model = Settings.Scripts.ControlNet.Model,
                ResizeMode = Settings.Scripts.ControlNet.ResizeModes[1],
                Weight = Settings.Scripts.ControlNet.Weight.Value,
                //Guidance = Settings.Scripts.ControlNet.Guidance.Strenght,
                GuidanceStart = Settings.Scripts.ControlNet.Guidance.Start,
                GuidanceEnd = Settings.Scripts.ControlNet.Guidance.End,
                IsLowVRam = Settings.Scripts.ControlNet.IsLowVRam,
                ControlMode = Settings.Scripts.ControlNet.ControlModes[0],
            };
        }

        public ScriptParametersCutoff CreateCutoff()
        {
            return new ScriptParametersCutoff()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.Cutoff.IsEnabled,
                Targets = Settings.Scripts.Cutoff.Targets,
                Weight = Settings.Scripts.Cutoff.Weight.Value,
                DisableNegative = Settings.Scripts.Cutoff.DisableNegative,
                Strong = Settings.Scripts.Cutoff.Strong,
                Padding = Settings.Scripts.Cutoff.Padding,
                Interpolation = Settings.Scripts.Cutoff.Interpolation,
                Debug = Settings.Scripts.Cutoff.Debug,
            };
        }

        public ScriptParametersDynamicPrompts CreateDynamicPrompts()
        {
            return new ScriptParametersDynamicPrompts()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.DynamicPrompts.IsEnabled,
                IsCombinatorial = Settings.Scripts.DynamicPrompts.Combinatorial.IsEnabled,
                CombinatorialBatches = Settings.Scripts.DynamicPrompts.Combinatorial.Batches.Value,
                IsMagicPrompt = Settings.Scripts.DynamicPrompts.PromptMagic.IsEnabled,
                IsFeelingLucky = Settings.Scripts.DynamicPrompts.PromptMagic.IsFeelingLucky,
                IsAttentionGrabber = Settings.Scripts.DynamicPrompts.PromptMagic.AttentionGrabber.IsEnabled,
                MinAttention = Settings.Scripts.DynamicPrompts.PromptMagic.AttentionGrabber.ValueMin,
                MaxAttention = Settings.Scripts.DynamicPrompts.PromptMagic.AttentionGrabber.ValueMax,
                MagicPromptLength = Settings.Scripts.DynamicPrompts.PromptMagic.Length.Value,
                MagicPromptCreativity = Settings.Scripts.DynamicPrompts.PromptMagic.Creativity.Value,
                UseFixedSeed = Settings.Scripts.DynamicPrompts.UseFixedSeed,
                UnlinkSeedFromPrompt = Settings.Scripts.DynamicPrompts.UnlinkSeedFromPrompt,
                DisableNegativePrompt = Settings.Scripts.DynamicPrompts.DisableNegativePrompt,
                EnableJinjaTemplates = Settings.Scripts.DynamicPrompts.EnableJinjaTemplates,
                NoImageGeneration = Settings.Scripts.DynamicPrompts.NoImageGeneration,
                MaxGenerations = Settings.Scripts.DynamicPrompts.Combinatorial.MaxGenerations.Value,
                MagicModel = Settings.Scripts.DynamicPrompts.PromptMagic.MagicModelList[0],
                MagicBlocklistRegex = Settings.Scripts.DynamicPrompts.PromptMagic.MagicBlocklistRegex,
            };
        }

        public ScriptParametersUltimateUpscale CreateUltimateUpscale()
        {
            return new ScriptParametersUltimateUpscale()
            {
                IsAlwaysOn = false,
                TileWidth = Settings.Scripts.UltimateUpscale.TileResolution.Width,
                TileHeight = Settings.Scripts.UltimateUpscale.TileResolution.Heigth,
                MaskBlur = Settings.Scripts.UltimateUpscale.MaskBlur.Value,
                Padding = Settings.Scripts.UltimateUpscale.Padding.Value,
                SeamFixType = Settings.Scripts.UltimateUpscale.SeamFixType,
                SeamFixWidth = Settings.Scripts.UltimateUpscale.SeamFix.Width.Value,
                SeamFixDenoise = Settings.Scripts.UltimateUpscale.SeamFix.Denoise.Value,
                SeamFixPadding = Settings.Scripts.UltimateUpscale.SeamFix.Padding.Value,
                SeamFixMaskBlur = Settings.Scripts.UltimateUpscale.SeamFix.MaskBlur.Value,
                SaveSeamFixImage = Settings.Scripts.UltimateUpscale.SaveSeamFixImage,
                SaveUpscaledImage = Settings.Scripts.UltimateUpscale.SaveUpscaledImage,
                UpscalerIndex = Settings.Scripts.UltimateUpscale.UpscalerIndex,
                RedrawMode = Settings.Scripts.UltimateUpscale.RedrawMode,
                TargetSizeType = Settings.Scripts.UltimateUpscale.TargetSizeType,
                CustomWidth = Settings.Scripts.UltimateUpscale.TileResolution.Width,
                CustomHeight = Settings.Scripts.UltimateUpscale.TileResolution.Heigth,
                CustomScale = Settings.Scripts.UltimateUpscale.TargetScale.Value
            };
        }

        public ScriptParametersMultiDiffusionTiledDiffusion CreateMultiDiffusionTiledDiffusion()
        {
            var bboxControls = new List<ScriptParametersMultiDiffusionBBoxControl>();
            for (int i = 0; i < 8; i++)
            {
                bboxControls.Add(new() { BlendMode = "Background" });
            }
            return new ScriptParametersMultiDiffusionTiledDiffusion()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.MultiDiffusion.TiledDiffusion.IsEnabled,
                Method = Settings.Scripts.MultiDiffusion.TiledDiffusion.Methods[0],
                IsNoiseInverse = Settings.Scripts.MultiDiffusion.TiledDiffusion.NoiseInverse.IsEnabled,
                NoiseInverseSteps = Settings.Scripts.MultiDiffusion.TiledDiffusion.NoiseInverse.Steps.Value,
                NoiseInverseRetouch = Settings.Scripts.MultiDiffusion.TiledDiffusion.NoiseInverse.Retouch.Value,
                NoiseInverseRenoiseStrength = Settings.Scripts.MultiDiffusion.TiledDiffusion.NoiseInverse.Renoise.Strength.Value,
                NoiseInverseRenoiseKernel = Settings.Scripts.MultiDiffusion.TiledDiffusion.NoiseInverse.Renoise.Kernel.Value,
                OverwriteImageSize = Settings.Scripts.MultiDiffusion.TiledDiffusion.Image.OverwriteImageSize,
                KeepInputSize = Settings.Scripts.MultiDiffusion.TiledDiffusion.Image.KeepInputSize,
                ImageWidth = Settings.Scripts.MultiDiffusion.TiledDiffusion.Image.Resolution.Width,
                ImageHeight = Settings.Scripts.MultiDiffusion.TiledDiffusion.Image.Resolution.Height,
                TileWidth = Settings.Scripts.MultiDiffusion.TiledDiffusion.LatentTile.Resolution.Width,
                TileHeight = Settings.Scripts.MultiDiffusion.TiledDiffusion.LatentTile.Resolution.Height,
                Overlap = Settings.Scripts.MultiDiffusion.TiledDiffusion.LatentTile.Overlap.Value,
                TileBatchSize = Settings.Scripts.MultiDiffusion.TiledDiffusion.LatentTile.Batch.Value,
                UpscalerIndex = Settings.Scripts.MultiDiffusion.TiledDiffusion.UpscalerIndex,
                ScaleFactor = Settings.Scripts.MultiDiffusion.TiledDiffusion.Image.Scale.Value,
                ControlTensorCpu = Settings.Scripts.MultiDiffusion.TiledDiffusion.ControlTensorCpu,
                EnableBBoxControl = Settings.Scripts.MultiDiffusion.TiledDiffusion.EnableBBoxControl,
                DrawBackground = Settings.Scripts.MultiDiffusion.TiledDiffusion.DrawBackground,
                CasualLayers = Settings.Scripts.MultiDiffusion.TiledDiffusion.CasualLayers,
                // TODO: Implement BBox Regions
                BBoxControlStates = bboxControls
            };
        }

        public ScriptParametersMultiDiffusionTiledVae CreateMultiDiffusionTiledVae()
        {
            return new ScriptParametersMultiDiffusionTiledVae()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.MultiDiffusion.TiledVae.IsEnabled,
                VaeToGpu = Settings.Scripts.MultiDiffusion.TiledVae.VaeToGpu,
                FastDecoder = Settings.Scripts.MultiDiffusion.TiledVae.FastDecoder,
                FastEncoder = Settings.Scripts.MultiDiffusion.TiledVae.FastEncoder,
                ColorFix = Settings.Scripts.MultiDiffusion.TiledVae.ColorFix,
                EncoderTileSize = Settings.Scripts.MultiDiffusion.TiledVae.Encoder.Value,
                DecoderTileSize = Settings.Scripts.MultiDiffusion.TiledVae.Decoder.Value
            };
        }

        public ScriptParametersRegionalPrompter CreateRegionalPrompter()
        {
            return new ScriptParametersRegionalPrompter()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.RegionalPrompter.IsEnabled,
                IsDebug = Settings.Scripts.RegionalPrompter.IsDebug,
                //SelectedTab = "Matrix",  // Must be initialized on the model, otherwise the arg is passed empty
                MatrixMode = Settings.Scripts.RegionalPrompter.MatrixModes[0],
                MaskMode = string.Empty,
                PromptMode = string.Empty,
                DivideRatio = Settings.Scripts.RegionalPrompter.DivideRatio,
                BaseRatio = Settings.Scripts.RegionalPrompter.BaseRatio,
                UseBasePrompt = Settings.Scripts.RegionalPrompter.UseBasePrompt,
                UseCommonPrompt = Settings.Scripts.RegionalPrompter.UseCommonPrompt,
                UseNegativeCommonPrompt = Settings.Scripts.RegionalPrompter.UseNegativeCommonPrompt,
                GenerationMode = Settings.Scripts.RegionalPrompter.GenerationModes[0],
                DisableConvertAND = Settings.Scripts.RegionalPrompter.DisableConvertAND,
                LoraNegTeRatios = string.Empty,
                LoraNegURatios = string.Empty,
                PromptThreshold = 0,
                Polymask = string.Empty,
            };
        }

        public ScriptParametersXYZPlot CreateXYZPlot()
        {
            return new ScriptParametersXYZPlot()
            {
                IsAlwaysOn = false,
                XTypeIndex = 0,
                XValues = string.Empty,
                YTypeIndex = 0,
                YValues = string.Empty,
                ZTypeIndex = 0,
                ZValues = string.Empty,
                DrawLegend = Settings.Scripts.XYZPlot.DrawLegend,
                IncludeSubImages = Settings.Scripts.XYZPlot.IncludeSubImages,
                IncludeSubGrids = Settings.Scripts.XYZPlot.IncludeSubGrids,
                RandomSeed = Settings.Scripts.XYZPlot.RandomSeed,
                Margin = Settings.Scripts.XYZPlot.Margin.Value,
            };
        }

        public ScriptParametersADetailer CreateADetailer()
        {
            return new ScriptParametersADetailer()
            {
                IsEnabled = Settings.Scripts.ADetailer.IsEnabled,
                IsAlwaysOn = true,
                SkipImg2Img = false,
                Model1 = CreateADetailerModel(Settings.Scripts.ADetailer.Models[0]),
                Model2 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
                Model3 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
                Model4 = CreateADetailerModel(Settings.Scripts.ADetailer.Model),
                Model5 = CreateADetailerModel(Settings.Scripts.ADetailer.Model)
            };
        }

        public ScriptParametersADetailerModel CreateADetailerModel(string model)
        {
            return new ScriptParametersADetailerModel()
            {
                Model = model,
                Prompt = Settings.Scripts.ADetailer.Prompt,
                NegativePrompt = Settings.Scripts.ADetailer.NegativePrompt,
                Confidence = Settings.Scripts.ADetailer.Confidence.Value,
                MaskKLargest = Settings.Scripts.ADetailer.MaskKLargest.Value,
                MaskMinRatio = Settings.Scripts.ADetailer.MaskRatio.ValueMin,
                MaskMaxRatio = Settings.Scripts.ADetailer.MaskRatio.ValueMax,
                DilateErode = Settings.Scripts.ADetailer.MaskErosionDilation.Value,
                XOffset = Settings.Scripts.ADetailer.MaskOffset.ValueX,
                YOffset = Settings.Scripts.ADetailer.MaskOffset.ValueY,
                MaskMergeInvert = Settings.Scripts.ADetailer.MaskMergeModes[0],
                MaskBlur = Settings.Scripts.ADetailer.MaskBlur.Value,
                DenoisingStrength = Settings.Scripts.ADetailer.DenoisingStrength.Value,
                InpaintOnlyMasked = Settings.Scripts.ADetailer.InpaintOnlyMasked,
                InpaintOnlyMaskedPadding = Settings.Scripts.ADetailer.InpaintMaskedPadding.Value,
                UseInpaintWidthHeight = Settings.Scripts.ADetailer.UseInpaintWidthHeight,
                InpaintWidth = Settings.Generation.Shared.Resolution.Width,
                InpaintHeight = Settings.Generation.Shared.Resolution.Height,
                UseSteps = Settings.Scripts.ADetailer.UseSteps,
                Steps = Settings.Generation.Shared.Steps.Value,
                UseCFGScale = Settings.Scripts.ADetailer.UseCFGScale,
                CFGScale = Settings.Generation.Shared.CfgScale.Value,
                UseCheckpoint = Settings.Scripts.ADetailer.UseCheckpoint,
                Checkpoint = Settings.Scripts.ADetailer.Checkpoint,
                UseVAE = Settings.Scripts.ADetailer.UseVAE,
                VAE = Settings.Scripts.ADetailer.VAE,
                UseSampler = Settings.Scripts.ADetailer.UseSampler,
                Sampler = Settings.Scripts.ADetailer.Sampler,
                UseNoiseMultiplier = Settings.Scripts.ADetailer.UseNoiseMultiplier,
                NoiseMultiplier = Settings.Scripts.ADetailer.NoiseMultiplier.Value,
                UseClipSkip = Settings.Scripts.ADetailer.UseClipSkip,
                ClipSkip = Settings.Webui.ClipSkip.Value,
                RestoreFace = Settings.Scripts.ADetailer.RestoreFace,
                ControlNetModel = Settings.Scripts.ADetailer.ControlNetModel,
                ControlNetModule = Settings.Scripts.ADetailer.ControlNetModule,
                ControlNetWeight = Settings.Scripts.ADetailer.ControlNetWeight,
                ControlNetGuidanceStart = Settings.Scripts.ControlNet.Guidance.Start,
                ControlNetGuidanceEnd = Settings.Scripts.ControlNet.Guidance.End,
                // TODO: create Settings
                Seed = Settings.Generation.Shared.Seed,
                Loras = [],
                UseScheduler = false,
                Scheduler = "simple",
                DropSize = 50,
                GuideSize = 1024,
                MaxSize = 2048,
                BBoxDilation = 100,
                BBoxCropFactor = 3.5,
                Cycle = 1,
            };
        }

        public ScriptParametersIncantations CreateIncantationsModel()
        {
            return new ScriptParametersIncantations()
            {
                IsAlwaysOn = true,
                IsEnabled = Settings.Scripts.Incantations.IsEnabled,
                IsPAGEnabled = Settings.Scripts.Incantations.PAG.IsPAGEnabled,
                PAGScale = Settings.Scripts.Incantations.PAG.Value,
                IsMultiConceptEnabled = Settings.Scripts.Incantations.MultiConcept.IsMultiConceptEnabled,
                UNK1 = Settings.Scripts.Incantations.MultiConcept.UNK1,
                CorrectionSize = Settings.Scripts.Incantations.MultiConcept.CorrectionSize.Value,
                SuppresionAlpha = Settings.Scripts.Incantations.MultiConcept.SuppresionAlpha.Value,
                CbSScoreThreshold = Settings.Scripts.Incantations.MultiConcept.CbSScoreThreshold.Value,
                CbSCorrectionStrength = Settings.Scripts.Incantations.MultiConcept.CbSCorrectionStrength.Value,
                UNK2 = Settings.Scripts.Incantations.MultiConcept.UNK2,
                EMAFactor = Settings.Scripts.Incantations.MultiConcept.EMAFactor.Value,
                StepEnd = Settings.Scripts.Incantations.MultiConcept.StepEnd.Value,
                IsSeekEnabled = Settings.Scripts.Incantations.Seek.IsSeekEnabled,
                AppendGenCaption = Settings.Scripts.Incantations.Seek.AppendGenCaption,
                DeepbooruInterrogate = Settings.Scripts.Incantations.Seek.DeepbooruInterrogate,
                Delimiter = Settings.Scripts.Incantations.Seek.Delimiter,
                WordReplacement = Settings.Scripts.Incantations.Seek.WordReplacement,
                Gamma = Settings.Scripts.Incantations.Seek.Gamma.Value,
                UNK3 = Settings.Scripts.Incantations.Seek.UNK3
            };
        }
        #endregion

        /// <summary>
        /// Loads models based on the current workflow's asset requirements.
        /// For WebUI (deprecated): loads all models as CheckpointModels.
        /// For ComfyUI: loads models based on workflow asset types (Checkpoint, Diffusion, VAE, CLIP, etc.)
        /// </summary>
        public async Task GetWorkflowModels(bool refresh = false)
        {
            if (IsWebuiUp)
            {
                if (refresh) await _sdapi.PostRefreshModels();
                // WebUI only supports checkpoint models
                CheckpointModels = await _sdapi.GetSDModels();
                CheckpointModels = CheckpointModels?.OrderBy(m => m.Model_name).ToList() ?? new List<SDModel>();
                OnSDModelsChange?.Invoke();
                return;
            }

            if (!IsComfyUIUp)
                return;

            var currentWorkflow = GetCurrentWorkflow();
            if (currentWorkflow?.Assets == null || currentWorkflow.Assets.Count == 0)
            {
                // No assets defined, load checkpoints as fallback
                CheckpointModels = await _capi.GetCheckpoints();
                OnSDModelsChange?.Invoke();
                return;
            }

            // Load models based on asset types defined in workflow
            var assetTypes = currentWorkflow.Assets.Select(a => a.Type).Distinct().ToList();

            foreach (var assetType in assetTypes)
            {
                switch (assetType)
                {
                    case AssetType.CheckpointModel:
                        CheckpointModels = await _capi.GetCheckpoints();
                        break;

                    case AssetType.DiffusionModel:
                        DiffusionModels = await _capi.GetDiffusionModels();
                        break;

                    case AssetType.Vae:
                        SDVAEs = await _capi.GetVAEs();
                        break;

                    case AssetType.Clip:
                        ClipModels = await _capi.GetClipModels();
                        break;

                    case AssetType.ClipVision:
                        ClipVisionModels = await _capi.GetClipVisionModels();
                        break;
                }
            }

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

        public async Task GetSDVAEs()
        {
            if (IsWebuiUp)
            {
                if (CmdFlags == null) await GetCmdFlags();
                var vaeDir = string.IsNullOrWhiteSpace(CmdFlags.VaeDir) ? Path.Join(CmdFlags.BaseDir, @"models/VAE") : CmdFlags.VaeDir;
                SDVAEs = _io.GetFilesRecursive(vaeDir).Select(f => f.Name).ToList();
            }
            else if (IsComfyUIUp) SDVAEs = await _capi.GetVAEs();
        }

        public async Task GetSDADetailerModels()
        {
            if (IsWebuiUp)
            {
                var modelsDir = Path.Join(CmdFlags.BaseDir, @"models/adetailer");
                SDADetailerModels = _io.GetFilesRecursive(modelsDir).Select(f => f.Name).ToList();
            }
            else if (IsComfyUIUp) SDADetailerModels = await _capi.GetBBoxDetailers();
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
            if (IsWebuiUp) Options = await _sdapi.GetOptions();
            if (IsComfyUIUp) Options = await _capi.GenerateOptions();
            OnOptionsChange?.Invoke();
        }

        public async Task GetStyles()
        {
            Styles = IsWebuiUp ? await _sdapi.GetStyles() : new();
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

        public async Task GetUpscalers()
        {
            if (IsWebuiUp) Upscalers = await _sdapi.GetUpscalers();
            else if (IsComfyUIUp) Upscalers = await _capi.GetUpscalers();
            else Upscalers = new();
        }

        public async Task GetFolders()
        {
            Folders = await _db.GetFolders();
        }

        public async Task GetProjects()
        {
            Projects = await _db.GetProjects(State.Gallery.FolderId);
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
            State.Gallery.ProjectId = id;
            State.Gallery.ProjectName = Projects.FirstOrDefault(p => p.Id == id)?.Name;
            SaveState();
            OnProjectChange?.Invoke();
            OnProjectChangeTask?.Invoke();
        }

        public async Task GetSamplers()
        {
            if (IsWebuiUp) Samplers = await _sdapi.GetSamplers();
            else if (IsComfyUIUp) Samplers = await _capi.GetSamplers();
            else Samplers = new();
            OnSamplersSchedulersChanged?.Invoke();
        }

        public async Task GetSchedulers()
        {
            if (IsWebuiUp) Schedulers = await _sdapi.GetSchedulers();
            else if (IsComfyUIUp) Schedulers = await _capi.GetSchedulers();
            else Schedulers = new();
            OnSamplersSchedulersChanged?.Invoke();
        }

        /// <summary>
        /// Loads all backend-dependent resources (samplers, schedulers, upscalers).
        /// Call this method when the backend comes online.
        /// </summary>
        public async Task LoadBackendDependentResources()
        {
            await GetSamplers();
            await GetSchedulers();
            await GetUpscalers();
        }

        public string GetDynamicPromptsVersion()
        {
            if (!IsWebuiUp) return string.Empty;
            var scriptFile = Path.Combine(CmdFlags.BaseDir, "extensions", "sd-dynamic-prompts", "sd_dynamic_prompts", "__init__.py");
            foreach (var line in _io.LoadTextLines(scriptFile))
            {
                if (line.StartsWith("__version__ = "))
                {
                    var version = line.Split(" = ", 2)[1].Replace("\"", "").Trim();
                    return $"Dynamic Prompts v{version}";
                }
            }
            return string.Empty;
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
            
            OnWorkflowBaseChanged?.Invoke();
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
            if (IsWebuiUp)
            {
                if (CmdFlags == null) await GetCmdFlags();
                var baseDir = CmdFlags.BaseDir;
                var checkpointDir = string.IsNullOrWhiteSpace(CmdFlags.CkptDir) ? Path.Join(baseDir, @"models/Stable-diffusion") : CmdFlags.CkptDir;
                var embeddingDir = string.IsNullOrWhiteSpace(CmdFlags.EmbeddingDir) ? Path.Join(baseDir, "embeddings") : CmdFlags.EmbeddingDir;
                var hypernetDir = string.IsNullOrWhiteSpace(CmdFlags.HypernetworkDir) ? Path.Join(baseDir, @"models/hypernetworks") : CmdFlags.HypernetworkDir;
                var loraDir = string.IsNullOrWhiteSpace(CmdFlags.LoraDir) ? Path.Join(baseDir, @"models/Lora") : CmdFlags.LoraDir;
                var vaeDir = string.IsNullOrWhiteSpace(CmdFlags.VaeDir) ? Path.Join(baseDir, @"models/VAE") : CmdFlags.VaeDir;
                ResourceTypeDirectories = new()
                {
                    {"Checkpoint", checkpointDir},
                    {"TextualInversion", embeddingDir},
                    {"Hypernetwork", hypernetDir},
                    {"LORA", loraDir},
                    {"LoCon", loraDir},
                    {"VAE", vaeDir}
                };
            }
            else
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
        }

        public async Task<string> PostOptions(Options options)
        {
            var response = await _sdapi.PostOptions(options);
            await GetOptions();
            return response;
        }

        public void SerializeInfo()
        {
            if (IsWebuiUp) ImagesInfo = JsonSerializer.Deserialize<GeneratedImagesInfo>(Images.Info);
            if (IsComfyUIUp) ImagesInfo = new() { InfoTexts = new[] { Images.Info } };
        }

        public async Task GetCmdFlags() => CmdFlags = await _sdapi.GetCmdFlags();

        public void ReplaceSelectedImages(List<int> ids)
        {
            ClearSelectedImages();
            SelectedImageIds = ids;
            OnSelectedImagesChanged?.Invoke();
        }

        public void AddSelectedImage(int id)
        {
            SelectedImageIds.Add(id);
            OnSelectedImagesChanged?.Invoke();
        }

        public void RemoveSelectedImage(int id)
        {
            SelectedImageIds.Remove(id);
            OnSelectedImagesChanged?.Invoke();
        }

        public void ClearSelectedImages()
        {
            SelectedImageIds.Clear();
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
            await _state.LoadState();

            if (state != null)
            {
                // Handle custom state loading if provided
                await _state.SaveState();
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
                AssetType.CheckpointModel => (CheckpointModels ?? await _capi.GetCheckpoints())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                AssetType.DiffusionModel => (DiffusionModels ?? await _capi.GetDiffusionModels())?.Select(m => m.Model_name).ToList() ?? new List<string>(),
                AssetType.Vae => SDVAEs ?? await _capi.GetVAEs(),
                AssetType.Clip => ClipModels ?? await _capi.GetClipModels(),
                AssetType.ClipVision => ClipVisionModels ?? await _capi.GetClipVisionModels(),
                _ => new List<string>()
            };
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
        /// Sets the current model name based on mode using WorkflowAssets.
        /// </summary>
        public async Task SetCurrentModel(string modelTitle, ModeType? mode = null)
        {
            if (IsWebuiUp)
            {
                var progressBar = new BaseProgress() { BarColor = MudBlazor.Color.Info, IsIndeterminate = true };
                _progress.Add(progressBar);
                await _sdapi.PostOptions(new() { SDModelCheckpoint = modelTitle });
                _progress.Remove(progressBar.Id);
            }

            if (IsComfyUIUp)
            {
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
            }

            // Set model using WorkflowAssets
            var modelKey = mode == ModeType.Img2Vid ? "HighModel" : "Model";
            SetWorkflowAsset(modelKey, modelTitle, mode);

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
            if (IsWebuiUp)
            {
                await _sdapi.PostOptions(new() { SDVae = vae });
            }

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
