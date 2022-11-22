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
		private PeriodicTimer? _timer;

		public event Action OnChange;

		public ImageService(SDAPIService api, IOService io, AppState app, MagickService magick, ParsingService parser)
		{
			_api = api;
			_io = io;
			_app = app;
			_magick = magick;
			_parser = parser;
		}

		public async Task GetTxt2Images()
		{
			StartProgressChecker();

			var newParams = new Txt2ImgParametersModel(_app.Parameters);
			_app.Images = await _api.PostTxt2Img(_parser.ParseParameters(newParams));

			StopProgressChecker();

			_app.SerializeInfo();
			NotifyStateChanged();
		}
		public async Task SaveImages(Outdir outdirSamples, Outdir? outdirGrid = null)
		{
			DirectoryInfo saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirSamples));

			int fileIndex = _io.GetFileIndex(saveDir.FullName);
			_app.CurrentSeed = _app.ImagesInfo.Seed;

			for (int i = 0; i < _app.Images.Images.Length; i++)
			{
				fileIndex++;

				string fullpath = GetImagePath(saveDir.FullName, fileIndex);
				string extension = _app.Options.SamplesFormat.ToLowerInvariant();

				await _io.SaveFileToDisk($"{fullpath}.{extension}", Convert.FromBase64String(_app.Images.Images[i]));

				if (_app.Options.SaveTxt)
				{
					await _io.SaveTextToDisk($"{fullpath}.txt", _app.ImagesInfo.InfoTexts[i]);
				}

				_app.CurrentSeed++;
			}

			if (outdirGrid != null && _app.Options.GridSave && (_app.Images.Images.Length > 1 && _app.Options.GridOnlyIfMultiple))
			{
				saveDir = _io.CreateDirectory(_app.GetCurrentSaveFolder(outdirGrid));
				fileIndex = _io.GetFileIndex(saveDir.FullName) + 1;
				string fullpath = Path.Combine(saveDir.FullName, $"grid-{fileIndex.ToString().PadLeft(4, '0')}");
				string extension = _app.Options.GridFormat.ToLowerInvariant();

				await _magick.SaveGrid(_app.Images.Images, $"{fullpath}.{extension}");

				if (_app.Options.SaveTxt)
				{
					await _io.SaveTextToDisk($"{fullpath}.txt", _app.ImagesInfo.InfoTexts[0]);
				}
			}
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
