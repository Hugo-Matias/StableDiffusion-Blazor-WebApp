using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Services
{
	public class ImageState
	{
		private readonly SDAPIService _api;
		private PeriodicTimer? _timer;

		public event Action OnChange;

		public GeneratedImagesModel Images { get; set; }
		public GeneratedImagesInfoModel ImagesInfo { get; set; }
		public ProgressModel Progress { get; set; }

		public ImageState(SDAPIService api)
		{
			_api = api;
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
