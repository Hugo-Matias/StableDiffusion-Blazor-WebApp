using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Services
{
	public class SDAPIService
	{
		private readonly HttpClient _httpClient;

		public SDAPIService(HttpClient httpClient)
		{
			_httpClient = httpClient;

			_httpClient.BaseAddress = new Uri("http://localhost:7860/sdapi/v1/");
		}

		public async Task<List<SDModelModel>> GetSDModels() => await _httpClient.GetFromJsonAsync<List<SDModelModel>>("sd-models");

		public async Task<ProgressModel> GetProgress() => await _httpClient.GetFromJsonAsync<ProgressModel>("progress");

		public async Task<GeneratedImagesModel> PostTxt2Img(Txt2ImgParametersModel param)
		{
			using var response = await _httpClient.PostAsJsonAsync("txt2img", param, new JsonSerializerOptions() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<GeneratedImagesModel>();
		}
	}
}
