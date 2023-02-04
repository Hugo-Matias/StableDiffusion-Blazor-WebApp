using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Services
{
	public class SDAPIService
	{
		private readonly HttpClient _httpClient;

		private readonly JsonSerializerOptions _jsonIgnoreNull;

		public SDAPIService(HttpClient httpClient)
		{
			_httpClient = httpClient;

			_httpClient.BaseAddress = new Uri("http://localhost:7860/sdapi/v1/");
			_httpClient.Timeout = TimeSpan.FromDays(1);

			_jsonIgnoreNull = new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
		}

		public async Task<List<SDModelModel>> GetSDModels() => await _httpClient.GetFromJsonAsync<List<SDModelModel>>("sd-models");

		public async Task<List<SamplerModel>> GetSamplers() => await _httpClient.GetFromJsonAsync<List<SamplerModel>>("samplers");

		public async Task<List<PromptStyleModel>> GetStyles() => await _httpClient.GetFromJsonAsync<List<PromptStyleModel>>("prompt-styles");

		public async Task<ProgressModel> GetProgress() => await _httpClient.GetFromJsonAsync<ProgressModel>("progress");

		public async Task<OptionsModel> GetOptions() => await _httpClient.GetFromJsonAsync<OptionsModel>("options");

		public async Task<string> PostOptions(OptionsModel options)
		{
			using var response = await _httpClient.PostAsJsonAsync("options", options, _jsonIgnoreNull);

			return await response.Content.ReadAsStringAsync();
		}

		public async Task<string> PostRefreshModels()
		{
			using var response = await _httpClient.PostAsync("refresh-checkpoints", null);
			return await response.Content.ReadAsStringAsync();
		}

		public async Task<GeneratedImagesModel> PostTxt2Img(Txt2ImgParametersModel param)
		{
			using var response = await _httpClient.PostAsJsonAsync("txt2img", param, _jsonIgnoreNull);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<GeneratedImagesModel>();
		}

		public async Task<GeneratedImagesModel> PostImg2Img(Img2ImgParametersModel param)
		{
			using var response = await _httpClient.PostAsJsonAsync("img2img", param, _jsonIgnoreNull);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<GeneratedImagesModel>();
		}

		public async Task<string> PostInterrupt()
		{
			using var response = await _httpClient.PostAsync("interrupt", null);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync();
		}

		public async Task<string> PostSkip()
		{
			using var response = await _httpClient.PostAsync("skip", null);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync();
		}
	}
}
