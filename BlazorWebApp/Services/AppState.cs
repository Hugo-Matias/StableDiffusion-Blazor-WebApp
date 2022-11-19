using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
	public class AppState
	{
		private readonly SDAPIService _api;

		public event Action OnSDModelsChange;
		public event Action OnOptionsChange;

		public List<SDModelModel> SDModels { get; set; }
		public List<SamplerModel> Samplers { get; set; }
		public OptionsModel Options { get; set; }
		public AppSettingsModel Settings { get; set; }
		public Txt2ImgParametersModel Parameters { get; set; }

		public AppState(SDAPIService api)
		{
			_api = api;

			Settings = new();
			Parameters = new()
			{
				Steps = Settings.StepsDefaultValue,
				SamplerIndex = Settings.SamplerDefault
			};
		}

		public async Task GetSDModels()
		{
			SDModels = await _api.GetSDModels();

			OnSDModelsChange?.Invoke();
		}

		public async Task GetOptions()
		{
			Options = await _api.GetOptions();

			OnOptionsChange?.Invoke();
		}

		public async Task GetSamplers() => Samplers = await _api.GetSamplers();

		public async Task<string> PostOptions(OptionsModel options) => await _api.PostOptions(options);
	}
}
