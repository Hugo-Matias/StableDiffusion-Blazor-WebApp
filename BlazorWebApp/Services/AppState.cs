using BlazorWebApp.Models;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
	public enum Outdir { Txt2Img, Img2Img, Extras }

	public class AppState
	{
		private readonly SDAPIService _api;
		private readonly IOService _io;

		public event Action OnSDModelsChange;
		public event Action OnOptionsChange;

		public List<SDModelModel> SDModels { get; set; }
		public List<SamplerModel> Samplers { get; set; }
		public OptionsModel Options { get; set; }
		public AppSettingsModel Settings { get; set; }
		public Txt2ImgParametersModel Parameters { get; set; }

		public AppState(SDAPIService api, IOService io)
		{
			_api = api;
			_io = io;

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

		public string? GetLastSavedFile(Outdir outdir) => _io.GetFilesFromPath(GetCurrentSaveFolder(outdir)).LastOrDefault();

		private string GetCurrentSaveFolder(Outdir outdir)
		{
			string path;

			switch (outdir)
			{
				case Outdir.Txt2Img:
					path = Options.OutdirSamplesTxt2Img;
					break;

				case Outdir.Img2Img:
					path = Options.OutdirSamplesImg2Img;
					break;

				case Outdir.Extras:
					path = Options.OutdirSamplesExtras;
					break;

				default:
					path = string.Empty;
					break;
			}

			return Path.Combine(path, ConvertPathPattern(Options.FilenamePatternDir));
		}

		private string ConvertPathPattern(string pattern)
		{
			var rg = new Regex(@"(\[.+?\])");

			return rg.Replace(pattern, (t) => ConvertPathTag(t.Value));
		}

		private string ConvertPathTag(string tag)
		{
			switch (tag)
			{
				case "[model_hash]":
					return GetModelHash(Options.SDModelCheckpoint);

				case "[sampler]":
					return Parameters.SamplerIndex;

				case "[seed]":
					return Parameters.Seed.ToString();

				case "[steps]":
					return Parameters.Steps.ToString();

				case "[cfg]":
					return Parameters.CfgScale.ToString();

				default:
					return "";
			}
		}

		private string GetModelHash(string modelName)
		{
			foreach (var model in SDModels)
			{
				if (model.Title == modelName)
				{
					return model.Hash;
				}
			}

			return string.Empty;
		}

		public async Task GetSamplers() => Samplers = await _api.GetSamplers();

		public async Task<string> PostOptions(OptionsModel options) => await _api.PostOptions(options);
	}
}
