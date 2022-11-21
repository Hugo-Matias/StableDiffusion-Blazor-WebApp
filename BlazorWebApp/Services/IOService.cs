namespace BlazorWebApp.Services
{
	public class IOService
	{
		//private readonly HttpClient httpClient;

		//public IOService(HttpClient httpClient)
		//{
		//	this.httpClient = httpClient;
		//	this.httpClient.BaseAddress = new Uri("http://localhost:7016/");
		//}

		//public async Task<JSONDataCategoryModel> GetJsonData() => await this.httpClient.GetFromJsonAsync<JSONDataCategoryModel>("Data/danbooru.json");

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

		public void SaveFileToDisk(string path, byte[] data) => File.WriteAllBytes(path, data);
		public void SaveTextToDisk(string path, string content) => File.WriteAllText(path, content);

		public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
	}
}
