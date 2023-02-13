using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class PromptStyle
	{
		public string Name { get; set; }
		public string Prompt { get; set; }
		[JsonPropertyName("negative_prompt")]
		public string NegativePrompt { get; set; }
	}
}
