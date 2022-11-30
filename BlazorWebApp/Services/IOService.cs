using BlazorWebApp.Models;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
	public class IOService
	{
		public string GetJsonAsString(string path) => new string(File.ReadAllText(path));

		public IOrderedEnumerable<FileInfo>? GetFilesFromPath(string path)
		{
			if (!Directory.Exists(path)) return null;

			try
			{
				var di = new DirectoryInfo(path);

				// Order by name is important to get txt/img file pairs
				var files = from file in di.GetFiles()
							orderby file.Name
							select file;

				return files;
			}
			catch (Exception e)
			{

				throw new Exception($"Couldn't read files from directory: {path}\n", e);
			}
		}

		public async Task<List<ImageInfoModel>?> GetImagesFromPath(string path)
		{
			if (!Directory.Exists(path)) return null;

			var files = GetFilesFromPath(path);
			var images = new List<ImageInfoModel>();
			var currentFileName = string.Empty;
			ImageInfoModel currentImage = null;

			foreach (var file in files)
			{
				var fileName = file.Name.Replace(file.Extension, "");

				if (currentFileName != fileName)
				{
					currentFileName = fileName;
					currentImage = new();
				}

				if (file.Extension == ".txt")
				{
					currentImage.InfoString = await LoadTextLines(file.FullName);
				}
				else
				{
					try
					{
						currentImage.Image = await GetBase64FromFileAsync(file.FullName);
					}
					catch (Exception e)
					{
						throw new Exception($"Couldn't get file: {file.Name}\n", e);
					}
				}

				if (currentImage.Image != null && currentImage.InfoString != null)
					images.Add(currentImage);
			}

			return images;
		}

		public string GetBase64FromFile(string path)
		{
			if (!File.Exists(path)) return string.Empty;

			var bytes = File.ReadAllBytes(path);

			return Convert.ToBase64String(bytes);
		}

		public async Task<string?> GetBase64FromFileAsync(string path)
		{
			if (!File.Exists(path)) return null;

			//var bytes = await GetByteArray(path);
			var bytes = await File.ReadAllBytesAsync(path);

			return Convert.ToBase64String(bytes);
		}

		public int GetFileIndex(string path)
		{
			FileInfo? lastFile = GetLastSavedFile(path);
			int fileIndex;

			if (lastFile != null)
			{
				string pattern;

				if (lastFile.Name.StartsWith("grid"))
				{
					pattern = @"-(\d+)";
				}
				else
				{
					pattern = @"(\d+)-";
				}

				fileIndex = int.Parse(Regex.Match(lastFile.Name, pattern).Groups[1].Value);
			}
			else
			{
				fileIndex = 0;
			}

			return fileIndex;
		}

		private FileInfo? GetLastSavedFile(string path) => GetFilesFromPath(path).LastOrDefault();

		public async Task SaveFileToDisk(string path, byte[] data) => await File.WriteAllBytesAsync(path, data);

		public async Task<string[]?> LoadTextLines(string path)
		{
			if (!File.Exists(path)) return null;

			return File.ReadAllLines(path);
		}

		public async Task<string?> LoadText(string path)
		{
			if (!File.Exists(path)) return null;

			return File.ReadAllText(path);
		}

		public async Task SaveText(string path, string content) => await File.WriteAllTextAsync(path, content);

		public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
	}
}
