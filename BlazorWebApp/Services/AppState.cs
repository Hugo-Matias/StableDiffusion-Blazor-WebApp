using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
	public enum Outdir { Txt2ImgSamples, Txt2ImgGrid, Img2ImgSamples, Img2ImgGrid, Extras }

	public class AppState
	{
		private readonly string _settingsFile = "BlazorDiffusion.json";
		private readonly SDAPIService _api;
		private readonly DatabaseService _db;
		private readonly IOService _io;
		private bool _isConverging;

		public event Action OnSDModelsChange;
		public event Action OnOptionsChange;
		public event Action OnStyleChange;
		public event Action OnConverging;
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
		public Txt2ImgParametersModel ParametersTxt2Img { get; set; }
		public Img2ImgParametersModel ParametersImg2Img { get; set; }
		public long? CurrentSeed { get; set; }
		public string? Style1 { get; set; }
		public string? Style2 { get; set; }
		public int CurrentProjectId { get; set; }
		public List<Project>? Projects { get; set; }
		public bool IsConverging
		{
			get => _isConverging; set
			{
				_isConverging = value;
				OnConverging?.Invoke();
			}
		}

		public AppState(SDAPIService api, DatabaseService db, IOService io)
		{
			_api = api;
			_db = db;
			_io = io;

			LoadSettings();

			Images = new();
			Progress = new();
			Projects = new();

			var defaultParameters = new SharedParametersModel()
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

			ParametersTxt2Img = new Txt2ImgParametersModel(defaultParameters)
			{
				FirstphaseWidth = Settings.Txt2ImgSettings.HighresSettings.FirstPassWidthDefaultValue,
				FirstphaseHeight = Settings.Txt2ImgSettings.HighresSettings.FirstPassHeightDefaultValue,
				DenoisingStrength = Settings.Txt2ImgSettings.HighresSettings.DenoisingDefaultValue,
			};

			ParametersImg2Img = new Img2ImgParametersModel(defaultParameters);
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
			ParametersTxt2Img.Prompt = image.Prompt;
			ParametersTxt2Img.NegativePrompt = image.NegativePrompt;
			ParametersTxt2Img.SamplerIndex = _db.GetSampler(image.SamplerId);
			ParametersTxt2Img.Steps = image.Steps;
			ParametersTxt2Img.Seed = image.Seed;
			ParametersTxt2Img.CfgScale = image.CfgScale;
			ParametersTxt2Img.Width = image.Width;
			ParametersTxt2Img.Height = image.Height;
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
					return ParametersTxt2Img.SamplerIndex;

				case "[seed]":
					return CurrentSeed.ToString();

				case "[steps]":
					return ParametersTxt2Img.Steps.ToString();

				case "[cfg]":
					return ParametersTxt2Img.CfgScale.ToString();

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

		public async void LoadSettings()
		{
			var json = await _io.LoadText(_settingsFile);

			if (json != null) { Settings = JsonSerializer.Deserialize<AppSettingsModel>(json); }
			else { Settings = new(); SaveSettings(); }
		}

		public async void SaveSettings()
		{
			var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions() { WriteIndented = true });
			await _io.SaveText(_settingsFile, json);
		}
	}
}
