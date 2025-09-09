using BlazorWebApp.Data.Dtos;
using System.Net.Http.Headers;
using System.Text;

namespace BlazorWebApp.Services
{
    public class DanbooruService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string[] _videoExtensions = new[] { "mp4", "webm" };
        private readonly string[] _imageExtensions = new[] { "avif", "jpg", "png", "gif" };
        private readonly string[] _otherExtentions = new[] { "swf", "zip" };

        public DanbooruService(HttpClient httpClient, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://hijiribe.donmai.us");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_configuration["DanbooruLogin"]}:{_configuration["DanbooruApiKey"]}")));
            var userAgent = "SDBlazor";
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
        }

        public async Task<List<DanbooruPost>>? GetPosts(string tags = "", int page = 1)
        {
            var url = $"/posts.json?page={page}";
            if (!string.IsNullOrWhiteSpace(tags)) url += "&tags=" + tags;

            //return await _httpClient.GetFromJsonAsync<List<DanbooruPost>>(url);
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<List<DanbooruPost>>();
                content.Select(v =>
                {
                    v.IsVideo = _videoExtensions.Contains(v.Extension);
                    v.PreviewUrl = v.PreviewUrl != null ? v.PreviewUrl.Replace("180x180", "360x360") : "";
                    return v;
                }).ToList();
                return content;
            }
            else return null;
        }
    }
}
