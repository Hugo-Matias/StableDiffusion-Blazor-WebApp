using System.Text.Json.Serialization;
using BlazorWebApp.Data.Dtos.WebUI;

namespace BlazorWebApp.Models
{
    public class GeneratedImages
	{
		[JsonPropertyName("images")]
		public List<string> Images { get; set; }
		[JsonPropertyName("parameters")]
		public SharedParameters Parameters { get; set; }
		[JsonPropertyName("info")]
		public string Info { get; set; }
	}
}
