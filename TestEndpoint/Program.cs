using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TestEndpoint
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:3000") // Replace with your ComfyUI base URL
            };

            string jsonPayload = @"
        {
	""input"": {
		""prompt"": ""score_9, score_8_up, score_7_up, teenager, 20 years old, black hair, looking at viewer, from front, red dress, street, busy street, crowded, blur background, bokeh, day, green eyes, parted lips, glossy lips, eyeliner, upper torso, small breasts, slim waist, wide hips, thick thighs"",
		""negative_prompt"": ""score_6, score_5, score_4, "",
		""width"": 832,
		""height"": 1248,
		""seed"": 5,
		""steps"": 20,
		""cfg_scale"": 5.5,
		""sampler_name"": ""multistep/res_2m"",
		""scheduler"": ""beta57"",
		""denoise"": 1,
		""batch_size"": 2,
		""checkpoint"": ""Anime/ntrMIXIllustriousXL_xiii.safetensors""
	}
}";
            var request = new HttpRequestMessage(HttpMethod.Post, "/workflow/sd/txt2img")
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", "PostmanRuntime/7.32.0"); // or whatever Postman uses
            request.Headers.Add("Accept-Encoding", "identity"); // disable compression

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync();

            var images = JsonSerializer.Deserialize<ComfyResponse>(rawJson);

            for (int i = 0; i < images.images.Count; i++)
            {
                Console.WriteLine($"Image {i}: Hash = {GetHash(images.images[i])}");
            }
        }
        static string GetHash(string base64)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(base64);
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }
    }
    public class ComfyResponse
    {
        public List<string> images { get; set; }
    }
}
