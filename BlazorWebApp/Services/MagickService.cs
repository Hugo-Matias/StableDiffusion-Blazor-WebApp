using ImageMagick;

namespace BlazorWebApp.Services
{
	public class MagickService
	{
		public MagickService()
		{
			MagickNET.Initialize();
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
