using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
	public class ImageService
	{
		private readonly SDAPIService _api;
		private readonly IOService _io;
		private readonly AppState _app;
		private readonly MagickService _magick;
		private readonly ParsingService _parser;
		private readonly DatabaseService _db;
		private PeriodicTimer? _timer;
		private SharedParametersModel _parsingParams;

		public event Action OnChange;

		public ImageService(SDAPIService api, IOService io, AppState app, MagickService magick, ParsingService parser, DatabaseService db)
		{
			_api = api;
			_io = io;
			_app = app;
			_magick = magick;
			_parser = parser;
			_db = db;
		}

		public async Task GetImages(bool isImg2Img = false)
		{
			_app.IsConverging = true;
			StartProgressChecker();

			_app.GridImage = string.Empty;

			// Using a temporary parameters model to parse selected dropdown Styles without writing them to the input fields
			if (isImg2Img)
			{
				_parsingParams = new SharedParametersModel(_app.ParametersImg2Img);
			}
			else
			{
				_parsingParams = _parser.ParseParameters(new SharedParametersModel(_app.ParametersTxt2Img));
				var newParameters = new Txt2ImgParametersModel(_parsingParams);
				newParameters.FirstphaseWidth = _app.ParametersTxt2Img.FirstphaseWidth;
				newParameters.FirstphaseHeight = _app.ParametersTxt2Img.FirstphaseHeight;
				newParameters.DenoisingStrength = _app.ParametersTxt2Img.DenoisingStrength;
				_app.Images = await _api.PostTxt2Img(newParameters);
			}

			_app.SerializeInfo();

			if (_app.Options.SamplesSave)
			{
				if (isImg2Img)
				{
					await SaveImages(Outdir.Img2ImgSamples, Outdir.Img2ImgGrid);
				}
				else
				{
					await SaveImages(Outdir.Txt2ImgSamples, Outdir.Txt2ImgGrid);
				}
			}

			StopProgressChecker();
			_app.IsConverging = false;

			NotifyStateChanged();
		}

		public async Task SaveImages(Outdir outdirSamples, Outdir? outdirGrid = null)
		{
			DirectoryInfo saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirSamples));

			int fileIndex = _io.GetFileIndex(saveDir.FullName);
			_app.CurrentSeed = _app.ImagesInfo.Seed;

			for (int i = 0; i < _app.Images.Images.Count; i++)
			{
				fileIndex++;

				string fullpath = GetImagePath(saveDir.FullName, fileIndex);
				string extension = _app.Options.SamplesFormat.ToLowerInvariant();
				string imagePath = $"{fullpath}.{extension}";

				await _io.SaveFileToDisk(imagePath, Convert.FromBase64String(_app.Images.Images[i]));


				if (_app.Options.SaveTxt)
				{
					var infoPath = $"{fullpath}.txt";
					await _io.SaveText(infoPath, _app.ImagesInfo.InfoTexts[i]);
					await AddImageToDb(imagePath, outdirSamples, infoPath);
				}
				else
					await AddImageToDb(imagePath, outdirSamples);

				_app.CurrentSeed++;
			}

			if (outdirGrid != null && _app.Options.GridSave && (_app.Images.Images.Count > 1 && _app.Options.GridOnlyIfMultiple))
			{
				saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirGrid));
				fileIndex = _io.GetFileIndex(saveDir.FullName) + 1;
				string fullpath = Path.Combine(saveDir.FullName, $"grid-{fileIndex.ToString().PadLeft(4, '0')}");
				string extension = _app.Options.GridFormat.ToLowerInvariant();
				string gridPath = $"{fullpath}.{extension}";

				_app.GridImage = await _magick.SaveGrid(_app.Images.Images, gridPath);

				if (_app.Options.SaveTxt)
				{
					await _io.SaveText($"{fullpath}.txt", _app.ImagesInfo.InfoTexts[0]);
				}
			}
		}

		public string LoadImage(string imagePath) => _io.GetBase64FromFile(imagePath);

		public async Task<string> LoadImageAsync(string imagePath) => await _io.GetBase64FromFileAsync(imagePath);

		private async Task AddImageToDb(string path, Outdir outdir, string infoPath = null)
		{
			Image image = new();

			image.Path = path;
			if (infoPath != null) { image.InfoPath = infoPath; }
			image.Prompt = _parsingParams.Prompt;
			image.NegativePrompt = _parsingParams.NegativePrompt;
			image.SamplerId = _db.GetSampler(_parsingParams.SamplerIndex);
			image.Steps = (int)_parsingParams.Steps;
			image.Seed = (long)_app.CurrentSeed;
			image.CfgScale = (float)_parsingParams.CfgScale;
			image.Width = (int)_parsingParams.Width;
			image.Height = (int)_parsingParams.Height;
			image.ProjectId = _app.CurrentProjectId;

			ModeType mode;
			switch (outdir)
			{
				case Outdir.Txt2ImgSamples:
					mode = ModeType.Txt2Img;
					break;

				case Outdir.Img2ImgSamples:
					mode = ModeType.Img2Img;
					break;

				case Outdir.Extras:
					mode = ModeType.Extras;
					break;

				default:
					mode = ModeType.Txt2Img;
					break;
			}
			image.ModeId = _db.GetMode(mode);

			await _db.AddImage(image);
		}

		private string GetImagePath(string path, int fileIndex)
		{
			string infoname = _app.ConvertPathPattern(_app.Options.FilenamePatternSamples);
			string filename = $"{fileIndex.ToString().PadLeft(5, '0')}-{infoname}";
			return Path.Combine(path, filename);
		}

		private async void StartProgressChecker()
		{
			_timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

			while (await _timer.WaitForNextTickAsync())
			{
				_app.Progress = await _api.GetProgress();
				NotifyStateChanged();
			}
		}

		private void StopProgressChecker()
		{
			_timer?.Dispose();
			_app.Progress = new();
		}

		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}
