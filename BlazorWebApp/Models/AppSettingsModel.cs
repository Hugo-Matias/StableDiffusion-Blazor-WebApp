namespace BlazorWebApp.Models
{
	public class AppSettingsModel
	{
		public int StepsDefaultValue { get; set; } = 20;
		public int StepsMin { get; set; } = 1;
		public int StepsMax { get; set; } = 150;
		public int StepsStep { get; set; } = 1;
		public string SamplerDefault { get; set; } = "Euler";
	}
}
