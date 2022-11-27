namespace BlazorWebApp.Models
{
	public class ImageInfoModel
	{
		public string Image { get; set; }
		public string[] InfoString { get; set; }
		public string? Prompt { get; set; }
		public string? NegativePrompt { get; set; }
		public string Sampler { get; set; }
		public int Steps { get; set; }
		public long Seed { get; set; }
		public float CfgScale { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
	}
}
