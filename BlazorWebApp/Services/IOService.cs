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
	}
}
