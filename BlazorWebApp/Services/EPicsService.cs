using HtmlAgilityPack;

namespace BlazorWebApp.Services
{
    public class EPicsService
    {
        private readonly HttpClient _httpClient;

        public EPicsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://eropics.to/");
        }

        public async Task GetSets()
        {
            var response = await _httpClient.GetAsync(_httpClient.BaseAddress);
            response.EnsureSuccessStatusCode();
            var data = await response.Content.ReadAsStringAsync();
            HtmlDocument doc = new();
            doc.LoadHtml(data);

            var setObjects = doc.DocumentNode.SelectSingleNode("//main/div");
        }
    }
}
