namespace MauiBlazor
{
	internal class FileService
	{
		public async Task<string> GetFile(string filePath)
		{
			if (File.Exists(filePath))
			{
				using Stream fileStream = File.OpenRead(filePath);
				using StreamReader reader = new StreamReader(fileStream);

				var bytes = default(byte[]);
				using (var ms = new MemoryStream())
				{
					await reader.BaseStream.CopyToAsync(ms);
					bytes = ms.ToArray();
				}

				return Convert.ToBase64String(bytes);
			}

			return null;
		}
	}
}
