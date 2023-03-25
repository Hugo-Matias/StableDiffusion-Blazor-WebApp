using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Services
{
    public class SDAPIService
    {
        private readonly HttpClient _httpClient;

        private readonly JsonSerializerOptions _jsonIgnoreNull;
        private readonly string _sdapiRoute = "sdapi/v1/";
        private readonly string _controlnetRoute = "controlnet/";

        public SDAPIService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _httpClient.BaseAddress = new Uri("http://localhost:7860/");
            _httpClient.Timeout = TimeSpan.FromDays(1);

            _jsonIgnoreNull = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        }

        #region SD WebUI
        public async Task<List<SDModel>> GetSDModels() => await _httpClient.GetFromJsonAsync<List<SDModel>>(_sdapiRoute + "sd-models");

        public async Task<List<Sampler>> GetSamplers() => await _httpClient.GetFromJsonAsync<List<Sampler>>(_sdapiRoute + "samplers");

        public async Task<List<PromptStyle>> GetStyles() => await _httpClient.GetFromJsonAsync<List<PromptStyle>>(_sdapiRoute + "prompt-styles");

        public async Task<List<Upscaler>> GetUpscalers() => await _httpClient.GetFromJsonAsync<List<Upscaler>>(_sdapiRoute + "upscalers");

        public async Task<InferenceProgress> GetProgress() => await _httpClient.GetFromJsonAsync<InferenceProgress>(_sdapiRoute + "progress");

        public async Task<Options> GetOptions() => await _httpClient.GetFromJsonAsync<Options>(_sdapiRoute + "options");
        //public async Task<Options> GetOptions()
        //{
        //    using var response = await _httpClient.GetAsync(_sdapiRoute + "options");
        //    var content = await response.Content.ReadAsStringAsync();
        //    var json = JsonNode.Parse(content);
        //    return await _httpClient.GetFromJsonAsync<Options>(_sdapiRoute + "options");
        //}

        public async Task<CmdFlags> GetCmdFlags() => await _httpClient.GetFromJsonAsync<CmdFlags>(_sdapiRoute + "cmd-flags");

        public async Task<string> PostOptions(Options options)
        {
            using var response = await _httpClient.PostAsJsonAsync(_sdapiRoute + "options", options, _jsonIgnoreNull);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostRefreshModels()
        {
            using var response = await _httpClient.PostAsync(_sdapiRoute + "refresh-checkpoints", null);
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters param)
        {
            var json = JsonSerializer.Serialize(param, new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true });
            await File.WriteAllTextAsync("payload.json", json);
            using var response = await _httpClient.PostAsJsonAsync(_sdapiRoute + "txt2img", param, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GeneratedImages>();
        }

        public async Task<GeneratedImages> PostImg2Img(Img2ImgParameters param)
        {
            using var response = await _httpClient.PostAsJsonAsync(_sdapiRoute + "img2img", param, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GeneratedImages>();
        }

        public async Task<UpscaledImageDto> PostExtraSingle(UpscaleParameters param)
        {
            using var response = await _httpClient.PostAsJsonAsync(_sdapiRoute + "extra-single-image", param, _jsonIgnoreNull);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UpscaledImageDto>();
        }

        public async Task<string> PostInterrupt()
        {
            using var response = await _httpClient.PostAsync(_sdapiRoute + "interrupt", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PostSkip()
        {
            using var response = await _httpClient.PostAsync(_sdapiRoute + "skip", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<bool> CheckWebuiState()
        {
            try
            {
                using var response = await _httpClient.GetAsync(_sdapiRoute + "cmd-flags");
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region ControlNet
        public async Task<List<string>> GetControlNetModels()
        {
            var response = await _httpClient.GetAsync(_controlnetRoute + "model_list");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonNode.Parse(content);
            return json["model_list"].AsArray().Select(n => n.ToString()).ToList();
        }

        public async Task<List<string>> GetControlNetPreprocessors()
        {
            var response = await _httpClient.GetAsync(_controlnetRoute + "module_list");
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonNode.Parse(content);
            return json["module_list"].AsArray().Select(n => n.ToString()).ToList();
        }

        // Deprecated
        //public async Task<GeneratedImages> PostControlNetTxt2Img(Txt2ImgParameters param)
        //{
        //    //var json = JsonSerializer.Serialize(param, new JsonSerializerOptions() { WriteIndented = true });
        //    //File.WriteAllText("txt2img.json", json);
        //    using var response = await _httpClient.PostAsJsonAsync(_controlnetRoute + "txt2img", param, _jsonIgnoreNull);
        //    response.EnsureSuccessStatusCode();
        //    return await response.Content.ReadFromJsonAsync<GeneratedImages>();
        //}

        // Deprecated
        //public async Task<GeneratedImages> PostControlNetImg2Img(Img2ImgParameters param)
        //{
        //    using var response = await _httpClient.PostAsJsonAsync(_controlnetRoute + "img2img", param, _jsonIgnoreNull);
        //    response.EnsureSuccessStatusCode();
        //    return await response.Content.ReadFromJsonAsync<GeneratedImages>();
        //}
        #endregion
    }
}
