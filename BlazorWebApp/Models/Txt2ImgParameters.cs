using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class Txt2ImgParameters : SharedParameters
	{
		[JsonPropertyName("enable_hr")]
		public bool? EnableHR { get; set; }
		[JsonPropertyName("firstphase_width")]
		public int? FirstphaseWidth { get; set; }
		[JsonPropertyName("firstphase_height")]
		public int? FirstphaseHeight { get; set; }

		public Txt2ImgParameters() { }
		public Txt2ImgParameters(SharedParameters clone)
		{
			DenoisingStrength = clone.DenoisingStrength;
			Prompt = clone.Prompt;
			Styles = clone.Styles;
			Seed = clone.Seed;
			Subseed = clone.Subseed;
			SubseedStrength = clone.SubseedStrength;
			SeedResizeFromH = clone.SeedResizeFromH;
			SeedResizeFromW = clone.SeedResizeFromW;
			BatchSize = clone.BatchSize;
			NIter = clone.NIter;
			Steps = clone.Steps;
			CfgScale = clone.CfgScale;
			Width = clone.Width;
			Height = clone.Height;
			RestoreFaces = clone.RestoreFaces;
			Tiling = clone.Tiling;
			NegativePrompt = clone.NegativePrompt;
			Eta = clone.Eta;
			SChurn = clone.SChurn;
			STmax = clone.STmax;
			STmin = clone.STmin;
			SNoise = clone.SNoise;
			SamplerIndex = clone.SamplerIndex;
		}
	}
}
