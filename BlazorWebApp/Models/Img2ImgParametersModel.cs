namespace BlazorWebApp.Models
{
	public class Img2ImgParametersModel : SharedParametersModel
	{

		public Img2ImgParametersModel() { }
		public Img2ImgParametersModel(SharedParametersModel clone)
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
