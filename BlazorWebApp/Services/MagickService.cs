﻿using ImageMagick;

namespace BlazorWebApp.Services
{
	public class MagickService
	{
		private readonly AppState _app;

		public MagickService(AppState app)
		{
			MagickNET.Initialize();
			_app = app;
		}

		public async Task<string> SaveGrid(List<string> data, string path)
		{
			using (var images = new MagickImageCollection())
			{
				foreach (var image in data)
				{
					images.Add(new MagickImage(Convert.FromBase64String(image)));
				}

				var width = images[0].Width;
				var height = images[0].Height;

				using (var result = images.Montage(new MontageSettings() { Geometry = new MagickGeometry(width, height), BackgroundColor = MagickColors.Black }))
				{
					await result.WriteAsync(path);

					return result.ToBase64(MagickFormat.Png);
				}
			}
		}

		#region Examples
		public MagickImageInfo ReadInfoFromBytes(byte[] bytes) => new MagickImageInfo(bytes);

		public void ResizeAndSave(string path, byte[] data, int size)
		{
			using (var image = new MagickImage(data))
			{
				var sizeGeom = new MagickGeometry(size);

				// This will resize the image to a fixed size without maintaining the aspect ratio.
				// Normally an image will be resized to fit inside the specified size.
				sizeGeom.IgnoreAspectRatio = true;

				image.Resize(sizeGeom);

				image.Write(path);
			}
		}
		#endregion
	}
}
