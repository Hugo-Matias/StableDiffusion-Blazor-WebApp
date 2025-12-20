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
        
        // Legacy field - kept for backward compatibility during transition
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

        #region Legacy Methods

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

        #endregion

        #region Image Generation

        /// <summary>
        /// Generates images based on the specified mode (Txt2Img, Img2Img).
        /// Handles the full generation workflow including API calls, file saving, and database persistence.
        /// </summary>
        /// <param name="mode">The generation mode to use.</param>
        /// <returns>DTO containing generated image information and metadata.</returns>
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
            var info = Parser.ParseInfoStrings(Images.Info, mode);

            // Extract parameters from GenerationParameters
            var samplerFragment = parameters.GetFragment("main_sampler");
            var promptsFragment = parameters.GetFragment("prompts");
            var latentFragment = parameters.GetFragment("latent");
            
            var seed = samplerFragment?.GetValueOrDefault("seed", _state.State.Generation.Seed) ?? _state.State.Generation.Seed;
            var steps = samplerFragment?.GetValueOrDefault("steps", 20) ?? 20;
            var cfg = samplerFragment?.GetValueOrDefault("cfg", 7.0) ?? 7.0;
            var samplerName = samplerFragment?.GetValue<string>("sampler_name") ?? "euler";
            var scheduler = samplerFragment?.GetValue<string>("scheduler") ?? "normal";
            var denoise = samplerFragment?.GetValueOrDefault("denoise", 1.0) ?? 1.0;
            
            var prompt = promptsFragment?.GetValue<string>("positive") ?? info?["prompt"] ?? "";
            var negativePrompt = promptsFragment?.GetValue<string>("negative") ?? info?["negative"] ?? "";
            
            var width = latentFragment?.GetValueOrDefault("width", 1024) ?? 1024;
            var height = latentFragment?.GetValueOrDefault("height", 1024) ?? 1024;

            for (int i = 0; i < Images.Images.Count; i++)
            {
                fileIndex++;
                var extension = _backend.OutputPaths.SamplesFormat.ToLowerInvariant();

                var fullpath = GetImagePathFromParams(saveDir.FullName, fileIndex, seed, steps, cfg, samplerName);
                var imagePath = $"{fullpath}.{extension}";

                await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(Images.Images[i]));

                // Create image entity from GenerationParameters
                var image = new Image
                {
                    Path = imagePath,
                    ProjectId = _state.State.Gallery.ProjectId,
                    Width = width,
                    Height = height,
                    Prompt = prompt,
                    NegativePrompt = negativePrompt,
                    SamplerId = await _db.GetSamplerIdByName(samplerName),
                    Scheduler = scheduler,
                    Steps = steps,
                    Seed = seed,
                    CfgScale = (float)cfg,
                    DenoisingStrength = denoise,
                    Model = await _db.GetResourceByFilename(_currentModel),
                    ModeId = await _db.GetMode(mode),
                    DateCreated = DateTime.Now
                };

                savedImages.Images.Add(await _db.AddImage(image));
            }

            return savedImages;
        }

        /// <summary>
        /// Gets the full path for an image file based on GenerationParameters.
        /// </summary>
        private string GetImagePathFromParams(string saveDir, int fileIndex, long seed, int steps, double cfg, string sampler)
        {
            var pattern = _backend.OutputPaths.FilenamePattern ?? "";
            
            var filename = fileIndex.ToString().PadLeft(5, '0');
            
            if (!string.IsNullOrWhiteSpace(pattern))
            {
                filename += "-" + pattern
                    .Replace("[seed]", seed.ToString())
                    .Replace("[steps]", steps.ToString())
                    .Replace("[cfg]", cfg.ToString())
                    .Replace("[sampler]", sampler);
            }
            
            return Path.Combine(saveDir, filename);
        }

        #endregion

        #region Video Generation

        /// <summary>
        /// Generates a video from an image using Img2Vid parameters
        /// </summary>
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
        /// Saves generated videos to the output directory
        /// </summary>
        private async Task SaveVideosFromGenerationParams(GeneratedVideos videos, long actualSeed, GenerationParameters parameters)
        {
            var saveDir = _io.CreateDirectory(GetVideoSaveFolder());
            var fileIndex = GetVideoFileIndex(saveDir.FullName);

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
                var filename = GenerateVideoFilename(fileIndex, actualSeed, samplerFragment);
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
                video.Prompt = promptsFragment?.GetValue<string>("positive") ?? "";
                video.NegativePrompt = promptsFragment?.GetValue<string>("negative") ?? "";
                video.Seed = actualSeed;
                video.Steps = samplerFragment?.GetValueOrDefault("steps", 20) ?? 20;
                video.CfgScale = (float)(samplerFragment?.GetValueOrDefault("cfg", 1.0) ?? 1.0);
                video.Sampler = samplerFragment?.GetValue<string>("sampler_name") ?? "euler";
                video.Model = parameters.Assets.GetValueOrDefault("Model") ?? 
                              parameters.Assets.GetValueOrDefault("HighModel") ?? "";
                
                var latentFragment = parameters.Fragments.Values.FirstOrDefault(f => 
                    f.FragmentFile.Contains("latent", StringComparison.OrdinalIgnoreCase));
                video.Width = latentFragment?.GetValueOrDefault("width", 768) ?? 
                              videoFragment?.GetValueOrDefault("width", 768) ?? 768;
                video.Height = latentFragment?.GetValueOrDefault("height", 768) ?? 
                               videoFragment?.GetValueOrDefault("height", 768) ?? 768;
                
                video.FrameCount = videoFragment?.GetValueOrDefault("length", 81) ?? 81;
                video.FrameRate = videoFragment?.GetValueOrDefault("frame_rate", 16) ?? 16;
                video.Duration = video.FrameRate > 0 ? (double)video.FrameCount / video.FrameRate : 0;

                // Persist to database using Image entity
                await AddVideoToDbFromParams(video, video.Steps, video.CfgScale, video.Sampler, 
                    samplerFragment?.GetValue<string>("scheduler") ?? "simple", parameters);
            }
        }

        /// <summary>
        /// Adds a generated video to the database using the Image entity
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

        private string GenerateVideoFilename(int fileIndex, long actualSeed, FragmentParameters? samplerFragment)
        {
            var pattern = _backend.OutputPaths.FilenamePattern;
            var filename = $"{fileIndex.ToString().PadLeft(5, '0')}";

            if (!string.IsNullOrWhiteSpace(pattern))
            {
                filename += "-" + pattern
                    .Replace("[seed]", actualSeed.ToString())
                    .Replace("[steps]", samplerFragment?.GetValueOrDefault("steps", 20).ToString() ?? "20")
                    .Replace("[cfg]", samplerFragment?.GetValueOrDefault("cfg", 1.0).ToString() ?? "1")
                    .Replace("[sampler]", samplerFragment?.GetValue<string>("sampler_name") ?? "euler");
            }

            return filename + ".mp4";
        }

        #endregion

        #region Utilities

        public string GetCurrentSaveFolder(Outdir? outdir)
        {
            if (outdir == null) return string.Empty;
            
            var basePath = _backend.GetOutputPath(outdir.Value);
            if (string.IsNullOrEmpty(basePath)) return string.Empty;
            
            if (outdir == Outdir.Extras) return basePath;

            var dirPattern = _backend.OutputPaths.DirectoryPattern;
            if (!string.IsNullOrWhiteSpace(dirPattern))
            {
                var subPath = ConvertPathPattern(dirPattern, Parser.ModeTypeFromOutdir(outdir.Value));
                basePath = Path.Combine(basePath, subPath).Replace('/', Path.DirectorySeparatorChar);
            }

            return basePath;
        }

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
                "[sampler]" => _currentGenerationParams?.GetFragment("main_sampler")?.GetValue<string>("sampler_name") ?? "euler",
                "[seed]" => _state.State.Generation.Seed.ToString(),
                "[steps]" => _currentGenerationParams?.GetFragment("main_sampler")?.GetValueOrDefault("steps", 20).ToString() ?? "20",
                "[cfg]" => _currentGenerationParams?.GetFragment("main_sampler")?.GetValueOrDefault("cfg", 7.0).ToString() ?? "7",
                _ => string.Empty
            };
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
            return false;
        }

        private void NotifyStateChanged(bool success = true, int count = 0) 
            => _events.Publish(new ImagesGeneratedEventArgs(success, count));

        #endregion
    }
}
