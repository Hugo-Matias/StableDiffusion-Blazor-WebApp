namespace BlazorWebApp.Data.Entities
{
    public class Image
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string? Info { get; set; }
        public string? InfoPath { get; set; }
        public string? Prompt { get; set; }
        public string? NegativePrompt { get; set; }
        public int SamplerId { get; set; } = 0;
        public int Steps { get; set; } = -1;
        public long Seed { get; set; } = -1;
        public float CfgScale { get; set; } = -1;
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Favorite { get; set; }
        public int ProjectId { get; set; }
        public int ModeId { get; set; }
        public double? DenoisingStrength { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;

        public Image() { }
        public Image(ResourceImage resourceImage)
        {
            Path = resourceImage.Path;
            Prompt = resourceImage.Prompt;
            NegativePrompt = resourceImage.NegativePrompt;
            Steps = resourceImage.Steps != null ? (int)resourceImage.Steps : 25;
            Seed = resourceImage.Seed != null ? (long)resourceImage.Seed : -1;
            CfgScale = resourceImage.CfgScale != null ? (float)resourceImage.CfgScale : 7.5f;
            Width = resourceImage.Width != null ? (int)resourceImage.Width : 512;
            Height = resourceImage.Height != null ? (int)resourceImage.Height : 768;
            DenoisingStrength = resourceImage.DenoisingStrength != null ? double.Parse(resourceImage.DenoisingStrength) : null;
        }
    }
}
