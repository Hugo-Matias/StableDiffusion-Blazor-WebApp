using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras }

    public class AppState
    {
        private readonly string _settingsFile = "BlazorDiffusion.json";
        private readonly SDAPIService _api;
        private readonly DatabaseService _db;
        private readonly IOService _io;
        private readonly IConfiguration _configuration;
        private bool _isConverging;
        private int _currentBrushSize;
        private string _currentBrushColor;
        private int _currentProgress;
        private bool _isWebuiUp;

        public event Action OnSDModelsChange;
        public event Action OnOptionsChange;
        public event Action OnStyleChange;
        public event Action OnConverging;
        public event Action OnBrushSizeChange;
        public event Action OnBrushColorChange;
        public event Action OnFolderChange;
        public event Action OnProjectsChange;
        public event Action OnProjectChange;
        public event Func<Task> OnProjectChangeTask;
        public event Action OnStateHasChanged;
        public event Action OnProgressChanged;
        public event Action OnDownloadCompleted;
        public event Action OnWebuiStateChanged;

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
        public Options Options { get; set; }
        public AppSettings Settings { get; set; }
        public Txt2ImgParameters ParametersTxt2Img { get; set; }
        public Img2ImgParameters ParametersImg2Img { get; set; }
        public UpscaleParameters ParametersUpscale { get; set; }
        public IEnumerable<PromptStyle> CurrentStyles { get; set; }
        public string CurrentSDModel { get; set; } = "Loading...";
        public string CurrentVae { get; set; }
        public int CurrentFolderId { get; set; }
        public string CurrentFolderName { get; set; }
        public int CurrentProjectId { get; set; }
        public string CurrentProjectName { get; set; }
        public string CurrentResourceSubType { get; set; }
        public long CurrentSeed { get; set; }
        public int CurrentProgress
        {
            get => _currentProgress; set
            {
                _currentProgress = value;
                OnProgressChanged?.Invoke();
            }
        }
        public int CurrentBrushSize
        {
            get => _currentBrushSize; set
            {
                _currentBrushSize = value;
                OnBrushSizeChange?.Invoke();
            }
        }
        public string CurrentBrushColor
        {
            get => _currentBrushColor; set
            {
                _currentBrushColor = value;
                OnBrushColorChange?.Invoke();
            }
        }
        public List<string> CanvasStates { get; set; } = new();
        public string CanvasImageData { get; set; }
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
        public bool? ResourceIsEnabledFilter { get; set; }
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

        public AppState(SDAPIService api, DatabaseService db, IOService io, IConfiguration configuration)
        {
            _api = api;
            _db = db;
            _io = io;
            _configuration = configuration;

            GetCmdFlags();
            LoadSettings();
            _db.PageSize = Settings.Gallery.PageSize;
            Settings.Gallery.DateRange = new(DateTime.Now.Date.AddDays(-5), DateTime.Now.Date);

            Images = new();
            Progress = new();
            Folders = new();
            Projects = new();
            CurrentBrushSize = Settings.Img2Img.Brush.DefaultValue;
            CurrentBrushColor = Settings.Img2Img.Brush.Color;

            GetButtonTags();
            ModeType[] modes = new ModeType[3] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Extras };
            InitializeParameters(modes);

            if (Settings.Folder > 0) SetCurrentFolder(Settings.Folder);
            if (Settings.Project > 0) SetCurrentProject(Settings.Project);
        }

        public void InitializeParameters(ModeType[] modes)
        {
            var defaultParameters = new SharedParameters()
            {
                Steps = Settings.Shared.Steps.DefaultValue,
                SamplerIndex = Settings.Shared.Sampler,
                Seed = Settings.Shared.Seed,
                CfgScale = Settings.Shared.CfgScale.DefaultValue,
                Width = Settings.Shared.Resolution.Width,
                Height = Settings.Shared.Resolution.Height,
                NIter = Settings.Shared.Batch.Count.DefaultValue,
                BatchSize = Settings.Shared.Batch.Size.DefaultValue,
                DenoisingStrength = Settings.Shared.Denoising.DefaultValue,
                RestoreFaces = Settings.Shared.FaceRestoration,
                Tiling = Settings.Shared.Tilling,
            };

            if (modes.Contains(ModeType.Txt2Img))
                ParametersTxt2Img = new Txt2ImgParameters(defaultParameters)
                {
                    EnableHR = Settings.Txt2Img.HighRes.Enabled,
                    FirstphaseWidth = Settings.Txt2Img.HighRes.FirstPass.Width,
                    FirstphaseHeight = Settings.Txt2Img.HighRes.FirstPass.Height,
                    HRUpscaler = Settings.Txt2Img.HighRes.Upscaler,
                    HRScale = Settings.Txt2Img.HighRes.Scale.DefaultValue,
                    HRWidth = Settings.Txt2Img.HighRes.Resolution.Width,
                    HRHeight = Settings.Txt2Img.HighRes.Resolution.Height,
                    HRSecondPassSteps = Settings.Txt2Img.HighRes.SecondPassSteps.DefaultValue,
                    Scripts = new()
                    {
                        ControlNet = new() { CreateControlNet() },
                        Cutoff = CreateCutoff(),
                        DynamicPrompts = CreateDynamicPrompts(),
                    }
                };

            if (modes.Contains(ModeType.Img2Img))
                ParametersImg2Img = new Img2ImgParameters(defaultParameters)
                {
                    MaskBlur = Settings.Img2Img.MaskBlur.DefaultValue,
                    ResizeMode = Settings.Img2Img.ResizeMode,
                    InpaintingFill = Settings.Img2Img.Inpainting.Fill,
                    InpaintFullRes = Settings.Img2Img.Inpainting.FullRes.DefaultValue,
                    InpaintFullResPadding = Settings.Img2Img.Inpainting.FullRes.Padding.DefaultValue,
                    InpaintingMaskInvert = Settings.Img2Img.Inpainting.MaskInvert,
                    Scripts = new()
                    {
                        ControlNet = new() { CreateControlNet() },
                        Cutoff = CreateCutoff(),
                        DynamicPrompts = CreateDynamicPrompts(),
                    }
                };

            if (modes.Contains(ModeType.Extras))
                ParametersUpscale = new UpscaleParameters(defaultParameters)
                {
                    ResizeMode = Settings.Upscale.ResizeMode,
                    ShowResults = Settings.Upscale.ShowResults,
                    GfpganVisibility = Settings.Upscale.FaceRestoration.GfpganVisibility,
                    CodeformerVisibility = Settings.Upscale.FaceRestoration.CodeformerVisibility,
                    CodeformerWeight = Settings.Upscale.FaceRestoration.CodeformerWeight,
                    UpscalingMultiplier = Settings.Upscale.UpscalingMultiplier.DefaultValue,
                    UpscalingWidth = Settings.Upscale.UpscalingResolution.Width,
                    UpscalingHeight = Settings.Upscale.UpscalingResolution.Height,
                    UpscalingCrop = Settings.Upscale.UpscalingResolution.CropToFit,
                    UpscalerPrimary = Settings.Upscale.UpscalerPrimary,
                    UpscalerSecondary = Settings.Upscale.UpscalerSecondary.Name,
                    UpscalerSecondaryVisibility = Settings.Upscale.UpscalerSecondary.DefaultValue,
                    UpscalePriority = Settings.Upscale.FaceRestoration.UpscaleBeforeRestoration
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
                MagicBlocklistRegex = Settings.Scripts.DynamicPrompts.PromptMagic.MagicBlocklistRegex
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
            CurrentSDModel = "Loading...";
            OnSDModelsChange?.Invoke();
            await _api.PostOptions(new() { SDModelCheckpoint = modelTitle });
            CurrentSDModel = modelTitle;
            OnSDModelsChange?.Invoke();
        }

        public async Task SetVae(string vae)
        {
            CurrentVae = vae;
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
            CurrentStyles = new List<PromptStyle>();
        }

        public async Task GetUpscalers()
        {
            Upscalers = new List<Upscaler>();
            List<string> builtInUpscalers = new() { "Latent", "Latent (antialiased)", "Latent (bicubic)", "Latent (bicubic antialiased)", "Latent (nearest)", "Latent (nearest-exact)" };
            foreach (var upscaler in builtInUpscalers)
            {
                Upscalers.Add(new Upscaler() { Name = upscaler, ModelName = upscaler, ModelPath = string.Empty, ModelUrl = string.Empty });
            }
            Upscalers.AddRange(await _api.GetUpscalers());
        }

        public async Task GetFolders()
        {
            Folders = await _db.GetFolders();
        }

        public async Task GetProjects()
        {
            Projects = await _db.GetProjects(CurrentFolderId);
            if (Settings.Gallery.GalleriesOrderDescending) Projects.Reverse();
            OnProjectsChange?.Invoke();
        }

        public void GetButtonTags() => ButtonTags = JsonSerializer.Deserialize<PromptButton>(_io.GetJsonAsString("Data/danbooru.json"), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        public async Task SetCurrentFolder(int id)
        {
            if (id > 0)
            {
                await GetFolders();
                CurrentFolderId = id;
                CurrentFolderName = Folders.FirstOrDefault(f => f.Id == id)!.Name;
            }
            else
            {
                CurrentFolderId = 0;
                CurrentFolderName = "All";
            }
            Settings.Folder = CurrentFolderId;
            SaveSettings();
            await GetProjects();
            OnFolderChange?.Invoke();
        }

        public async Task SetCurrentProject(int id)
        {
            await GetFolders();
            await GetProjects();
            CurrentProjectId = id;
            CurrentProjectName = Projects.FirstOrDefault(p => p.Id == id)?.Name;
            Settings.Project = CurrentProjectId;
            SaveSettings();
            OnProjectChange?.Invoke();
            OnProjectChangeTask?.Invoke();
        }

        public async Task GetSamplers() => Samplers = await _api.GetSamplers();

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
                        return CurrentSeed.ToString();
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
                        return CurrentSeed.ToString();
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

        public void InvokeDownloadComplete() => OnDownloadCompleted?.Invoke();

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

        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions() { WriteIndented = true });
            _io.SaveText(_settingsFile, json);
        }
    }
}
