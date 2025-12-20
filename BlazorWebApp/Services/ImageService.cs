using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for orchestrating image and video generation workflows.
    /// Coordinates between API services, file I/O, database operations, and progress tracking.
    /// Events are published through IEventService.
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
        private readonly IEventService _events;
        private PeriodicTimer? _timer;
        
        // Legacy fields - kept for backward compatibility with GetImages/GetVideo
        private SharedParameters _parsingParams;
        private Txt2ImgParameters _txt2imgParams;
        private Img2VidParameters _img2vidParams;
        private int _canvasSourceWidth;
        private int _canvasSourceHeight;
        private string _currentModel = string.Empty;
        
        // New fields for GenerationParameters-based flow
        private GenerationParameters? _currentGenerationParams;
        private Workflow? _currentWorkflow;

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
            IWildcardService wildcardService,
            IEventService events)
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
            _events = events;
        }

        /// <summary>
        /// Generates images based on the specified mode (Txt2Img, Img2Img).
        /// Handles the full generation workflow including API calls, file saving, and database persistence.
        /// </summary>
        /// <param name="mode">The generation mode to use.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
        [Obsolete("Use GenerateImagesAsync(GenerationParameters, Workflow) instead")]
        public async Task<ImagesDto> GetImages(ModeType mode)
        {
            // Legacy method - no longer functional after Phase 10 cleanup
            // Use GenerateImagesAsync(GenerationParameters, Workflow) instead
            throw new NotSupportedException("GetImages(ModeType) is no longer supported. Use GenerateImagesAsync(GenerationParameters, Workflow) instead.");
        }

        /// <summary>
        /// Generates a video from an image using Img2Vid parameters
        /// </summary>
        [Obsolete("Use GenerateVideoAsync(GenerationParameters, Workflow) instead")]
        public async Task<GeneratedVideos> GetVideo()
        {
            // Legacy method - no longer functional after Phase 10 cleanup
            // Use GenerateVideoAsync(GenerationParameters, Workflow) instead  
            throw new NotSupportedException("GetVideo() is no longer supported. Use GenerateVideoAsync(GenerationParameters, Workflow) instead.");
        }

        /// <summary>
        /// Saves generated videos to the output directory
        /// </summary>
        private async Task SaveVideos(GeneratedVideos videos, long actualSeed)
        {
            var saveDir = _io.CreateDirectory(GetVideoSaveFolder());
            var fileIndex = GetVideoFileIndex(saveDir.FullName);

            foreach (var video in videos.Videos)
            {
                fileIndex++;
                var filename = GenerateVideoFilename(fileIndex, actualSeed);
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

                // Update video metadata with actual seed used
                video.Filename = filename;
                video.Prompt = _img2vidParams.Prompt;
                video.NegativePrompt = _img2vidParams.NegativePrompt;
                video.Seed = actualSeed;
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
        private string GenerateVideoFilename(int fileIndex, long actualSeed)
        {
            var pattern = _backend.OutputPaths.FilenamePattern;
            var filename = $"{fileIndex.ToString().PadLeft(5, '0')}";

            if (!string.IsNullOrWhiteSpace(pattern))
            {
                filename += "-" + pattern
                    .Replace("[seed]", actualSeed.ToString())
                    .Replace("[steps]", _img2vidParams.Steps.ToString())
                    .Replace("[cfg]", _img2vidParams.CfgScale.ToString())
                    .Replace("[sampler]", _img2vidParams.SamplerName ?? "euler");
            }

            return filename + ".mp4";
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

        private void NotifyStateChanged(bool success = true, int count = 0) 
            => _events.Publish(new ImagesGeneratedEventArgs(success, count));

        #region New GenerationParameters-based Methods

        /// <inheritdoc />
        public async Task<ImagesDto> GenerateImagesAsync(GenerationParameters parameters, Workflow workflow)
        {
            if (workflow == null)
            {
                _logger.LogError("Cannot generate images: workflow is null");
                return new ImagesDto();
            }

            _logger.LogInformation("Starting image generation with GenerationParameters for workflow: {WorkflowTitle}", workflow.Title);
            _progress.IsConverging = true;

            ImagesDto images = new();
            _currentGenerationParams = parameters;
            _currentWorkflow = workflow;
            
            try
            {
                // Apply wildcard expansion and seed randomization directly to GenerationParameters
                await PrepareGenerationParametersAsync(parameters);
                
                // Get current model from assets
                _currentModel = parameters.Assets.GetValueOrDefault("Model", "") 
                    ?? _models.GetCurrentModel(workflow.Mode);

                // Call the new unified router method
                Images = await _router.PostGenerationAsync(parameters, workflow);

                if (_state.State.Generation.IsInterrupted)
                {
                    _logger.LogWarning("Generation was interrupted by user");
                    throw new Exception("Generation Canceled!");
                }

                if (_backend.OutputPaths.SaveSamples)
                {
                    var outdir = workflow.Mode == ModeType.Img2Img 
                        ? Outdir.Img2ImgSamples 
                        : Outdir.Txt2ImgSamples;
                    images = await SaveImagesFromGenerationParams(outdir, parameters, workflow);
                }

                _logger.LogInformation("Image generation completed for workflow: {WorkflowTitle}, generated {ImageCount} images", 
                    workflow.Title, images?.Images?.Count ?? 0);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during image generation for workflow: {WorkflowTitle}", workflow.Title);
            }

            _progress.IsConverging = false;
            _state.State.Generation.IsInterrupted = false;

            NotifyStateChanged();
            return images;
        }

        /// <inheritdoc />
        public async Task<GeneratedVideos> GenerateVideoAsync(GenerationParameters parameters, Workflow workflow)
        {
            if (workflow == null)
            {
                _logger.LogError("Cannot generate video: workflow is null");
                return new GeneratedVideos();
            }

            _logger.LogInformation("Starting video generation with GenerationParameters for workflow: {WorkflowTitle}", workflow.Title);
            _progress.IsConverging = true;
            GeneratedVideos = null;
            _currentGenerationParams = parameters;
            _currentWorkflow = workflow;

            try
            {
                // Apply wildcard expansion and seed randomization
                await PrepareGenerationParametersAsync(parameters);
                
                // Get current model from assets
                _currentModel = parameters.Assets.GetValueOrDefault("HighModel", "") 
                    ?? parameters.Assets.GetValueOrDefault("Model", "") 
                    ?? _models.GetCurrentModel(ModeType.Img2Vid);

                // Get the actual seed that will be used
                var samplerFragment = parameters.GetFragment("main_sampler");
                var seed = samplerFragment?.GetValueOrDefault("seed", -1L) ?? -1L;
                var actualSeed = seed == -1 
                    ? new Random().Next(0, int.MaxValue) 
                    : seed;
                
                // Update the seed in the fragment so it gets passed to ComfyUI
                if (samplerFragment != null)
                {
                    samplerFragment.SetValue("seed", actualSeed);
                }

                // Call the new unified router method
                GeneratedVideos = await _router.PostVideoGenerationAsync(parameters, workflow);

                if (_state.State.Generation.IsInterrupted)
                {
                    throw new Exception("Generation Canceled!");
                }

                // Store the actual seed used
                _state.State.Generation.Seed = actualSeed;

                if (_backend.OutputPaths.SaveSamples && GeneratedVideos?.Videos?.Count > 0)
                {
                    await SaveVideosFromGenerationParams(GeneratedVideos, actualSeed, parameters);
                    _session.AddSessionVideos(GeneratedVideos.Videos);
                }

                _logger.LogInformation("Video generation completed for workflow: {WorkflowTitle}", workflow.Title);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during video generation for workflow: {WorkflowTitle}", workflow.Title);
            }

            _progress.IsConverging = false;
            _state.State.Generation.IsInterrupted = false;

            NotifyStateChanged();
            return GeneratedVideos;
        }

        /// <summary>
        /// Prepares GenerationParameters for generation by applying wildcard expansion,
        /// seed randomization, and style processing.
        /// </summary>
        private async Task PrepareGenerationParametersAsync(GenerationParameters parameters)
        {
            // Get prompts fragment
            var promptsFragment = parameters.GetFragment("prompts");
            if (promptsFragment != null)
            {
                var prompt = promptsFragment.GetValueOrDefault<string>("positive", "") ?? "";
                var negativePrompt = promptsFragment.GetValueOrDefault<string>("negative", "") ?? "";
                
                // Apply wildcard expansion
                prompt = await _wildcardService.ParseWildcards(prompt);
                negativePrompt = await _wildcardService.ParseWildcards(negativePrompt);
                
                // Apply styles
                foreach (var style in _state.State.Generation.Styles)
                {
                    if (!string.IsNullOrWhiteSpace(style.Prompt))
                        prompt = style.Prompt.Replace("{prompt}", prompt);
                    if (!string.IsNullOrWhiteSpace(style.NegativePrompt))
                        negativePrompt = string.IsNullOrEmpty(negativePrompt) 
                            ? style.NegativePrompt 
                            : $"{negativePrompt}, {style.NegativePrompt}";
                }
                
                promptsFragment.SetValue("positive", prompt);
                promptsFragment.SetValue("negative", negativePrompt);
            }
            
            // Handle seed randomization for sampler fragment
            var samplerFragment = parameters.GetFragment("main_sampler");
            if (samplerFragment != null)
            {
                var seed = samplerFragment.GetValueOrDefault("seed", -1L);
                if (seed == -1)
                {
                    var actualSeed = new Random().Next(0, int.MaxValue);
                    samplerFragment.SetValue("seed", (long)actualSeed);
                    _state.State.Generation.Seed = actualSeed;
                }
                else
                {
                    _state.State.Generation.Seed = seed;
                }
            }
        }

        /// <summary>
        /// Saves generated images using data from GenerationParameters.
        /// </summary>
        private async Task<ImagesDto> SaveImagesFromGenerationParams(Outdir outdirSamples, GenerationParameters parameters, Workflow workflow)
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

        /// <summary>
        /// Saves generated videos to disk and database using GenerationParameters.
        /// </summary>
        private async Task SaveVideosFromGenerationParams(GeneratedVideos videos, long actualSeed, GenerationParameters parameters)
        {
            var saveDir = _io.CreateDirectory(GetVideoSaveFolder());
            var fileIndex = GetVideoFileIndex(saveDir.FullName);

            // Extract sampler info from parameters
            var samplerFragment = parameters.Fragments.Values.FirstOrDefault(f => 
                f.FragmentFile.Contains("sampler", StringComparison.OrdinalIgnoreCase));
            var promptsFragment = parameters.Fragments.Values.FirstOrDefault(f => 
                f.FragmentFile.Contains("prompt", StringComparison.OrdinalIgnoreCase));
            var videoFragment = parameters.Fragments.Values.FirstOrDefault(f => 
                f.FragmentFile.Contains("video", StringComparison.OrdinalIgnoreCase) || 
                f.FragmentFile.Contains("wan", StringComparison.OrdinalIgnoreCase));

            foreach (var video in videos.Videos)
            {
                fileIndex++;
                var filename = GenerateVideoFilename(fileIndex, actualSeed);
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

                // Update video metadata from parameters
                video.Filename = filename;
                video.Prompt = promptsFragment?.GetValue<string>("positive") ?? "";
                video.NegativePrompt = promptsFragment?.GetValue<string>("negative") ?? "";
                video.Seed = actualSeed;
                video.Steps = samplerFragment?.GetValueOrDefault("steps", 20) ?? 20;
                video.CfgScale = (float)(samplerFragment?.GetValueOrDefault("cfg", 1.0) ?? 1.0);
                video.Sampler = samplerFragment?.GetValue<string>("sampler_name") ?? "euler";
                video.Model = parameters.Assets.GetValueOrDefault("Model") ?? 
                              parameters.Assets.GetValueOrDefault("HighModel") ?? "";
                
                // Get resolution from latent or video fragment
                var latentFragment = parameters.Fragments.Values.FirstOrDefault(f => 
                    f.FragmentFile.Contains("latent", StringComparison.OrdinalIgnoreCase));
                video.Width = latentFragment?.GetValueOrDefault("width", 768) ?? 
                              videoFragment?.GetValueOrDefault("width", 768) ?? 768;
                video.Height = latentFragment?.GetValueOrDefault("height", 768) ?? 
                               videoFragment?.GetValueOrDefault("height", 768) ?? 768;
                
                video.FrameCount = videoFragment?.GetValueOrDefault("length", 81) ?? 81;
                video.FrameRate = videoFragment?.GetValueOrDefault("frame_rate", 16) ?? 16;
                video.Duration = video.FrameRate > 0 ? (double)video.FrameCount / video.FrameRate : 0;

                // Persist to database
                await AddVideoToDbFromParams(video, video.Steps, video.CfgScale, video.Sampler, 
                    samplerFragment?.GetValue<string>("scheduler") ?? "simple", parameters);
            }
        }

        /// <summary>
        /// Adds a video to the database using GenerationParameters for metadata.
        /// </summary>
        private async Task<Image> AddVideoToDbFromParams(GeneratedVideo video, int steps, double cfg, string samplerName, string scheduler, GenerationParameters parameters)
        {
            var samplerId = await _db.GetSamplerIdByName(samplerName);
            
            var image = new Image
            {
                Path = video.FilePath,
                Prompt = video.Prompt,
                NegativePrompt = video.NegativePrompt,
                Seed = video.Seed,
                Width = video.Width,
                Height = video.Height,
                Steps = steps,
                CfgScale = (float)cfg,
                DenoisingStrength = 1.0,
                DateCreated = DateTime.Now,
                SamplerId = samplerId,
                Scheduler = scheduler,
                Favorite = false,
                Score = 0,
                ModeId = (int)ModeType.Img2Vid,
                ProjectId = _state.State.Gallery.ProjectId
            };

            await _db.AddImage(image);
            video.Id = image.Id;
            
            return image;
        }

        private async Task BuildTxt2ImgParametersAsync(string scriptName)
        {
            // Legacy method - no longer needed after Phase 10 cleanup
            // All parameter building is now done via GenerationParameters
            await Task.CompletedTask;
        }

        private async Task<Img2ImgParameters> BuildImg2ImgParametersAsync(string scriptName)
        {
            // Legacy method - no longer needed after Phase 10 cleanup
            // All parameter building is now done via GenerationParameters
            await Task.CompletedTask;
            return new Img2ImgParameters();
        }

        #endregion

        #region Legacy Methods (to be removed)
        
        // All legacy methods have been removed in Phase 10.7
        // Use GenerateImagesAsync(GenerationParameters, Workflow) for image generation
        // Use GenerateVideoAsync(GenerationParameters, Workflow) for video generation
        
        #endregion

        /// <summary>
        /// Gets the full path for an image file based on the save directory and index.
        /// </summary>
        private string GetImagePath(string saveDir, int fileIndex, ModeType mode)
        {
            var pattern = _backend.OutputPaths.FilenamePattern ?? "";
            var seed = _state.State.Generation.Seed.ToString();
            
            // Get settings from the current parameters
            var steps = _parsingParams?.Steps?.ToString() ?? "20";
            var cfg = _parsingParams?.CfgScale?.ToString() ?? "7";
            var sampler = _parsingParams?.SamplerName ?? "euler";
            
            var filename = fileIndex.ToString().PadLeft(5, '0');
            
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                filename += "-" + pattern
                    .Replace("[seed]", seed)
                    .Replace("[steps]", steps)
                    .Replace("[cfg]", cfg)
                    .Replace("[sampler]", sampler);
            }
            
            return Path.Combine(saveDir, filename);
        }
    }
}
