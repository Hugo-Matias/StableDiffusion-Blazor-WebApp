using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Services
{
	public class ImageState
	{
		private readonly SDAPIService _api;
		private readonly IOService _io;
		private readonly AppState _appState;
		private PeriodicTimer? _timer;

		public event Action OnChange;

		public GeneratedImagesModel Images { get; set; }
		public GeneratedImagesInfoModel ImagesInfo { get; set; }
		public ProgressModel Progress { get; set; }

		public ImageState(SDAPIService api, IOService io, AppState appState)
		{
			_api = api;
			_io = io;
			_appState = appState;

			Images = new();
			Progress = new();
		}

		public async Task GetTxt2Images(Txt2ImgParametersModel param)
		{
			StartProgressChecker();

			Images = await _api.PostTxt2Img(param);

			StopProgressChecker();

			SerializeInfo();
			NotifyStateChanged();
		}
		public void SaveImages(Outdir outdir)
		{
			DirectoryInfo saveDir = _io.CreateDirectory(_appState.GetCurrentSaveFolder(outdir));

			int fileIndex = _appState.GetFileIndex(saveDir.FullName);
			_appState.CurrentSeed = ImagesInfo.Seed;

			for (int i = 0; i < Images.Images.Length; i++)
			{
				fileIndex++;

				string fullpath = GetImagePath(saveDir.FullName, fileIndex);
				string extension = _appState.Options.SamplesFormat.ToLowerInvariant();

				_io.SaveFileToDisk($"{fullpath}.{extension}", Convert.FromBase64String(Images.Images[i]));

				if (_appState.Options.SaveTxt)
				{
					_io.SaveTextToDisk($"{fullpath}.txt", ImagesInfo.InfoTexts[i]);
				}

				_appState.CurrentSeed++;
			}
		}

		private string GetImagePath(string path, int fileIndex)
		{
			string infoname = _appState.ConvertPathPattern(_appState.Options.FilenamePatternSamples);
			string filename = $"{fileIndex.ToString().PadLeft(5, '0')}-{infoname}";
			return Path.Combine(path, filename);
		}

		private void WriteInfoTxt()
		{

		}

		private void SerializeInfo() => ImagesInfo = JsonSerializer.Deserialize<GeneratedImagesInfoModel>(Images.Info);

		private async void StartProgressChecker()
		{
			_timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

			while (await _timer.WaitForNextTickAsync())
			{
				Progress = await _api.GetProgress();
				NotifyStateChanged();
			}
		}

		private void StopProgressChecker()
		{
			_timer?.Dispose();
			Progress = new();
		}

		private void NotifyStateChanged() => OnChange?.Invoke();
	}
}
