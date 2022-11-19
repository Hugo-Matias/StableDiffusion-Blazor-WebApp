namespace BlazorWebApp.Models
{
	public class SamplerModel
	{
		//[JsonPropertyName("name")]
		public string Name { get; set; }
		//[JsonPropertyName("aliases")]
		public string[] Aliases { get; set; }
		//[JsonPropertyName("options")]
		public SamplerOptionsModel Options { get; set; }
	}

	public class SamplerOptionsModel
	{
		//[JsonPropertyName("scheduler")]
		public string Scheduler { get; set; }
	}
}
