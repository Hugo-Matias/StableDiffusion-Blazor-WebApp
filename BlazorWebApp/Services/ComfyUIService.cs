using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Comfy = BlazorWebApp.Data.Dtos.ComfyUI.Workflow;

namespace BlazorWebApp.Services
{
    public class ComfyUIService
    {
        private readonly HttpClient _comfyUIClient;
        private readonly HttpClient _comfyUIAPIClient;
        private readonly JsonSerializerOptions _jsonIgnoreNull;

        public ComfyUIService(IHttpClientFactory httpClientFactory)
        {
            _comfyUIClient = httpClientFactory.CreateClient("ComfyUI");
            _comfyUIAPIClient = httpClientFactory.CreateClient("ComfyUIAPI");
            _jsonIgnoreNull = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        }

        #region GET
        public async Task<bool> GetHealth()
        {
            var health = await _comfyUIAPIClient.GetFromJsonAsync<Health>("/health");
            return health != null && health.Status == "healthy";
        }

        public async Task<List<SDModel>> GetModels()
        {
            var models = await _comfyUIClient.GetFromJsonAsync<List<string>>("/models/checkpoints");
            return models?.Select(m => new SDModel { Title = m, Model_name = m }).ToList() ?? new List<SDModel>();
        }

        public async Task<List<string>> GetVAEs() => await _comfyUIClient.GetFromJsonAsync<List<string>>("/models/vae") ?? new List<string>();

        public async Task<List<Models.Sampler>> GetSamplers() => await GetNodeInputOptions<Models.Sampler>("ClownsharKSampler_Beta", "sampler_name", name => new Models.Sampler { Name = name });
        public async Task<List<Models.Scheduler>> GetSchedulers() => await GetNodeInputOptions<Models.Scheduler>("ClownsharKSampler_Beta", "scheduler", name => new Models.Scheduler { Name = name });

        private async Task<List<T>> GetNodeInputOptions<T>(string node, string inputName, Func<string, T> mapFunc)
        {
            var response = await _comfyUIClient.GetAsync($"/object_info/{node}");
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
        #endregion

        #region POST
        public async Task<GeneratedImages> PostTxt2Img(Models.Txt2ImgParameters param, string checkpoint, string vae)
        {
            var comfyParam = param.ToSDTxt2ImgParameters(checkpoint, vae);
            var payload = new { input = comfyParam };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true });
            await File.WriteAllTextAsync("payload.json", json);
            using var response = await _comfyUIAPIClient.PostAsJsonAsync("/workflow/sd15/text2img", payload, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<PromptResponse<Comfy.sd.Txt2ImgParameters>>();
            return content.ToGeneratedImages();
        }

        public async Task<string> PostInterrupt()
        {
            using var response = await _comfyUIClient.PostAsync("/interrupt", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<HttpResponseMessage> PostClearQueue()
        {
            var payload = new { clear = true }; // Anonymous type
            var content = JsonContent.Create(payload, options: _jsonIgnoreNull);
            var response = await _comfyUIClient.PostAsync("/queue", content);
            response.EnsureSuccessStatusCode();

            return response;
        }
        #endregion
    }
}
