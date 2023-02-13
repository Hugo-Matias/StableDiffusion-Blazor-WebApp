using BlazorWebApp.Data.Dtos;
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

		public async Task<List<SDModel>> GetSDModels() => await _httpClient.GetFromJsonAsync<List<SDModel>>("sd-models");

		public async Task<List<Sampler>> GetSamplers() => await _httpClient.GetFromJsonAsync<List<Sampler>>("samplers");

		public async Task<List<PromptStyle>> GetStyles() => await _httpClient.GetFromJsonAsync<List<PromptStyle>>("prompt-styles");

		public async Task<List<Upscaler>> GetUpscalers() => await _httpClient.GetFromJsonAsync<List<Upscaler>>("upscalers");

		public async Task<Progress> GetProgress() => await _httpClient.GetFromJsonAsync<Progress>("progress");

		public async Task<Options> GetOptions() => await _httpClient.GetFromJsonAsync<Options>("options");

		public async Task<string> PostOptions(Options options)
		{
			using var response = await _httpClient.PostAsJsonAsync("options", options, _jsonIgnoreNull);

			return await response.Content.ReadAsStringAsync();
		}

		public async Task<string> PostRefreshModels()
		{
			using var response = await _httpClient.PostAsync("refresh-checkpoints", null);
			return await response.Content.ReadAsStringAsync();
		}

		public async Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters param)
		{
			using var response = await _httpClient.PostAsJsonAsync("txt2img", param, _jsonIgnoreNull);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<GeneratedImages>();
		}

		public async Task<GeneratedImages> PostImg2Img(Img2ImgParameters param)
		{
			using var response = await _httpClient.PostAsJsonAsync("img2img", param, _jsonIgnoreNull);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<GeneratedImages>();
		}

		public async Task<UpscaledImageDto> PostExtraSingle(UpscaleParameters param)
		{
			using var response = await _httpClient.PostAsJsonAsync("extra-single-image", param, _jsonIgnoreNull);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadFromJsonAsync<UpscaledImageDto>();
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
