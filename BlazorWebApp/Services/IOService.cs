using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
	public class IOService
	{
		public string GetJsonAsString(string path) => new string(File.ReadAllText(path));

		public List<string> GetFilesFromPath(string path)
		{
			if (!Directory.Exists(path)) return new List<string>();

			try
			{
				var di = new DirectoryInfo(path);

				var files = from file in di.GetFiles()
							orderby file.Name
							select file.Name;

				return files.ToList();
			}
			catch (Exception e)
			{

				throw new Exception($"Couldn't read files from directory: {path}", e);
			}
		}

		public byte[] LoadBytesFromFile(string file) => File.ReadAllBytes(file);

		public int GetFileIndex(string path)
		{
			string? lastFile = GetLastSavedFile(path);
			int fileIndex;

			if (lastFile != null)
			{
				string pattern;

				if (lastFile.StartsWith("grid"))
				{
					pattern = @"-(\d+)";
				}
				else
				{
					pattern = @"(\d+)-";
				}

				fileIndex = int.Parse(Regex.Match(lastFile, pattern).Groups[1].Value);
			}
			else
			{
				fileIndex = 0;
			}

			return fileIndex;
		}

		private string? GetLastSavedFile(string path) => GetFilesFromPath(path).LastOrDefault();

		public async Task SaveFileToDisk(string path, byte[] data) => await File.WriteAllBytesAsync(path, data);

		public async Task SaveTextToDisk(string path, string content) => await File.WriteAllTextAsync(path, content);

		public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
	}
}
