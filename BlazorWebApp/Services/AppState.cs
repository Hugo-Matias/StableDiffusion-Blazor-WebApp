﻿using BlazorWebApp.Data.Dtos;
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
        private bool _isConverging;
        private int _currentBrushSize;
        private string _currentBrushColor;

        public event Action OnSDModelsChange;
        public event Action OnOptionsChange;
        public event Action OnStyleChange;
        public event Action OnConverging;
        public event Action OnBrushSizeChange;
        public event Action OnBrushColorChange;
        public event Action OnProjectChange;
        public event Func<Task> OnProjectChangeTask;
        public event Action OnStateHasChanged;

        public GeneratedImages Images { get; set; }
        public GeneratedImagesInfo ImagesInfo { get; set; }
        public ImagesDto GeneratedImageEntities { get; set; }
        public string? GridImage { get; set; }
        public Progress Progress { get; set; }
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
        public long? CurrentSeed { get; set; }
        public int CurrentFolderId { get; set; }
        public string CurrentFolderName { get; set; }
        public int CurrentProjectId { get; set; }
        public string CurrentProjectName { get; set; }
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
        public UpscaledImageDto GeneratedUpscaleImage { get; set; }
        public bool IsGalleryFiltered { get; set; }
        public List<CsvTag> CsvTags { get; set; }
        public PromptButton RootButtonTag { get; set; }
        public List<int> TagAccordionIds { get; set; }

        public string? Style1 { get; set; }
        public string? Style2 { get; set; }
        public bool IsConverging
        {
            get => _isConverging;
            set
            {
                _isConverging = value;
                OnConverging?.Invoke();
            }
        }

        public AppState(SDAPIService api, DatabaseService db, IOService io)
        {
            _api = api;
            _db = db;
            _io = io;

            LoadSettings();
            _db.PageSize = Settings.Gallery.PageSize;

            Images = new();
            Progress = new();
            Folders = new();
            Projects = new();
            TagAccordionIds = new();
            CurrentBrushSize = Settings.Img2Img.BrushSetttings.DefaultValue;
            CurrentBrushColor = Settings.Img2Img.BrushSetttings.Color;

            GetCsvTags();
            GetButtonTags();
            CreateParameters();
        }

        private void CreateParameters()
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

            ParametersTxt2Img = new Txt2ImgParameters(defaultParameters)
            {
                EnableHR = Settings.Txt2Img.HighRes.Enabled,
                FirstphaseWidth = Settings.Txt2Img.HighRes.FirstPass.Width,
                FirstphaseHeight = Settings.Txt2Img.HighRes.FirstPass.Height,
            };

            ParametersImg2Img = new Img2ImgParameters(defaultParameters)
            {
                MaskBlur = Settings.Img2Img.MaskBlurSettings.DefaultValue,
                ResizeMode = Settings.Img2Img.ResizeMode,
                InpaintingFill = Settings.Img2Img.Inpainting.Fill,
                InpaintFullRes = Settings.Img2Img.Inpainting.FullRes.DefaultValue,
                InpaintFullResPadding = Settings.Img2Img.Inpainting.FullRes.Padding.DefaultValue,
                InpaintingMaskInvert = Settings.Img2Img.Inpainting.MaskInvert,
            };

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

        public async Task GetSDModels()
        {
            SDModels = await _api.GetSDModels();
            SDModels = SDModels.OrderBy(m => m.Model_name).ToList();

            OnSDModelsChange?.Invoke();
        }

        public async Task GetOptions()
        {
            Options = await _api.GetOptions();

            OnOptionsChange?.Invoke();
        }

        public async Task GetStyles()
        {
            Styles = await _api.GetStyles();

            Style1 = Styles[0].Name;
            Style2 = Styles[0].Name;
        }

        public async Task GetUpscalers()
        {
            Upscalers = await _api.GetUpscalers();
        }

        public async Task GetFolders()
        {
            Folders = await _db.GetFolders();
        }

        public async Task GetProjects(int folderId = 0)
        {
            Projects = await _db.GetProjects(folderId);
            if (Settings.Gallery.GalleriesOrderDescending) Projects.Reverse();
        }

        public void GetCsvTags(bool readAll = false)
        {
            var path = @"E:\Programas\Stable Diffusion\stable-diffusion-webui\extensions\a1111-sd-webui-tagcomplete\tags";
            if (readAll)
            {
                var files = _io.GetFilesFromPath(path);
                CsvTags = new();
                if (files == null) return;
                foreach (var file in files)
                {
                    if (file.Extension.ToLower().Contains("csv"))
                        CsvTags.AddRange(Parser.ParseCsvTags(file.FullName));
                }
            }
            else CsvTags = Parser.ParseCsvTags(path + @"\danbooru.csv");
        }

        public void GetButtonTags() => RootButtonTag = JsonSerializer.Deserialize<PromptButton>(_io.GetJsonAsString("Data/danbooru.json"), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

        public int GetTagAccordionId()
        {
            var id = 0;
            while (TagAccordionIds.Contains(id))
            {
                id = new Random().Next();
            }
            TagAccordionIds.Add(id);
            return id;
        }

        public async Task SetCurrentFolder(int id)
        {
            await GetFolders();
            CurrentFolderId = id;
            CurrentFolderName = Folders.FirstOrDefault(f => f.Id == id)!.Name;
        }

        public async Task SetCurrentProject(int id)
        {
            await GetProjects();
            CurrentProjectId = id;
            CurrentProjectName = Projects.FirstOrDefault(p => p.Id == id)!.Name;
            OnProjectChange?.Invoke();
            OnProjectChangeTask?.Invoke();
        }

        public void ResetStyles()
        {
            Style1 = "None";
            Style2 = "None";
            OnStyleChange?.Invoke();
        }

        public async Task GetSamplers() => Samplers = await _api.GetSamplers();

        public async Task LoadImageInfoParameters(Image image)
        {
            ParametersTxt2Img.Prompt = image.Prompt;
            ParametersTxt2Img.NegativePrompt = image.NegativePrompt;
            ParametersTxt2Img.SamplerIndex = await _db.GetSampler(image.SamplerId);
            ParametersTxt2Img.Steps = image.Steps;
            ParametersTxt2Img.Seed = image.Seed;
            ParametersTxt2Img.CfgScale = image.CfgScale;
            ParametersTxt2Img.Width = image.Width;
            ParametersTxt2Img.Height = image.Height;
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

            return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir));
        }

        public string ConvertPathPattern(string pattern)
        {
            var rg = new Regex(@"(\[.+?\])");

            return rg.Replace(pattern, (t) => ConvertPathTag(t.Value));
        }

        private string ConvertPathTag(string tag)
        {
            switch (tag)
            {
                case "[model_hash]":
                    return GetModelHash(Options.SDModelCheckpoint);

                case "[sampler]":
                    return ParametersTxt2Img.SamplerIndex;

                case "[seed]":
                    return CurrentSeed.ToString();

                case "[steps]":
                    return ParametersTxt2Img.Steps.ToString();

                case "[cfg]":
                    return ParametersTxt2Img.CfgScale.ToString();

                default:
                    return "";
            }
        }

        private string GetModelHash(string modelName)
        {
            foreach (var model in SDModels)
            {
                if (model.Title == modelName)
                {
                    return model.Hash;
                }
            }

            return string.Empty;
        }

        public async Task<string> PostOptions(Options options) => await _api.PostOptions(options);

        public void SerializeInfo() => ImagesInfo = JsonSerializer.Deserialize<GeneratedImagesInfo>(Images.Info);

        public void InvokeStateHasChanged() => OnStateHasChanged?.Invoke();

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
