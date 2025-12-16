using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for orchestrating image and video generation workflows.
    /// Coordinates between API services, file I/O, database operations, and progress tracking.
    /// </summary>
    public class ImageService : IImageService
    {
        private readonly IIOService _io;
        private readonly IBackendService _backend;
        private readonly MagickService _magick;
        private readonly IDatabaseService _db;
        private readonly IProgressService _progress;
        private readonly IRouterService _router;
        private readonly ILogger<ImageService> _logger;
        private readonly IStateService _state;
        private readonly ISessionService _session;
        private readonly IModelService _models;
        private readonly IWildcardService _wildcardService;
        private PeriodicTimer? _timer;
        private SharedParameters _parsingParams;
        private Txt2ImgParameters _txt2imgParams;
        private Img2VidParameters _img2vidParams;
        private int _canvasSourceWidth;
        private int _canvasSourceHeight;
        private string _currentModel = string.Empty;

        /// <summary>
        /// Event fired when image generation state changes.
        /// </summary>
        public event Action OnChange;

        #region Generation Results

        /// <summary>
        /// Raw generated images from the backend (base64 encoded).
        /// Contains Images list and Info (workflow JSON from ComfyUI).
        /// </summary>
        public GeneratedImages Images { get; private set; } = new();

        /// <summary>
        /// Generated image entities saved to database.
        /// </summary>
        public ImagesDto GeneratedImageEntities { get; set; }

        /// <summary>
        /// Last generated video result.
        /// </summary>
        public GeneratedVideos GeneratedVideos { get; private set; }

        /// <summary>
        /// Current inference progress. Can be set by websocket service.
        /// </summary>
        public InferenceProgress Progress { get; set; } = new();

        #endregion

        public ImageService(
            IIOService io, 
            IBackendService backend, 
            MagickService magick, 
            IDatabaseService db, 
            IProgressService progress, 
            IRouterService router, 
            ILogger<ImageService> logger, 
            IStateService state, 
            ISessionService session, 
            IModelService models,
            IWildcardService wildcardService)
        {
            _io = io;
            _backend = backend;
            _magick = magick;
            _db = db;
            _progress = progress;
            _router = router;
            _logger = logger;
            _state = state;
            _session = session;
            _models = models;
            _wildcardService = wildcardService;
        }

        /// <summary>
        /// Generates images based on the specified mode (Txt2Img, Img2Img).
        /// Handles the full generation workflow including API calls, file saving, and database persistence.
        /// </summary>
        /// <param name="mode">The generation mode to use.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        public async Task<ImagesDto> GetImages(ModeType mode)
        {
            _logger.LogInformation("Starting image generation for mode: {Mode}", mode);
            _progress.IsConverging = true;

            ImagesDto images = new();
            string scriptName = string.Empty;
            _currentModel = _models.GetCurrentModel(mode);

            try
            {
                switch (mode)
                {
                    case ModeType.Img2Img:
                        _logger.LogDebug("Building Img2Img parameters");
                        var img2imgParams = await BuildImg2ImgParametersAsync(scriptName);
                        Images = await _router.PostImg2Img(img2imgParams);
                        break;

                    default:
                        _logger.LogDebug("Building Txt2Img parameters");
                        await BuildTxt2ImgParametersAsync(scriptName);
                        Images = await _router.PostTxt2Img(_txt2imgParams);
                        break;
                }

                if (_state.State.Generation.IsInterrupted)
                {
                    _logger.LogWarning("Generation was interrupted by user");
                    throw new Exception("Generation Canceled!");
                }

                if (_backend.OutputPaths.SaveSamples)
                {
                    switch (mode)
                    {
                        case ModeType.Img2Img:
                            images = await SaveImages(Outdir.Img2ImgSamples, scriptName);
                            break;
                        default:
                            images = await SaveImages(Outdir.Txt2ImgSamples, scriptName);
                            break;
                    }
                }

                _logger.LogInformation("Image generation completed successfully for mode: {Mode}, generated {ImageCount} images", mode, images?.Images?.Count ?? 0);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred during image generation for mode: {Mode}", mode);
            }

            _progress.IsConverging = false;
            _state.State.Generation.IsInterrupted = false;

            NotifyStateChanged();
            return images;
        }

        /// <summary>
        /// Generates a video from an image using Img2Vid parameters
        /// </summary>
        public async Task<GeneratedVideos> GetVideo()
        {
            _progress.IsConverging = true;
            GeneratedVideos = null;
            _currentModel = _models.GetCurrentModel(ModeType.Img2Vid);

            try
            {
                _img2vidParams = _state.ParametersImg2Vid;

                // Ensure seed is set
                if (_img2vidParams.Seed == -1)
                {
                    _img2vidParams.Seed = new Random().Next(0, int.MaxValue);
                }

                GeneratedVideos = await _router.PostImg2Vid(_img2vidParams);

                if (_state.State.Generation.IsInterrupted)
                {
                    throw new Exception("Generation Canceled!");
                }

                // Store the seed used for this generation
                _state.State.Generation.Seed = (long)_img2vidParams.Seed;

                if (_backend.OutputPaths.SaveSamples && GeneratedVideos?.Videos?.Count > 0)
                {
                    await SaveVideos(GeneratedVideos);
                }
            }
            catch (Exception e)
            {
                await Console.Out.WriteLineAsync($"Video generation error: {e}");
            }

            _progress.IsConverging = false;
            _state.State.Generation.IsInterrupted = false;

            NotifyStateChanged();
            return GeneratedVideos;
        }

        /// <summary>
        /// Saves generated videos to the output directory
        /// </summary>
        private async Task SaveVideos(GeneratedVideos videos)
        {
            var saveDir = _io.CreateDirectory(GetVideoSaveFolder());
            var fileIndex = GetVideoFileIndex(saveDir.FullName);

            foreach (var video in videos.Videos)
            {
                fileIndex++;
                var filename = GenerateVideoFilename(fileIndex);
                var videoPath = Path.Combine(saveDir.FullName, filename);

                // If video data is available as base64, decode and save
                if (!string.IsNullOrWhiteSpace(video.VideoData))
                {
                    var videoBytes = Convert.FromBase64String(video.VideoData);
                    await _io.SaveFileToDisk(videoPath, videoBytes);
                    video.FilePath = videoPath;
                }
                // If we have a source file path from ComfyUI, copy it
                else if (!string.IsNullOrWhiteSpace(video.FilePath) && File.Exists(video.FilePath))
                {
                    File.Copy(video.FilePath, videoPath, overwrite: true);
                    video.FilePath = videoPath;
                }

                // Update video metadata
                video.Filename = filename;
                video.Prompt = _img2vidParams.Prompt;
                video.NegativePrompt = _img2vidParams.NegativePrompt;
                video.Seed = (long)_img2vidParams.Seed;
                video.Steps = (int)_img2vidParams.Steps;
                video.CfgScale = (float)_img2vidParams.CfgScale;
                video.Sampler = _img2vidParams.SamplerName;
                video.Model = _currentModel;
                video.Width = (int)_img2vidParams.Width;
                video.Height = (int)_img2vidParams.Height;
                video.FrameCount = (int)_img2vidParams.Length;
                video.FrameRate = (int)_img2vidParams.FrameRate;
                video.Duration = (double)_img2vidParams.Length / (double)_img2vidParams.FrameRate;

                // Persist to database using Image entity
                await AddVideoToDb(video);
            }
        }

        /// <summary>
        /// Adds a generated video to the database using the Image entity
        /// </summary>
        private async Task<Image> AddVideoToDb(GeneratedVideo video)
        {
            var image = new Image
            {
                Path = video.FilePath,
                Prompt = video.Prompt,
                NegativePrompt = video.NegativePrompt,
                Seed = video.Seed,
                Width = video.Width,
                Height = video.Height,
                Steps = (int)_img2vidParams.Steps,
                CfgScale = (float)_img2vidParams.CfgScale,
                SamplerId = await _db.GetSamplerIdByName(_img2vidParams.SamplerName),
                Scheduler = _img2vidParams.Scheduler,
                ProjectId = _state.State.Gallery.ProjectId,
                ModeId = await _db.GetMode(ModeType.Img2Vid),
                Model = await _db.GetResourceByFilename(_currentModel)
            };

            return await _db.AddImage(image);
        }

        /// <summary>
        /// Gets the save folder for Img2Vid outputs
        /// </summary>
        private string GetVideoSaveFolder()
        {
            var basePath = _backend.GetOutputPath(Outdir.Img2VidSamples);

            // Apply directory pattern if configured
            var dirPattern = _backend.OutputPaths.DirectoryPattern;
            if (!string.IsNullOrWhiteSpace(dirPattern))
            {
                var subPath = ConvertPathPattern(dirPattern, ModeType.Img2Vid);
                basePath = Path.Combine(basePath, subPath).Replace('/', Path.DirectorySeparatorChar);
            }

            return basePath;
        }

        /// <summary>
        /// Gets the next file index for video files in the directory
        /// </summary>
        private int GetVideoFileIndex(string path)
        {
            if (!Directory.Exists(path)) return 0;

            var files = Directory.GetFiles(path, "*.mp4")
                .Concat(Directory.GetFiles(path, "*.webm"))
                .Concat(Directory.GetFiles(path, "*.gif"))
                .ToList();

            if (files.Count == 0) return 0;

            var maxIndex = 0;
            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                var match = Regex.Match(filename, @"^(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var index))
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }

            return maxIndex;
        }

        /// <summary>
        /// Generates a filename for the video based on parameters
        /// </summary>
        private string GenerateVideoFilename(int fileIndex)
        {
            var pattern = _backend.OutputPaths.FilenamePattern;
            var filename = $"{fileIndex.ToString().PadLeft(5, '0')}";

            if (!string.IsNullOrWhiteSpace(pattern))
            {
                filename += "-" + pattern
                    .Replace("[seed]", _img2vidParams.Seed.ToString())
                    .Replace("[steps]", _img2vidParams.Steps.ToString())
                    .Replace("[cfg]", _img2vidParams.CfgScale.ToString())
                    .Replace("[sampler]", _img2vidParams.SamplerName ?? "euler");
            }

            return filename + ".mp4";
        }

        private async Task BuildTxt2ImgParametersAsync(string scriptName)
        {
            _parsingParams = await Parser.ParseParametersAsync(
                new SharedParameters(_state.ParametersTxt2Img), 
                _state.State.Generation.Styles,
                _wildcardService);
            _txt2imgParams = new Txt2ImgParameters(_parsingParams);
            _txt2imgParams.EnableHR = _state.ParametersTxt2Img.EnableHR;
            if (_txt2imgParams.EnableHR != null && (bool)_txt2imgParams.EnableHR)
            {
                _txt2imgParams.FirstphaseWidth = _state.ParametersTxt2Img.Width;
                _txt2imgParams.FirstphaseHeight = _state.ParametersTxt2Img.Height;
                _txt2imgParams.HRUpscaler = _state.ParametersTxt2Img.HRUpscaler;
                _txt2imgParams.HRScale = _state.ParametersTxt2Img.HRScale;
                _txt2imgParams.HRWidth = _state.ParametersTxt2Img.HRWidth;
                _txt2imgParams.HRHeight = _state.ParametersTxt2Img.HRHeight;
                _txt2imgParams.HRSecondPassSteps = _state.ParametersTxt2Img.HRSecondPassSteps;
                _txt2imgParams.DenoisingStrength = _state.ParametersTxt2Img.DenoisingStrength;
            }
            _txt2imgParams.SeedVR2 = _state.ParametersTxt2Img.SeedVR2;
            _txt2imgParams.ConditioningVariation = _state.ParametersTxt2Img.ConditioningVariation;
        }

        private async Task<Img2ImgParameters> BuildImg2ImgParametersAsync(string scriptName)
        {
            _parsingParams = await Parser.ParseParametersAsync(
                new SharedParameters(_state.ParametersImg2Img), 
                _state.State.Generation.Styles,
                _wildcardService);
            var img2imgParams = new Img2ImgParameters(_parsingParams);
            img2imgParams.InitImages = _state.ParametersImg2Img.InitImages;
            img2imgParams.Mask = _state.ParametersImg2Img.Mask;
            img2imgParams.MaskBlur = _state.ParametersImg2Img.MaskBlur;
            img2imgParams.ResizeMode = _state.ParametersImg2Img.ResizeMode;
            img2imgParams.InpaintingFill = _state.ParametersImg2Img.InpaintingFill;
            img2imgParams.InpaintFullRes = _state.ParametersImg2Img.InpaintFullRes;
            img2imgParams.InpaintFullResPadding = _state.ParametersImg2Img.InpaintFullResPadding;
            img2imgParams.InpaintingMaskInvert = _state.ParametersImg2Img.InpaintingMaskInvert;
            SetSourceImageSize();
            return img2imgParams;
        }

        public async Task<ImagesDto> SaveImages(Outdir outdirSamples, string scriptName)
        {
            DirectoryInfo saveDir = _io.CreateDirectory(GetCurrentSaveFolder(outdirSamples));
            ImagesDto savedImages = new() { PageCount = 1, HasNext = false, HasPrev = false, CurrentPage = 1, Images = new() };

            var fileIndex = _io.GetFileIndex(saveDir.FullName, outdirSamples);
            var mode = Parser.ModeTypeFromOutdir(outdirSamples);

            // Parse info from the workflow JSON (Images.Info contains ComfyUI workflow data)
            var info = Parser.ParseInfoStrings(Images.Info, mode, _backend.IsBackendAvailable);

            for (int i = 0; i < Images.Images.Count; i++)
            {
                fileIndex++;
                var extension = _backend.OutputPaths.SamplesFormat.ToLowerInvariant();

                // Use seed from parameters (already set during generation)
                _state.State.Generation.Seed = (long)_parsingParams.Seed;

                var fullpath = GetImagePath(saveDir.FullName, fileIndex, mode);
                var imagePath = $"{fullpath}.{extension}";

                await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(Images.Images[i]));

                savedImages.Images.Add(await AddImageToDb(imagePath, outdirSamples, info));
            }

            return savedImages;
        }

        /// <summary>
        /// Gets the save folder for the specified output type.
        /// </summary>
        public string GetCurrentSaveFolder(Outdir? outdir)
        {
            if (outdir == null) return string.Empty;
            
            var basePath = _backend.GetOutputPath(outdir.Value);
            if (string.IsNullOrEmpty(basePath)) return string.Empty;
            
            // For Extras, don't add directory pattern
            if (outdir == Outdir.Extras) return basePath;

            var dirPattern = _backend.OutputPaths.DirectoryPattern;
            if (!string.IsNullOrWhiteSpace(dirPattern))
            {
                var subPath = ConvertPathPattern(dirPattern, Parser.ModeTypeFromOutdir(outdir.Value));
                basePath = Path.Combine(basePath, subPath).Replace('/', Path.DirectorySeparatorChar);
            }

            return basePath;
        }

        /// <summary>
        /// Converts a path pattern with placeholders to actual values.
        /// </summary>
        public string ConvertPathPattern(string pattern, ModeType mode)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return string.Empty;
            var rg = new Regex(@"(\[.+?\])");
            return rg.Replace(pattern, t => ConvertPathTag(t.Value, mode));
        }

        private string ConvertPathTag(string tag, ModeType mode)
        {
            if (tag == "[model_name]")
            {
                var modelAsPath = _models.GetCurrentModel(mode)?.Replace('/', Path.DirectorySeparatorChar) ?? "unknown";
                return Path.Combine(Path.GetDirectoryName(modelAsPath) ?? string.Empty, Path.GetFileNameWithoutExtension(modelAsPath));
            }

            return tag switch
            {
                "[sampler]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.SamplerName ?? "euler",
                    ModeType.Img2Img => _state.ParametersImg2Img?.SamplerIndex ?? "euler",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.SamplerName ?? "euler",
                    _ => "euler"
                },
                "[seed]" => _state.State.Generation.Seed.ToString(),
                "[steps]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.Steps?.ToString() ?? "20",
                    ModeType.Img2Img => _state.ParametersImg2Img?.Steps?.ToString() ?? "20",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.Steps?.ToString() ?? "8",
                    _ => "20"
                },
                "[cfg]" => mode switch
                {
                    ModeType.Txt2Img => _state.ParametersTxt2Img?.CfgScale?.ToString() ?? "7",
                    ModeType.Img2Img => _state.ParametersImg2Img?.CfgScale?.ToString() ?? "7",
                    ModeType.Img2Vid => _state.ParametersImg2Vid?.CfgScale?.ToString() ?? "1",
                    _ => "7"
                },
                _ => string.Empty
            };
        }

        private async Task<Image> AddImageToDb(string path, Outdir outdir, Dictionary<string, string> info)
        {
            Image image = new();

            image.Path = path;
            image.ProjectId = _state.State.Gallery.ProjectId;
            
            if (outdir == Outdir.Txt2ImgSamples && _txt2imgParams.EnableHR == true)
            {
                var resizeRes = Parser.ParseHighresResolution((int)_parsingParams.Width, (int)_parsingParams.Height, _txt2imgParams.HRWidth, _txt2imgParams.HRHeight, _txt2imgParams.HRScale);
                image.Width = resizeRes.Item1;
                image.Height = resizeRes.Item2;
            }
            else if (outdir == Outdir.Img2ImgSamples)
            {
                image.Width = _canvasSourceWidth;
                image.Height = _canvasSourceHeight;
            }
            else
            {
                image.Width = (int)_parsingParams.Width;
                image.Height = (int)_parsingParams.Height;
            }

            image.Prompt = info != null ? info["prompt"] : _parsingParams.Prompt;
            image.NegativePrompt = info != null ? info["negative"] : _parsingParams.NegativePrompt;
            image.SamplerId = await _db.GetSamplerIdByName(_parsingParams.SamplerName);
            image.Scheduler = _parsingParams.Scheduler;
            image.Steps = (int)_parsingParams.Steps;
            image.Seed = (long)_parsingParams.Seed;
            image.CfgScale = (float)_parsingParams.CfgScale;
            image.DenoisingStrength = _parsingParams.DenoisingStrength;
            image.Model = await _db.GetResourceByFilename(_currentModel);
            image.ModeId = await _db.GetMode(Parser.ModeTypeFromOutdir(outdir));

            return await _db.AddImage(image);
        }

        public async Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true)
        {
            if (File.Exists(path) && !overwrite) return false;
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(url);
            var response = await httpClient.GetByteArrayAsync(url);
            Directory.CreateDirectory(new FileInfo(path).DirectoryName);
            var imageData = _magick.ConvertToPng(response);
            if (imageData?.Length > 0)
            {
                await File.WriteAllBytesAsync(path, imageData);
                return true;
            }
            else return false;
        }

        private string GetImagePath(string path, int fileIndex, ModeType mode)
        {
            string infoname = ConvertPathPattern(_backend.OutputPaths.FilenamePattern, mode);
            string filename = $"{fileIndex.ToString().PadLeft(5, '0')}-{infoname}";
            return Path.Combine(path, filename);
        }

        /// <summary>
        /// In Img2Img, Width and Height are related to the section being masked and not the final image.
        /// This method will set global variables with the proper dimensions to be used in the image's data.
        /// </summary>
        private void SetSourceImageSize()
        {
            var data = Regex.Replace(_session.CanvasImageData, @"data.+?,", "");
            var size = _magick.GetImageSize(data);
            _canvasSourceWidth = size.Item1;
            _canvasSourceHeight = size.Item2;
        }

        private async void StartProgressChecker(BaseProgress progress)
        {
            // Progress checking temporarily disabled - will be reimplemented for ComfyUI
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            _progress.Add(progress);

            while (await _timer.WaitForNextTickAsync())
            {
                // ComfyUI progress checking to be implemented
                Progress = new InferenceProgress();
                _progress.Update(progress.Id, 0);
                NotifyStateChanged();
            }
        }

        private void StopProgressChecker(Guid id)
        {
            _timer?.Dispose();
            _progress.Remove(id);
            Progress = new();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
