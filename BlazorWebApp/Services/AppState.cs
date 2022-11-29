using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
	public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras }

	public class AppState
	{
		private readonly SDAPIService _api;
		private readonly DatabaseService _db;

		public event Action OnSDModelsChange;
		public event Action OnOptionsChange;
		public event Action OnStyleChange;
		public event Func<Task> OnProjectChange;

		public GeneratedImagesModel Images { get; set; }
		public GeneratedImagesInfoModel ImagesInfo { get; set; }
		public string? GridImage { get; set; }
		public ProgressModel Progress { get; set; }
		public List<SDModelModel> SDModels { get; set; }
		public List<SamplerModel> Samplers { get; set; }
		public List<PromptStyleModel> Styles { get; set; }
		public OptionsModel Options { get; set; }
		public AppSettingsModel Settings { get; set; }
		public Txt2ImgParametersModel Parameters { get; set; }
		public long? CurrentSeed { get; set; }
		public string? Style1 { get; set; }
		public string? Style2 { get; set; }
		public bool IsConverging { get; set; }
		public int CurrentProjectId { get; set; }
		public List<Project>? Projects { get; set; }

		public AppState(SDAPIService api, DatabaseService db)
		{
			_api = api;
			_db = db;

			Images = new();
			Progress = new();
			Settings = new();
			Projects = new();
			Parameters = new()
			{
				Steps = Settings.StepsDefaultValue,
				SamplerIndex = Settings.SamplerDefault,
				Seed = Settings.SeedDefaultValue,
				CfgScale = Settings.CfgScaleDefaultValue,
				Width = Settings.ResolutionDefaultValue,
				Height = Settings.ResolutionDefaultValue,
				NIter = Settings.BatchCountDefaultValue,
				BatchSize = Settings.BatchSizeDefaultValue,
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

		public async Task GetStyles()
		{
			Styles = await _api.GetStyles();

			Style1 = Styles[0].Name;
			Style2 = Styles[0].Name;
		}

		public void SetCurrentProjectId(int id)
		{
			CurrentProjectId = id;
			OnProjectChange?.Invoke();
		}

		public void ResetStyles()
		{
			Style1 = "None";
			Style2 = "None";
			OnStyleChange?.Invoke();
		}

		public async Task GetSamplers() => Samplers = await _api.GetSamplers();

		public void LoadImageInfoParameters(Image image)
		{
			Parameters.Prompt = image.Prompt;
			Parameters.NegativePrompt = image.NegativePrompt;
			Parameters.SamplerIndex = _db.GetSampler(image.SamplerId);
			Parameters.Steps = image.Steps;
			Parameters.Seed = image.Seed;
			Parameters.CfgScale = image.CfgScale;
			Parameters.Width = image.Width;
			Parameters.Height = image.Height;
		}

		public string GetCurrentSaveFolder(Outdir? outdir)
		{
			string path;

			switch (outdir)
			{
				case Outdir.Txt2ImgSamples:
					path = Options.OutdirSamplesTxt2Img;
					break;

				case Outdir.Txt2ImgGrid:
					path = Options.OutdirGridTxt2Img;
					break;

				case Outdir.Img2ImgSamples:
					path = Options.OutdirSamplesImg2Img;
					break;

				case Outdir.Img2ImgGrid:
					path = Options.OutdirGridImg2Img;
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

		public string ConvertPathPattern(string pattern)
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
					return CurrentSeed.ToString();

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

		public async Task<string> PostOptions(OptionsModel options) => await _api.PostOptions(options);

		public void SerializeInfo() => ImagesInfo = JsonSerializer.Deserialize<GeneratedImagesInfoModel>(Images.Info);
	}
}
