using BlazorWebApp.Data.Dtos.ComfyUI;
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
    public class ComfyUIService : IComfyUIService
    {
        private readonly IWorkflowService _workflow;
        private readonly IIOService _io;
        private readonly ILogger<ComfyUIService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ComfyUIEventBus _bus;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonIgnoreNull;
        private readonly ConcurrentDictionary<Guid, object> _pendingJobs = new();
        private readonly string _comfyOutputsPath;
        private readonly string _comfyInputsPath;

        // Track uploaded images for cleanup: promptId -> list of uploaded filenames
        private readonly ConcurrentDictionary<Guid, List<string>> _uploadedImages = new();

        // Cache of image hashes to filenames to avoid re-uploading identical images
        private readonly ConcurrentDictionary<string, string> _imageHashCache = new();

        // Supported video extensions
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".gif", ".avi", ".mov", ".mkv"
        };

        public ComfyUIService(HttpClient httpClient, ComfyUIEventBus bus, IConfiguration configuration, IWorkflowService workflow, IIOService io, ILogger<ComfyUIService> logger)
        {
            _httpClient = httpClient;
            _bus = bus;
            _configuration = configuration;
            _workflow = workflow;
            _io = io;
            _logger = logger;
            _comfyOutputsPath = ResolveComfyPath("ComfyUI:OutputsPath");
            _comfyInputsPath = ResolveComfyPath("ComfyUI:InputsPath");
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

        private string ResolveComfyPath(string key)
        {
            var configured = _configuration[key];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return string.Empty;
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
            try
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
                    var filepath = Path.Combine(_comfyOutputsPath, file);
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
            finally
            {
                // Cleanup uploaded input images after job completion
                await CleanupUploadedImagesAsync(promptId);
            }
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
                    var filepath = Path.Combine(_comfyOutputsPath, file);

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

        public async Task<List<string>> GetVAEModels() => await GetModels("vae", m => m);

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

        /// <inheritdoc />
        // Resolve loader folders through /object_info so we always read the same combo
        // ComfyUI exposes for the node, not a hardcoded /models/<folder> guess.
        public Task<List<string>> GetUpscaleModels()
            => GetNodeInputOptionsAsync("UpscaleModelLoader", "model_name");

        /// <inheritdoc />
        public Task<List<string>> GetLatentUpscaleModels()
            => GetNodeInputOptionsAsync("LatentUpscaleModelLoader", "model_name");

        /// <inheritdoc />
        public Task<List<string>> GetControlNetModels()
            => GetNodeInputOptionsAsync("ControlNetLoader", "control_net_name");

        /// <inheritdoc />
        public async Task<List<string>> GetNodeInputOptionsAsync(string classType, string inputName)
        {
            return await GetNodeInputOptions<string>(classType, inputName, name => name);
        }

        private async Task<List<T>> GetNodeInputOptions<T>(string node, string inputName, Func<string, T> mapFunc)
        {
            var response = await _httpClient.GetAsync($"/object_info/{node}");
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var root = doc.RootElement;

            // Navigate: root[node]["input"]["required"][inputName]
            if (!root.TryGetProperty(node, out var nodeElement) ||
                !nodeElement.TryGetProperty("input", out var inputNode))
            {
                _logger.LogWarning("Node {Node} or input section not found", node);
                return new List<T>();
            }

            // Check both required and optional sections
            JsonElement? fieldNode = null;

            if (inputNode.TryGetProperty("required", out var requiredNode) &&
                requiredNode.TryGetProperty(inputName, out var reqField))
            {
                fieldNode = reqField;
            }
            else if (inputNode.TryGetProperty("optional", out var optionalNode) &&
                     optionalNode.TryGetProperty(inputName, out var optField))
            {
                fieldNode = optField;
            }

            if (fieldNode == null || fieldNode.Value.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Input {InputName} not found in node {Node}", inputName, node);
                return new List<T>();
            }

            var field = fieldNode.Value;

            // Format 1: Direct array of options - [["option1", "option2", ...], {...}]
            // First element is an array of strings
            if (field.GetArrayLength() >= 1 && field[0].ValueKind == JsonValueKind.Array)
            {
                return field[0]
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => mapFunc(e.GetString()!))
                    .ToList();
            }

            // Format 2: COMBO type - ["COMBO", {"options": ["option1", ...], ...}]
            // First element is "COMBO" string, second is object with options
            if (field.GetArrayLength() >= 2 &&
                field[0].ValueKind == JsonValueKind.String &&
                field[0].GetString() == "COMBO" &&
                field[1].ValueKind == JsonValueKind.Object &&
                field[1].TryGetProperty("options", out var optionsEl) &&
                optionsEl.ValueKind == JsonValueKind.Array)
            {
                return optionsEl
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => mapFunc(e.GetString()!))
                    .ToList();
            }

            _logger.LogWarning("Unknown input format for {Node}.{InputName}", node, inputName);
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
            return await UploadInputAsync(base64Data, "image/png", ".png", promptId);
        }

        /// <summary>
        /// Uploads an audio file to ComfyUI's input folder. The extension is preserved
        /// so that <c>LoadAudio</c> and similar nodes can resolve the file.
        /// </summary>
        /// <param name="base64Data">Base64 (with or without data URI prefix) of the audio bytes.</param>
        /// <param name="extensionOrFilename">Optional extension (e.g. <c>.wav</c>) or filename to derive the extension from. Defaults to <c>.wav</c>.</param>
        /// <param name="promptId">Optional prompt id for cleanup tracking.</param>
        public async Task<string> UploadAudioAsync(string base64Data, string? extensionOrFilename = null, Guid? promptId = null)
        {
            var ext = ResolveExtension(extensionOrFilename, ".wav");
            var mime = ext.ToLowerInvariant() switch
            {
                ".mp3" => "audio/mpeg",
                ".flac" => "audio/flac",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                _ => "audio/wav"
            };
            return await UploadInputAsync(base64Data, mime, ext, promptId);
        }

        /// <summary>
        /// Streams an upload directly to ComfyUI's input folder without going through a
        /// base64 round-trip. This is the path used by the browser-side video / large-file
        /// upload flow: bytes flow from <c>fetch()</c> -> minimal API endpoint -> here ->
        /// ComfyUI, never touching the SignalR circuit. Returns the uploaded filename.
        /// </summary>
        public async Task<string> UploadStreamAsync(Stream stream, string originalFilename, string mediaType, Guid? promptId = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            // Buffer to memory so we can hash for dedup. For typical video inputs this is the
            // simplest path; if multi-GB sources become a concern we can switch to streamed
            // hashing + a temp file, but ComfyUI itself loads the full file into memory anyway.
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var hash = ComputeHash(bytes);

            if (_imageHashCache.TryGetValue(hash, out var existingFilename))
            {
                _logger.LogDebug("Stream upload hit cache for {OriginalFilename} -> {Filename}", originalFilename, existingFilename);
                if (promptId.HasValue)
                {
                    _uploadedImages.AddOrUpdate(
                        promptId.Value,
                        new List<string> { existingFilename },
                        (_, list) =>
                        {
                            if (!list.Contains(existingFilename)) list.Add(existingFilename);
                            return list;
                        });
                }
                return existingFilename;
            }

            var ext = ResolveExtension(originalFilename, ".bin");
            var filename = $"blazor_input_{hash[..16]}{ext}";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(mediaType) ? "application/octet-stream" : mediaType);
            content.Add(fileContent, "image", filename);
            content.Add(new StringContent("true"), "overwrite");

            var response = await _httpClient.PostAsync("/upload/image", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ComfyUIUploadResponse>();
            var uploadedFilename = result?.Name ?? filename;

            _imageHashCache[hash] = uploadedFilename;

            if (promptId.HasValue)
            {
                _uploadedImages.AddOrUpdate(
                    promptId.Value,
                    new List<string> { uploadedFilename },
                    (_, list) => { list.Add(uploadedFilename); return list; });
            }

            _logger.LogDebug("Streamed input {OriginalFilename} ({Bytes} bytes) -> {Filename}",
                originalFilename, bytes.Length, uploadedFilename);
            return uploadedFilename;
        }

        private static string ResolveExtension(string? extensionOrFilename, string fallback)
        {
            if (string.IsNullOrWhiteSpace(extensionOrFilename)) return fallback;
            // If a data URI is supplied, sniff the type after "data:".
            if (extensionOrFilename.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var slash = extensionOrFilename.IndexOf('/');
                var semi = extensionOrFilename.IndexOf(';');
                if (slash > 0 && semi > slash)
                {
                    var sub = extensionOrFilename[(slash + 1)..semi];
                    return "." + sub.Replace("mpeg", "mp3");
                }
                return fallback;
            }
            // If a path/filename is supplied, take its extension.
            var dot = extensionOrFilename.LastIndexOf('.');
            if (dot >= 0 && dot < extensionOrFilename.Length - 1)
            {
                return extensionOrFilename[dot..].ToLowerInvariant();
            }
            return fallback;
        }

        /// <summary>
        /// Generic upload to ComfyUI's <c>/upload/image</c> endpoint (which actually accepts any
        /// input file type, with <c>image</c> being the form field name). The extension and MIME
        /// type drive what nodes such as <c>LoadAudio</c> can resolve later.
        /// </summary>
        private async Task<string> UploadInputAsync(string base64Data, string mediaType, string extension, Guid? promptId)
        {
            // Remove data URI prefix if present
            var base64 = base64Data;
            if (base64.Contains(','))
            {
                base64 = base64.Split(',')[1];
            }

            var bytes = Convert.FromBase64String(base64);

            // Generate hash of content for deduplication
            var hash = ComputeHash(bytes);

            // Check if we already uploaded this exact payload
            if (_imageHashCache.TryGetValue(hash, out var existingFilename))
            {
                _logger.LogDebug("Input already uploaded with hash {Hash}, reusing {Filename}", hash, existingFilename);

                // Track this prompt as also referencing the file so a concurrent
                // (e.g. queued) generation does not delete it on cleanup while
                // we still need it. Without this, queued Img2Img runs that share
                // the same input image fail when the first run's cleanup removes
                // the file from ComfyUI's input folder.
                if (promptId.HasValue)
                {
                    _uploadedImages.AddOrUpdate(
                        promptId.Value,
                        new List<string> { existingFilename },
                        (_, list) =>
                        {
                            if (!list.Contains(existingFilename)) list.Add(existingFilename);
                            return list;
                        }
                    );
                }
                return existingFilename;
            }

            // Use hash as filename to ensure uniqueness and deduplication
            var filename = $"blazor_input_{hash[..16]}{extension}";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            content.Add(fileContent, "image", filename);
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

            _logger.LogDebug("Uploaded input {Filename} with hash {Hash}", uploadedFilename, hash);
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
                    // If another in-flight prompt (e.g. a queued generation that
                    // reused the same hash-cached input) still references this
                    // file, skip deletion and cache eviction so its run can
                    // still load the input.
                    var stillInUse = _uploadedImages.Values.Any(list => list.Contains(filename));
                    if (stillInUse)
                    {
                        _logger.LogDebug("Skipping cleanup of input image {Filename} - still referenced by another prompt", filename);
                        continue;
                    }

                    // Remove from hash cache so it can be re-uploaded if needed
                    var hashToRemove = _imageHashCache.FirstOrDefault(kv => kv.Value == filename).Key;
                    if (hashToRemove != null)
                    {
                        _imageHashCache.TryRemove(hashToRemove, out _);
                    }

                    // Delete the file from ComfyUI inputs image folder
                    var inputPath = Path.Combine(_comfyInputsPath, "input", filename);
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


        public async Task<string> PostInterrupt()
        {
            using var response = await _httpClient.PostAsync("/interrupt", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> PostClearQueue()
        {
            // Fetch the current queue first so we know which prompt IDs are pending.
            // ComfyUI does not emit execution_* events for queue items that get
            // cleared before they run, which would otherwise leave their awaiting
            // TaskCompletionSources (and the in-flight counter in ImageService)
            // stuck forever.
            var pendingIds = new List<Guid>();
            try
            {
                using var queueResponse = await _httpClient.GetAsync("/queue");
                if (queueResponse.IsSuccessStatusCode)
                {
                    using var stream = await queueResponse.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    if (doc.RootElement.TryGetProperty("queue_pending", out var pending) &&
                        pending.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in pending.EnumerateArray())
                        {
                            // Each item is a tuple-style array: [number, prompt_id, prompt, extra, outputs]
                            if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 2)
                            {
                                var idElement = item[1];
                                if (idElement.ValueKind == JsonValueKind.String &&
                                    Guid.TryParse(idElement.GetString(), out var pid))
                                {
                                    pendingIds.Add(pid);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read /queue before clearing; awaiting jobs may hang.");
            }

            var payload = new { clear = true }; // Anonymous type
            var content = JsonContent.Create(payload, options: _jsonIgnoreNull);
            var response = await _httpClient.PostAsync("/queue", content);
            response.EnsureSuccessStatusCode();

            // Release any TCS waiting on the dropped prompts so their awaiters exit.
            foreach (var pid in pendingIds)
            {
                if (_pendingJobs.TryRemove(pid, out var obj))
                {
                    TryFailTcs(obj, "Queue cleared by user");
                }
            }

            return response;
        }

        private static void TryFailTcs(object tcsObject, string message)
        {
            // _pendingJobs stores various TaskCompletionSource<T> shapes; use reflection
            // through the non-generic base so we can resolve all of them in one place.
            switch (tcsObject)
            {
                case TaskCompletionSource<GeneratedImages> img:
                    img.TrySetException(new OperationCanceledException(message));
                    break;
                case TaskCompletionSource<GeneratedVideos> vid:
                    vid.TrySetException(new OperationCanceledException(message));
                    break;
                default:
                    // Fallback: invoke TrySetException(Exception) via reflection.
                    var method = tcsObject.GetType().GetMethod("TrySetException", new[] { typeof(Exception) });
                    method?.Invoke(tcsObject, new object[] { new OperationCanceledException(message) });
                    break;
            }
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

        #region New GenerationParameters-based Methods

        /// <inheritdoc />
        public async Task<GeneratedImages> PostGenerationAsync(GenerationParameters parameters, string clientId, Workflow workflow)
        {
            // Generate a temporary prompt ID for tracking uploads
            var tempId = Guid.NewGuid();

            // Upload any source images that are base64 data
            await UploadSourceImagesAsync(parameters, tempId);

            // Compose workflow from template using GenerationParameters
            var workflowJson = _workflow.ComposeWorkflowFromGenerationParameters(workflow, parameters);
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync("payload_generation.json", json);

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

        /// <inheritdoc />
        public async Task<GeneratedVideos> PostVideoGenerationAsync(GenerationParameters parameters, string clientId, Workflow workflow)
        {
            // Generate a temporary prompt ID for tracking uploads
            var tempId = Guid.NewGuid();

            // Upload any source images that are base64 data
            await UploadSourceImagesAsync(parameters, tempId);

            // Compose workflow from template using GenerationParameters
            var workflowJson = _workflow.ComposeWorkflowFromGenerationParameters(workflow, parameters);
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

            await File.WriteAllTextAsync("payload_video_generation.json", json);

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

        /// <summary>
        /// Uploads all source images from GenerationParameters to ComfyUI.
        /// Updates the SourceAsset.Data with the uploaded filename.
        /// </summary>
        private async Task UploadSourceImagesAsync(GenerationParameters parameters, Guid tempId)
        {
            foreach (var source in parameters.Sources.Values)
            {
                var typeKey = (source.Type ?? "image").ToLowerInvariant();
                var isAudio = typeKey == "audio";
                if (source.HasData && !string.IsNullOrEmpty(source.Data))
                {
                    // Check if it's base64 data (not already an uploaded filename)
                    if (source.Data.StartsWith("data:") || source.Data.Length > 260)
                    {
                        string uploadedFilename;
                        if (isAudio)
                        {
                            var ext = source.Filename ?? source.Data; // ResolveExtension sniffs both
                            uploadedFilename = await UploadAudioAsync(source.Data, ext, tempId);
                        }
                        else
                        {
                            uploadedFilename = await UploadImageAsync(source.Data, tempId);
                        }
                        source.Data = uploadedFilename;
                        source.Filename = uploadedFilename;
                        _logger.LogDebug("Uploaded source {Type} from data: {Filename}", typeKey, uploadedFilename);
                    }
                }
                else if (!string.IsNullOrEmpty(source.FilePath) && File.Exists(source.FilePath))
                {
                    // Source has a local file path but no base64 data — read and upload to ComfyUI
                    var base64 = _io.GetBase64FromFile(source.FilePath);
                    if (!string.IsNullOrEmpty(base64))
                    {
                        string uploadedFilename;
                        if (isAudio)
                        {
                            uploadedFilename = await UploadAudioAsync(base64, source.FilePath, tempId);
                        }
                        else
                        {
                            uploadedFilename = await UploadImageAsync(base64, tempId);
                        }
                        source.Filename = uploadedFilename;
                        _logger.LogDebug("Uploaded source {Type} from file path: {FilePath} -> {Filename}", typeKey, source.FilePath, uploadedFilename);
                    }
                }
            }
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
