using BlazorWebApp.Services;
using System.Text.Json;

namespace BlazorWebApp.Data
{
	public class PromptButtonService
	{
		public PromptButtonModel Tags { get; set; }

		private readonly IOService _ioService;

		public PromptButtonService(IOService ioService)
		{
			_ioService = ioService;

			GetPromptButtons();
		}

		public void GetPromptButtons()
		{
			Tags = JsonSerializer.Deserialize<PromptButtonModel>(_ioService.GetJsonAsString("Data/danbooru.json"), new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
		}
	}
}
