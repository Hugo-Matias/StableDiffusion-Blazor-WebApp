using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class GeneratedImagesModel
	{
		[JsonPropertyName("images")]
		public List<string> Images { get; set; }
		[JsonPropertyName("parameters")]
		public SharedParametersModel Parameters { get; set; }
		[JsonPropertyName("info")]
		public string Info { get; set; }
	}
}
