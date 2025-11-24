using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Collections.Concurrent;
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

            _bus.ExecutionFailed += (promptId, error) =>
            {
                if (_pendingJobs.TryRemove(promptId, out var obj) && obj is TaskCompletionSource<GeneratedImages> tcs)
                    tcs.SetException(new Exception(error));
            };

        }


        private async Task HandleExecutionSucceededAsync(Guid promptId)
        {
            var files = await GetFilenameFromHistory(promptId);
            if (files == null || files.Count == 0)
            {
                _logger.LogError("File not found!");
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

            if (_pendingJobs.TryRemove(promptId, out var obj) && obj is TaskCompletionSource<GeneratedImages> tcs)
                tcs.SetResult(images);
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
                OutdirSamplesExtras = Path.Combine(_configuration["OutputDir"], "Extras")
            };

            return options;
        }

        #region GET
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

        public async Task<List<string>> GetLoras() => await GetModels("loras", m => Regex.Replace(m, @"\.[^\.]+$", ""));

        public async Task<List<string>> SearchLoras(string search)
        {
            Func<string, bool> filter = null;
            if (!string.IsNullOrEmpty(search)) filter = m => m.ToLower().Contains(search.ToLower());
            return await GetModels("loras", m => Regex.Replace(m, @"\.[^\.]+$", ""), filter);
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
        #endregion

        #region POST
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

            // Deserialize the JSON string to an object
            var workflowObject = JsonSerializer.Deserialize<object>(workflowJson);

            var payload = new { prompt = workflowObject, client_id = clientId };
            return await PostPromptAsync<GeneratedImages>(payload);
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
        #endregion
    }
}
