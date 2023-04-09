using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using MudBlazor;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras }

    public class ManagerService
    {
        private readonly string _settingsFile = "BlazorDiffusion.json";
        private readonly SDAPIService _api;
        private readonly DatabaseService _db;
        private readonly IOService _io;
        private readonly ProgressService _progress;
        private readonly IConfiguration _configuration;
        private int _currentProgress;
        private bool _isConverging;
        private bool _isWebuiUp;
        private string _canvasImageData;

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
        public event Action OnAppStateChanged;
        public event Action OnTxt2ImgParametersChanged;
        public event Action OnImg2ImgParametersChanged;
        public event Action OnUpscaleParametersChanged;
        public event Action OnSelectedImagesChanged;
        public event Action OnRefreshImagesContainer;
        public event Action OnCanvasImageDataChanged;
        public event Action OnResourcesStateChanged;

        public AppState State { get; set; }
        public AppSettings Settings { get; set; }
        public Options Options { get; set; }
        public Txt2ImgParameters ParametersTxt2Img { get; set; }
        public Img2ImgParameters ParametersImg2Img { get; set; }
        public UpscaleParameters ParametersUpscale { get; set; }
        public GeneratedImages Images { get; set; }
        public GeneratedImagesInfo ImagesInfo { get; set; }
        public ImagesDto GeneratedImageEntities { get; set; }
        public string? GridImage { get; set; }
        public InferenceProgress Progress { get; set; }
        public List<SDModel> SDModels { get; set; }
        public List<Models.Sampler> Samplers { get; set; }
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
        public bool ControlNetEnabled { get; set; }
        public UpscaledImageDto GeneratedUpscaleImage { get; set; }
        public bool IsGalleryFiltered { get; set; }
        public PromptButton ButtonTags { get; set; }
        public CmdFlags CmdFlags { get; set; }
        public CivitaiModelsDto CivitaiModels { get; set; }
        public CivitaiCreatorsDto CivitaiCreators { get; set; }
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

        public ManagerService(SDAPIService api, DatabaseService db, IOService io, ProgressService progress, IConfiguration configuration)
        {
            _api = api;
            _db = db;
            _io = io;
            _progress = progress;
            _configuration = configuration;
            LoadSettings();
            LoadState();

            GetUpscalers();
            Images = new();
            Progress = new();
            Folders = new();
            Projects = new();
            SelectedImageIds = new();
            _db.PageSize = State.Gallery.PageSize;
            State.Gallery.DateRange = new(DateTime.Now.Date.AddDays(-5), DateTime.Now.Date);

            GetButtonTags();
            GetCmdFlags();
        }

        public void InvokeDownloadComplete() => OnDownloadCompleted?.Invoke();

        public void InvokeRefreshImagesContainer() => OnRefreshImagesContainer?.Invoke();

        public void InvokeResourcesStateChanged() => OnResourcesStateChanged?.Invoke();

        public void InvokeParametersChanged(bool isImg2Img)
        {
            if (isImg2Img) OnImg2ImgParametersChanged?.Invoke();
            else OnTxt2ImgParametersChanged?.Invoke();
        }

        public void InitializeParameters(ModeType[] modes)
        {
            var defaultParameters = new SharedParameters()
            {
                Steps = Settings.Generation.Shared.Steps.Value,
                SamplerIndex = Settings.Generation.Shared.Sampler,
                Seed = Settings.Generation.Shared.Seed,
                CfgScale = Settings.Generation.Shared.CfgScale.Value,
                Width = Settings.Generation.Shared.Resolution.Width,
                Height = Settings.Generation.Shared.Resolution.Height,
                NIter = Settings.Generation.Shared.Batch.Count.Value,
                BatchSize = Settings.Generation.Shared.Batch.Size.Value,
                DenoisingStrength = Settings.Generation.Shared.Denoising.Value,
                RestoreFaces = Settings.Generation.Shared.FaceRestoration,
                Tiling = Settings.Generation.Shared.Tilling,
            };

            if (modes.Contains(ModeType.Txt2Img))
                ParametersTxt2Img = new Txt2ImgParameters(defaultParameters)
                {
                    EnableHR = Settings.Generation.Txt2Img.HighRes.Enabled,
                    FirstphaseWidth = Settings.Generation.Txt2Img.HighRes.FirstPass.Width,
                    FirstphaseHeight = Settings.Generation.Txt2Img.HighRes.FirstPass.Height,
                    HRUpscaler = Settings.Generation.Txt2Img.HighRes.Upscaler,
                    HRScale = Settings.Generation.Txt2Img.HighRes.Scale.Value,
                    HRWidth = Settings.Generation.Txt2Img.HighRes.Resolution.Width,
                    HRHeight = Settings.Generation.Txt2Img.HighRes.Resolution.Height,
                    HRSecondPassSteps = Settings.Generation.Txt2Img.HighRes.SecondPassSteps.Value,
                    Scripts = new()
                    {
                        ControlNet = new() { CreateControlNet() },
                        Cutoff = CreateCutoff(),
                        DynamicPrompts = CreateDynamicPrompts(),
                        MultiDiffusionTiledDiffusion = CreateMultiDiffusionTiledDiffusion(),
                        MultiDiffusionTiledVae = CreateMultiDiffusionTiledVae(),
                        RegionalPrompter = CreateRegionalPrompter(),
                        XYZPlot = CreateXYZPlot(),
                    }
                };

            if (modes.Contains(ModeType.Img2Img))
                ParametersImg2Img = new Img2ImgParameters(defaultParameters)
                {
                    MaskBlur = Settings.Generation.Img2Img.MaskBlur.Value,
                    ResizeMode = Settings.Generation.Img2Img.ResizeMode,
                    InpaintingFill = Settings.Generation.Img2Img.Inpainting.Fill,
                    InpaintFullRes = Settings.Generation.Img2Img.Inpainting.FullRes.Value,
                    InpaintFullResPadding = Settings.Generation.Img2Img.Inpainting.FullRes.Padding.Value,
                    InpaintingMaskInvert = Settings.Generation.Img2Img.Inpainting.MaskInvert,
                    Scripts = new()
                    {
                        ControlNet = new() { CreateControlNet() },
                        Cutoff = CreateCutoff(),
                        DynamicPrompts = CreateDynamicPrompts(),
                        UltimateUpscale = CreateUltimateUpscale(),
                        MultiDiffusionTiledDiffusion = CreateMultiDiffusionTiledDiffusion(),
                        MultiDiffusionTiledVae = CreateMultiDiffusionTiledVae(),
                        RegionalPrompter = CreateRegionalPrompter(),
                        XYZPlot = CreateXYZPlot(),
                    }
                };

            if (modes.Contains(ModeType.Extras))
                ParametersUpscale = new UpscaleParameters(defaultParameters)
                {
                    ResizeMode = Settings.Generation.Upscale.ResizeMode,
                    ShowResults = Settings.Generation.Upscale.ShowResults,
                    GfpganVisibility = Settings.Generation.Upscale.FaceRestoration.GfpganVisibility,
                    CodeformerVisibility = Settings.Generation.Upscale.FaceRestoration.CodeformerVisibility,
                    CodeformerWeight = Settings.Generation.Upscale.FaceRestoration.CodeformerWeight,
                    UpscalingMultiplier = Settings.Generation.Upscale.UpscalingMultiplier.DefaultValue,
                    UpscalingWidth = Settings.Generation.Upscale.UpscalingResolution.Width,
                    UpscalingHeight = Settings.Generation.Upscale.UpscalingResolution.Height,
                    UpscalingCrop = Settings.Generation.Upscale.UpscalingResolution.CropToFit,
                    UpscalerPrimary = Settings.Generation.Upscale.UpscalerPrimary,
                    UpscalerSecondary = Settings.Generation.Upscale.UpscalerSecondary.Name,
                    UpscalerSecondaryVisibility = Settings.Generation.Upscale.UpscalerSecondary.DefaultValue,
                    UpscalePriority = Settings.Generation.Upscale.FaceRestoration.UpscaleBeforeRestoration
                };
        }

        public ScriptParametersControlNet CreateControlNet()
        {
            return new ScriptParametersControlNet()
            {
                Preprocessor = Settings.Scripts.ControlNet.Preprocessor,
                Model = Settings.Scripts.ControlNet.Model,
                ResizeMode = Settings.Scripts.ControlNet.ResizeMode,
                Weight = Settings.Scripts.ControlNet.Weight.Value,
                Guidance = Settings.Scripts.ControlNet.Guidance.Strenght,
                GuidanceStart = Settings.Scripts.ControlNet.Guidance.Start,
                GuidanceEnd = Settings.Scripts.ControlNet.Guidance.End,
                IsLowVRam = Settings.Scripts.ControlNet.IsLowVRam,
                IsGuessMode = Settings.Scripts.ControlNet.IsGuessMode,
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
                MagicBatchSize = Settings.Scripts.DynamicPrompts.PromptMagic.Batch.Value
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
                Mode = Settings.Scripts.RegionalPrompter.Modes[0],
                DivideRatio = Settings.Scripts.RegionalPrompter.DivideRatio,
                BaseRatio = Settings.Scripts.RegionalPrompter.BaseRatio,
                UseBasePrompt = Settings.Scripts.RegionalPrompter.UseBasePrompt,
                UseCommonPrompt = Settings.Scripts.RegionalPrompter.UseCommonPrompt,
                UseNegativeCommonPrompt = Settings.Scripts.RegionalPrompter.UseNegativeCommonPrompt,
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

        public async Task GetSDModels(bool refresh = false)
        {
            if (refresh) await _api.PostRefreshModels();
            SDModels = await _api.GetSDModels();
            SDModels = SDModels.OrderBy(m => m.Model_name).ToList();

            OnSDModelsChange?.Invoke();
        }

        public async Task SetSDModel(string modelTitle)
        {
            State.Generation.SDModel = "Loading...";
            OnSDModelsChange?.Invoke();
            var progressBar = new BaseProgress() { BarColor = MudBlazor.Color.Info, IsIndeterminate = true };
            _progress.Add(progressBar);
            await _api.PostOptions(new() { SDModelCheckpoint = modelTitle });
            _progress.Remove(progressBar.Id);
            State.Generation.SDModel = modelTitle;
            OnSDModelsChange?.Invoke();
            SaveState();
        }

        public async Task SetVae(string vae)
        {
            State.Generation.Vae = vae;
            await _api.PostOptions(new() { SDVae = vae });
        }

        public async Task GetOptions()
        {
            Options = await _api.GetOptions();
            OnOptionsChange?.Invoke();
        }

        public async Task GetStyles()
        {
            Styles = await _api.GetStyles();
            var promptResources = await _db.GetPrompts();
            foreach (var prompt in promptResources)
            {
                Styles.Add(new(prompt));
            }
            if (State.Generation.Styles == null) State.Generation.Styles = new List<PromptStyle>();
        }

        public async Task GetUpscalers()
        {
            Upscalers = await _api.GetUpscalers();
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

        public void GetButtonTags() => ButtonTags = JsonSerializer.Deserialize<PromptButton>(_io.GetJsonAsString("Data/danbooru.json"), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

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

        public async Task GetSamplers() => Samplers = await _api.GetSamplers();

        public string GetDynamicPromptsVersion()
        {
            var scriptFile = Path.Combine(CmdFlags.BaseDir, "extensions", "sd-dynamic-prompts", "sd_dynamic_prompts", "dynamic_prompting.py");
            foreach (var line in _io.LoadTextLines(scriptFile))
            {
                if (line.StartsWith("VERSION = "))
                {
                    var version = line.Split(" = ", 2)[1].Replace("\"", "").Trim();
                    return $"Dynamic Prompts v{version}";
                }
            }
            return string.Empty;
        }

        public async Task LoadImageInfoParameters(Image image, ModeType mode)
        {
            if (mode == ModeType.Img2Img)
            {
                ParametersImg2Img.Prompt = image.Prompt;
                ParametersImg2Img.NegativePrompt = image.NegativePrompt;
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
                ParametersTxt2Img.Prompt = image.Prompt;
                ParametersTxt2Img.NegativePrompt = image.NegativePrompt;
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

            return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir, Parser.ModeTypeFromOutdir((Outdir)outdir)));
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
                    return GetModelName(Options.SDModelCheckpoint);
                default:
                    break;
            }

            if (mode == ModeType.Txt2Img)
            {
                switch (tag)
                {
                    case "[sampler]":
                        return ParametersTxt2Img.SamplerIndex;
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

            return string.Empty;
        }

        private string GetModelHash(string modelName) => SDModels.FirstOrDefault(m => m.Title == modelName)?.Hash;

        private string GetModelName(string modelName) => SDModels.FirstOrDefault(m => m.Title == modelName)?.Model_name;

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
                    {"VAE", Path.Combine(baseDir, "VAE")}
                };
            }
        }

        public async Task<string> PostOptions(Options options)
        {
            var response = await _api.PostOptions(options);
            await GetOptions();
            return response;
        }

        public void SerializeInfo() => ImagesInfo = JsonSerializer.Deserialize<GeneratedImagesInfo>(Images.Info);

        public async Task GetCmdFlags() => CmdFlags = await _api.GetCmdFlags();

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
                    param.Prompt = source.Prompt;
                    break;
                case nameof(SharedParameters.NegativePrompt):
                    param.NegativePrompt = source.NegativePrompt;
                    break;
                case nameof(SharedParameters.SamplerIndex):
                    param.SamplerIndex = GetSampler();
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
            var json = _io.LoadText(_settingsFile);

            if (json != null)
            {
                Settings = new();
                Settings = JsonSerializer.Deserialize<AppSettings>(json);
                SaveSettings();
            }
            else { Settings = new(); SaveSettings(); }
        }

        public async Task LoadState(State? state = null)
        {
            if (state == null)
            {
                state = await _db.GetState(1);
                State = new(Settings);
                ModeType[] modes = new ModeType[3] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Extras };
                InitializeParameters(modes);
            }

            if (state != null)
            {
                if (state.AppState != null)
                {
                    State = state.AppState;
                    OnAppStateChanged?.Invoke();
                }
                if (state.Txt2ImgParameters != null)
                {
                    ParametersTxt2Img = state.Txt2ImgParameters;
                    OnTxt2ImgParametersChanged?.Invoke();
                }
                if (state.Img2ImgParameters != null)
                {
                    ParametersImg2Img = state.Img2ImgParameters;
                    OnImg2ImgParametersChanged?.Invoke();
                }
                if (state.UpscaleParameters != null)
                {
                    ParametersUpscale = state.UpscaleParameters;
                    OnUpscaleParametersChanged?.Invoke();
                }
            }
            else await SaveState();
        }

        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions() { WriteIndented = true });
            _io.SaveText(_settingsFile, json);
        }

        public async Task SaveState(State? state = null)
        {
            if (state == null)
            {
                var entity = await _db.GetState(1);
                if (entity == null) entity = new() { Title = "AutoSave", CreationDate = DateTime.Now, Version = int.Parse(_configuration["StateVersion"]) };
                entity.AppState = State;
                entity.Txt2ImgParameters = ParametersTxt2Img;
                entity.Img2ImgParameters = ParametersImg2Img;
                entity.UpscaleParameters = ParametersUpscale;
                await _db.UpdateState(entity);
            }
            else await _db.UpdateState(state);
        }
    }
}
