namespace BlazorWebApp.Data.Entities
{
	public class Image
	{
		public int Id { get; set; }
		public string Path { get; set; }
		public string? InfoPath { get; set; }
		public string? Prompt { get; set; }
		public string? NegativePrompt { get; set; }
		public int SamplerId { get; set; }
		public int Steps { get; set; }
		public long Seed { get; set; }
		public float CfgScale { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public bool Favorite { get; set; }
		public int ProjectId { get; set; }
		public int ModeId { get; set; }
		public double? DenoisingStrength { get; set; }
	}
}
