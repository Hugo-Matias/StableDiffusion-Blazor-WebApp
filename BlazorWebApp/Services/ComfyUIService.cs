using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class ComfyUIService
    {
        private readonly WorkflowService _workflow;
        private readonly IOService _io;
        private readonly ILogger<ComfyUIService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ComfyUIEventBus _bus;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonIgnoreNull;
        private readonly ConcurrentDictionary<Guid, object> _pendingJobs = new();

        // Track uploaded images for cleanup: promptId -> list of uploaded filenames
        private readonly ConcurrentDictionary<Guid, List<string>> _uploadedImages = new();

        // Cache of image hashes to filenames to avoid re-uploading identical images
        private readonly ConcurrentDictionary<string, string> _imageHashCache = new();

        // Supported video extensions
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".gif", ".avi", ".mov", ".mkv"
        };

        public ComfyUIService(HttpClient httpClient, ComfyUIEventBus bus, IConfiguration configuration, WorkflowService workflow, IOService io, ILogger<ComfyUIService> logger)
        {
            _httpClient = httpClient;
            _bus = bus;
            _configuration = configuration;
            _workflow = workflow;
            _io = io;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("http://localhost:8188/");
            _httpClient.Timeout = TimeSpan.FromDays(1);
            _jsonIgnoreNull = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

            _bus.ExecutionSucceeded += async promptId => await HandleExecutionSucceededAsync(promptId);

            _bus.ExecutionFailed += async (promptId, error) =>
            {
                if (_pendingJobs.TryRemove(promptId, out var obj))
                {
                    if (obj is TaskCompletionSource<GeneratedImages> imgTcs)
                    {
                        imgTcs.SetException(new Exception(error));
                    }
                    else if (obj is TaskCompletionSource<GeneratedVideos> vidTcs)
                    {
                        vidTcs.SetException(new Exception(error));
                    }
                    else if (obj is TaskCompletionSource<LLMResponse> llmTcs)
                    {
                        llmTcs.SetException(new Exception(error));
                    }
                }

                // Cleanup uploaded input images on failure
                await CleanupUploadedImagesAsync(promptId);
            };
        }


        private async Task HandleExecutionSucceededAsync(Guid promptId)
        {
            if (!_pendingJobs.TryGetValue(promptId, out var obj))
            {
                _logger.LogDebug("No pending job found for prompt {PromptId}", promptId);
                return;
            }

            var objType = obj.GetType();

            // Check for LLM job
            var isLLMJob = objType.IsGenericType &&
                           objType.GetGenericTypeDefinition() == typeof(TaskCompletionSource<>) &&
                           objType.GetGenericArguments()[0] == typeof(LLMResponse);

            if (isLLMJob)
            {
                _logger.LogDebug("Handling LLM job completion for prompt {PromptId}", promptId);
                var llmTcs = (TaskCompletionSource<LLMResponse>)obj;

                try
                {
                    var text = await GetTextFromHistory(promptId);
                    _pendingJobs.TryRemove(promptId, out _);
                    llmTcs.SetResult(new LLMResponse
                    {
                        Text = text ?? string.Empty,
                        PromptId = promptId.ToString()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get LLM text output for prompt {PromptId}", promptId);
                    _pendingJobs.TryRemove(promptId, out _);
                    llmTcs.SetException(ex);
                }
                return;
            }

            // Check for Video job
            var isVideoJob = objType.IsGenericType &&
                             objType.GetGenericTypeDefinition() == typeof(TaskCompletionSource<>) &&
                             objType.GetGenericArguments()[0] == typeof(GeneratedVideos);

            if (isVideoJob)
            {
                _logger.LogDebug("Handling video job completion for prompt {PromptId}", promptId);
                await HandleVideoJobCompletionAsync(promptId, (TaskCompletionSource<GeneratedVideos>)obj);
                return;
            }

            // Default: Image job
            _logger.LogDebug("Handling image job completion for prompt {PromptId}", promptId);
            await HandleImageJobCompletionAsync(promptId, obj);
        }

        private async Task HandleImageJobCompletionAsync(Guid promptId, object obj)
        {
            var files = await GetFilenameFromHistory(promptId);
            if (files == null || files.Count == 0)
            {
                _logger.LogError("File not found for prompt {PromptId}!", promptId);

                if (_pendingJobs.TryRemove(promptId, out var imgObj) && imgObj is TaskCompletionSource<GeneratedImages> imgTcs)
                {
                    imgTcs.SetException(new Exception("No image files generated"));
                }
                return;
            }

            var images = new GeneratedImages() { Images = [] };
            var workflowInfo = string.Empty;

            foreach (var file in files)
            {
                var filepath = Path.Combine(_configuration["ComfyUIPath"], "output", file);
                var base64 = await _io.GetBase64FromFileAsync(filepath);
                images.Images.Add(base64);

                try
                {
                    var metadata = await _io.ReadMetadata(filepath);

                    // ComfyUI embeds as "prompt: {json}" - extract just the JSON
                    if (!string.IsNullOrWhiteSpace(metadata))
                    {
                        var match = Regex.Match(metadata, @"^(?:prompt|workflow):\s*(\{.+\})$", RegexOptions.Singleline);
                        workflowInfo = match.Success ? match.Groups[1].Value : metadata;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read metadata from image file: {FilePath}", filepath);
                }
            }

            images.Info = workflowInfo;

            if (_pendingJobs.TryRemove(promptId, out var finalObj) && finalObj is TaskCompletionSource<GeneratedImages> tcs)
                tcs.SetResult(images);
        }

        private async Task HandleVideoJobCompletionAsync(Guid promptId, TaskCompletionSource<GeneratedVideos> tcs)
        {
            try
            {
                var files = await GetVideoFilenameFromHistory(promptId);
                if (files == null || files.Count == 0)
                {
                    _logger.LogError("Video file not found for prompt {PromptId}!", promptId);
                    _pendingJobs.TryRemove(promptId, out _);
                    tcs.SetException(new Exception("No video files generated"));
                    return;
                }

                var videos = new GeneratedVideos();

                foreach (var file in files)
                {
                    var filepath = Path.Combine(_configuration["ComfyUIPath"], "output", file);

                    if (!File.Exists(filepath))
                    {
                        _logger.LogWarning("Video file not found on disk: {FilePath}", filepath);
                        continue;
                    }

                    var video = new GeneratedVideo
                    {
                        FilePath = filepath,
                        Filename = Path.GetFileName(file),
                        DateCreated = DateTime.Now
                    };

                    // For smaller videos, we can include base64 data
                    var fileInfo = new FileInfo(filepath);
                    if (fileInfo.Length < 50 * 1024 * 1024) // Less than 50MB
                    {
                        video.VideoData = await _io.GetBase64FromFileAsync(filepath);
                    }

                    videos.Videos.Add(video);
                }

                if (videos.Videos.Count == 0)
                {
                    _pendingJobs.TryRemove(promptId, out _);
                    tcs.SetException(new Exception("No video files could be loaded"));
                    return;
                }

                _pendingJobs.TryRemove(promptId, out _);
                tcs.SetResult(videos);
            }
            finally
            {
                // Cleanup uploaded input images after job completion
                await CleanupUploadedImagesAsync(promptId);
            }
        }

        // TODO: Load from AppSettings
        public async Task<Options> GenerateOptions()
        {
            Options options = new Options()
            {
                ClipSkip = 1,
                SaveTxt = false,
                GridSave = false,
                SamplesSave = true,
                SamplesFormat = "png",
                FilenamePatternDir = "[model_name]/[sampler]",
                FilenamePatternSamples = "[seed]_[steps]_[cfg]",
                OutdirSamplesImg2Img = Path.Combine(_configuration["OutputDir"], "Image-2-Image\\_samples"),
                OutdirSamplesTxt2Img = Path.Combine(_configuration["OutputDir"], "Text-2-Image\\_samples"),
                OutdirSamplesExtras = Path.Combine(_configuration["OutputDir"], "Extras"),
                OutdirSamplesImg2Vid = Path.Combine(_configuration["OutputDir"], "Image-2-Video\\_samples")
            };

            return options;
        }

        #region GET
        /// <summary>
        /// Checks if ComfyUI backend is available by querying the system stats endpoint.
        /// Returns true if the endpoint responds successfully, false otherwise.
        /// </summary>
        public async Task<bool> CheckComfyUIState()
        {
            try
            {
                var response = await _httpClient.GetAsync("/system_stats");
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<string> GetClientIdFromHistory()
        {
            var response = await _httpClient.GetAsync($"/history");
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var clientId = Parser.FindJsonValueByKey<string>(doc.RootElement, "client_id");
            return clientId;
        }

        private async Task<List<T>> GetModels<T>(string type, Func<string, T> mapper, Func<T, bool>? filter = null)
        {
            var models = await _httpClient.GetFromJsonAsync<List<string>>($"/models/{type}");
            var mapped = models?.Select(mapper).ToList() ?? [];
            return filter is null ? mapped : mapped.Where(filter).ToList();
        }

        public async Task<List<SDModel>> GetCheckpoints() => await GetModels("checkpoints", m => new SDModel { Title = m, Model_name = m });

        public async Task<List<Upscaler>> GetUpscalers() => await GetModels("upscale_models", m => new Upscaler { Name = m });

        public async Task<List<string>> GetVAEs() => await GetModels("vae", m => m);

        public async Task<List<string>> GetTextEncoders() => await GetModels("text_encoders", m => m);

        public async Task<List<SDModel>> GetDiffusionModels() => await GetModels("diffusion_models", m => new SDModel { Title = m, Model_name = m });

        /// <summary>
        /// Gets available CLIP models from ComfyUI.
        /// Maps to the "clip" model type which includes text encoders like T5, CLIP-L, etc.
        /// </summary>
        public async Task<List<string>> GetClipModels() => await GetModels("text_encoders", m => m);

        /// <summary>
        /// Gets available CLIP Vision models from ComfyUI.
        /// Maps to the "clip_vision" model type for image understanding models.
        /// </summary>
        public async Task<List<string>> GetClipVisionModels() => await GetModels("clip_vision", m => m);

        public async Task<List<string>> GetLoras() => await GetModels("loras", m => m);

        public async Task<List<string>> SearchLoras(string search)
        {
            Func<string, bool> filter = null;
            if (!string.IsNullOrEmpty(search)) filter = m => m.ToLower().Contains(search.ToLower());
            return await GetModels("loras", m => m, filter);
        }

        public async Task<List<string>> GetBBoxDetailers() => await GetModels("ultralytics_bbox", m => m);

        public async Task<List<Models.Sampler>> GetSamplers() => await GetNodeInputOptions<Models.Sampler>("ClownsharKSampler_Beta", "sampler_name", name => new Models.Sampler { Name = name });
        public async Task<List<Models.Scheduler>> GetSchedulers() => await GetNodeInputOptions<Models.Scheduler>("ClownsharKSampler_Beta", "scheduler", name => new Models.Scheduler { Name = name });

        public async Task<List<string>> GetDetailerSamplers() => await GetNodeInputOptions<string>("FaceDetailer", "sampler_name", name => name);

        public async Task<List<string>> GetDetailerSchedulers() => await GetNodeInputOptions<string>("FaceDetailer", "scheduler", name => name);

        private async Task<List<T>> GetNodeInputOptions<T>(string node, string inputName, Func<string, T> mapFunc)
        {
            var response = await _httpClient.GetAsync($"/object_info/{node}");
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var root = doc.RootElement;

            // Navigate: root[samplerKey]["input"]["required"][fieldName][0]
            if (root.TryGetProperty(node, out var samplerNode) &&
                samplerNode.TryGetProperty("input", out var inputNode) &&
                inputNode.TryGetProperty("required", out var requiredNode) &&
                requiredNode.TryGetProperty(inputName, out var fieldNode) &&
                fieldNode.ValueKind == JsonValueKind.Array &&
                fieldNode[0].ValueKind == JsonValueKind.Array)
            {
                return fieldNode[0]
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => mapFunc(e.GetString()!))
                    .ToList();
            }

            return new List<T>();
        }

        public async Task<List<string>> GetFilenameFromHistory(Guid promptId)
        {
            var files = new List<string>();
            var response = await _httpClient.GetAsync($"/history/{promptId}");
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty(promptId.ToString(), out var promptElement))
            {
                _logger.LogWarning("Prompt ID {PromptId} not found in history response", promptId);
                return files;
            }

            if (!promptElement.TryGetProperty("outputs", out var outputsElement))
            {
                _logger.LogWarning("No outputs found for prompt ID {PromptId}", promptId);
                return files;
            }

            var outputs = Parser.GetFirstJsonProperty(outputsElement);
            if (outputs == null || !outputs.Value.TryGetProperty("images", out var imagesElement))
            {
                _logger.LogWarning("No images found in outputs for prompt ID {PromptId}", promptId);
                return files;
            }

            var images = JsonSerializer.Deserialize<List<ComfyUIHistoryImageResponse>>(imagesElement.GetRawText());
            foreach (var image in images)
            {
                files.Add(Path.Combine(image.Subfolder, image.Filename));
            }
            return files;
        }

        /// <summary>
        /// Gets video filenames from ComfyUI history. Video outputs are typically in "gifs" or "videos" property.
        /// </summary>
        public async Task<List<string>> GetVideoFilenameFromHistory(Guid promptId)
        {
            var files = new List<string>();
            var response = await _httpClient.GetAsync($"/history/{promptId}");
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty(promptId.ToString(), out var promptElement))
            {
                _logger.LogWarning("Prompt ID {PromptId} not found in history response", promptId);
                return files;
            }

            if (!promptElement.TryGetProperty("outputs", out var outputsElement))
            {
                _logger.LogWarning("No outputs found for prompt ID {PromptId}", promptId);
                return files;
            }

            // Iterate through all output nodes to find video outputs
            foreach (var outputNode in outputsElement.EnumerateObject())
            {
                var nodeValue = outputNode.Value;

                // Check for "gifs" property (common for video outputs in ComfyUI)
                if (nodeValue.TryGetProperty("gifs", out var gifsElement) && gifsElement.ValueKind == JsonValueKind.Array)
                {
                    var videoFiles = JsonSerializer.Deserialize<List<ComfyUIHistoryImageResponse>>(gifsElement.GetRawText());
                    foreach (var video in videoFiles ?? [])
                    {
                        var ext = Path.GetExtension(video.Filename);
                        if (VideoExtensions.Contains(ext))
                        {
                            files.Add(Path.Combine(video.Subfolder ?? "", video.Filename));
                        }
                    }
                }

                // Check for "videos" property (alternative naming)
                if (nodeValue.TryGetProperty("videos", out var videosElement) && videosElement.ValueKind == JsonValueKind.Array)
                {
                    var videoFiles = JsonSerializer.Deserialize<List<ComfyUIHistoryImageResponse>>(videosElement.GetRawText());
                    foreach (var video in videoFiles ?? [])
                    {
                        files.Add(Path.Combine(video.Subfolder ?? "", video.Filename));
                    }
                }

                // Also check images for mp4/gif files (some nodes output videos as "images")
                if (nodeValue.TryGetProperty("images", out var imagesElement) && imagesElement.ValueKind == JsonValueKind.Array)
                {
                    var imageFiles = JsonSerializer.Deserialize<List<ComfyUIHistoryImageResponse>>(imagesElement.GetRawText());
                    foreach (var file in imageFiles ?? [])
                    {
                        var ext = Path.GetExtension(file.Filename);
                        if (VideoExtensions.Contains(ext))
                        {
                            files.Add(Path.Combine(file.Subfolder ?? "", file.Filename));
                        }
                    }
                }
            }

            return files;
        }

        private async Task<string?> GetTextFromHistory(Guid promptId)
        {
            var response = await _httpClient.GetAsync($"/history/{promptId}");
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty(promptId.ToString(), out var promptElement))
            {
                _logger.LogWarning("Prompt ID {PromptId} not found in history response", promptId);
                return null;
            }

            if (!promptElement.TryGetProperty("outputs", out var outputsElement))
            {
                _logger.LogWarning("No outputs found for prompt ID {PromptId}", promptId);
                return null;
            }

            // Find the first node with text output
            foreach (var outputNode in outputsElement.EnumerateObject())
            {
                var nodeValue = outputNode.Value;

                if (nodeValue.TryGetProperty("string", out var stringElement) &&
                    stringElement.ValueKind == JsonValueKind.Array &&
                    stringElement.GetArrayLength() > 0)
                {
                    return stringElement[0].GetString();
                }

                // Some nodes might use "text"
                if (nodeValue.TryGetProperty("text", out var textElement) &&
                    textElement.ValueKind == JsonValueKind.Array &&
                    textElement.GetArrayLength() > 0)
                {
                    return textElement[0].GetString();
                }
            }

            _logger.LogWarning("No text output found for prompt ID {PromptId}", promptId);
            return null;
        }
        #endregion

        #region POST
        /// <summary>
        /// Uploads an image to ComfyUI's input folder. Uses content hash for deduplication.
        /// Returns the filename that can be used in LoadImage nodes.
        /// </summary>
        public async Task<string> UploadImageAsync(string base64Data, Guid? promptId = null)
        {
            // Remove data URI prefix if present
            var base64 = base64Data;
            if (base64.Contains(","))
            {
                base64 = base64.Split(',')[1];
            }

            var imageBytes = Convert.FromBase64String(base64);

            // Generate hash of image content for deduplication
            var hash = ComputeHash(imageBytes);

            // Check if we already uploaded this exact image
            if (_imageHashCache.TryGetValue(hash, out var existingFilename))
            {
                _logger.LogDebug("Image already uploaded with hash {Hash}, reusing {Filename}", hash, existingFilename);
                return existingFilename;
            }

            // Use hash as filename to ensure uniqueness and deduplication
            var filename = $"blazor_input_{hash[..16]}.png";

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", filename);
            content.Add(new StringContent("true"), "overwrite");

            var response = await _httpClient.PostAsync("/upload/image", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ComfyUIUploadResponse>();
            var uploadedFilename = result?.Name ?? filename;

            // Cache the hash -> filename mapping
            _imageHashCache[hash] = uploadedFilename;

            // Track for cleanup if we have a promptId
            if (promptId.HasValue)
            {
                _uploadedImages.AddOrUpdate(
                    promptId.Value,
                    new List<string> { uploadedFilename },
                    (_, list) => { list.Add(uploadedFilename); return list; }
                );
            }

            _logger.LogDebug("Uploaded image {Filename} with hash {Hash}", uploadedFilename, hash);
            return uploadedFilename;
        }

        /// <summary>
        /// Computes a SHA256 hash of the image bytes
        /// </summary>
        private static string ComputeHash(byte[] bytes)
        {
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Cleans up uploaded input images for a completed job.
        /// Called after job completion (success or failure).
        /// </summary>
        private async Task CleanupUploadedImagesAsync(Guid promptId)
        {
            if (!_uploadedImages.TryRemove(promptId, out var filenames))
                return;

            foreach (var filename in filenames)
            {
                try
                {
                    // Remove from hash cache so it can be re-uploaded if needed
                    var hashToRemove = _imageHashCache.FirstOrDefault(kv => kv.Value == filename).Key;
                    if (hashToRemove != null)
                    {
                        _imageHashCache.TryRemove(hashToRemove, out _);
                    }

                    // Delete the file from ComfyUI input folder
                    var inputPath = Path.Combine(_configuration["ComfyUIPath"], "input", filename);
                    if (File.Exists(inputPath))
                    {
                        File.Delete(inputPath);
                        _logger.LogDebug("Cleaned up input image: {Filename}", filename);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup input image: {Filename}", filename);
                }
            }
        }

        public async Task<TResponse> PostPromptAsync<TResponse>(object payload, string payloadLogPath = "payload.json")
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(payloadLogPath, json);

            using var response = await _httpClient.PostAsJsonAsync("/prompt", payload, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();

            var submit = await response.Content.ReadFromJsonAsync<ComfyUIPromptSubmitResponse>();
            var promptId = Guid.Parse(submit.PromptId);

            var tcs = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJobs[promptId] = tcs;

            return await tcs.Task;
        }


        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgComfyUI param, string clientId, Workflow workflow)
        {
            var workflowJson = _workflow.ComposeWorkflowFromTemplate(workflow, param);
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };
            return await PostPromptAsync<GeneratedImages>(payload);
        }

        public async Task<GeneratedImages> PostImg2Img(Img2ImgComfyUI param, string clientId, Workflow workflow)
        {
            // Generate a temporary prompt ID for tracking uploads
            // We'll get the real one after submission
            var tempId = Guid.NewGuid();

            // Upload the image first if it's base64 data
            if (!string.IsNullOrEmpty(param.Image) && (param.Image.StartsWith("data:") || param.Image.Length > 260))
            {
                var uploadedFilename = await UploadImageAsync(param.Image, tempId);
                param.Image = uploadedFilename;
                _logger.LogDebug("Uploaded image for Img2Img: {Filename}", uploadedFilename);
            }

            var workflowJson = _workflow.ComposeWorkflowFromTemplate(workflow, param);
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync("payload_img2img.json", json);

            using var response = await _httpClient.PostAsJsonAsync("/prompt", payload, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();

            var submit = await response.Content.ReadFromJsonAsync<ComfyUIPromptSubmitResponse>();
            var promptId = Guid.Parse(submit.PromptId);

            // Transfer the uploaded images tracking from temp ID to real prompt ID
            if (_uploadedImages.TryRemove(tempId, out var uploadedFiles))
            {
                _uploadedImages[promptId] = uploadedFiles;
            }

            var tcs = new TaskCompletionSource<GeneratedImages>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJobs[promptId] = tcs;

            return await tcs.Task;
        }

        public async Task<GeneratedVideos> PostImg2Vid(Img2VidComfyUI param, string clientId, Workflow workflow)
        {
            // Generate a temporary prompt ID for tracking uploads
            // We'll get the real one after submission
            var tempId = Guid.NewGuid();

            // Upload the image first if it's base64 data
            if (!string.IsNullOrEmpty(param.Image) && (param.Image.StartsWith("data:") || param.Image.Length > 260))
            {
                var uploadedFilename = await UploadImageAsync(param.Image, tempId);
                param.Image = uploadedFilename;
                _logger.LogDebug("Uploaded image for Img2Vid: {Filename}", uploadedFilename);
            }

            var workflowJson = _workflow.ComposeWorkflowFromTemplate(workflow, param);
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync("payload_img2vid.json", json);

            using var response = await _httpClient.PostAsJsonAsync("/prompt", payload, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();

            var submit = await response.Content.ReadFromJsonAsync<ComfyUIPromptSubmitResponse>();
            var promptId = Guid.Parse(submit.PromptId);

            // Transfer the uploaded images tracking from temp ID to real prompt ID
            if (_uploadedImages.TryRemove(tempId, out var uploadedFiles))
            {
                _uploadedImages[promptId] = uploadedFiles;
            }

            var tcs = new TaskCompletionSource<GeneratedVideos>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJobs[promptId] = tcs;

            return await tcs.Task;
        }

        public async Task<string> PostInterrupt()
        {
            using var response = await _httpClient.PostAsync("/interrupt", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> PostClearQueue()
        {
            var payload = new { clear = true }; // Anonymous type
            var content = JsonContent.Create(payload, options: _jsonIgnoreNull);
            var response = await _httpClient.PostAsync("/queue", content);
            response.EnsureSuccessStatusCode();

            return response;
        }

        public async Task<LLMResponse> GeneratePromptWithLLM(LLMRequest request, string clientId)
        {
            var workflow = new
            {
                llm_generator = new
                {
                    inputs = new
                    {
                        text = request.Prompt,
                        random_seed = request.Seed == -1 ? new Random().Next() : request.Seed,
                        model = "Llama-3.2-3B-Instruct-abliterated.Q5_K_M.gguf",
                        max_tokens = 4096,
                        apply_instructions = true,
                        instructions = request.Instructions.Replace("{prompt}", request.Prompt)
                    },
                    class_type = "Searge_LLM_Node",
                    _meta = new { title = "Searge LLM Node" }
                },
                llm_output = new
                {
                    inputs = new
                    {
                        text = new object[] { "llm_generator", 0 }
                    },
                    class_type = "Searge_Output_Node",
                    _meta = new { title = "LLM Output" }
                }
            };

            var payload = new { prompt = workflow, client_id = clientId };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync("llm_payload.json", json);

            using var response = await _httpClient.PostAsJsonAsync("/prompt", payload, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();

            var submit = await response.Content.ReadFromJsonAsync<ComfyUIPromptSubmitResponse>();
            var promptId = Guid.Parse(submit.PromptId);

            var tcs = new TaskCompletionSource<LLMResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingJobs[promptId] = tcs;

            return await tcs.Task;
        }
        #endregion
    }

    /// <summary>
    /// Response from ComfyUI upload endpoint
    /// </summary>
    public class ComfyUIUploadResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("subfolder")]
        public string? Subfolder { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }
}
