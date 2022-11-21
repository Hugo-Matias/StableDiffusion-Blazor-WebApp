namespace BlazorWebApp.Models
{
	public class AppSettingsModel
	{
		public int StepsDefaultValue { get; set; } = 20;
		public int StepsMin { get; set; } = 1;
		public int StepsMax { get; set; } = 150;
		public int StepsStep { get; set; } = 1;
		public string SamplerDefault { get; set; } = "Euler";
		public int SeedDefaultValue { get; set; } = -1;
		public int ResolutionMin { get; set; } = 64;
		public int ResolutionMax { get; set; } = 2048;
		public int ResolutionStep { get; set; } = 64;
		public int ResolutionDefaultValue { get; set; } = 512;
		public int BatchCountMin { get; set; } = 1;
		public int BatchCountMax { get; set; } = 8;
		public int BatchCountStep { get; set; } = 1;
		public int BatchCountDefaultValue { get; set; } = 4;
		public int BatchSizeMin { get; set; } = 1;
		public int BatchSizeMax { get; set; } = 8;
		public int BatchSizeStep { get; set; } = 1;
		public int BatchSizeDefaultValue { get; set; } = 1;
		public float CfgScaleMin { get; set; } = 1.0f;
		public float CfgScaleMax { get; set; } = 30.0f;
		public float CfgScaleStep { get; set; } = 0.5f;
		public float CfgScaleDefaultValue { get; set; } = 9.5f;
	}
}
