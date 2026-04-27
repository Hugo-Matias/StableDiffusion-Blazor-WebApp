using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Data.Entities
{
    public class Image
    {
        public int Id { get; set; }
        public string Path { get; set; }
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
        public int Score { get; set; } = 0;
        public List<Selection> Selections { get; set; } = new();
        public string Scheduler { get; set; }
        public Resource? Model { get; set; }
        public int? ResourceId { get; set; }

        /// <summary>
        /// Excludes the image from gallery / browser views when true. Used by Workshop previews
        /// and other internal-only generations. By-id lookups and direct references still work.
        /// </summary>
        public bool IsHidden { get; set; }


        public Image() { }
        public Image(ResourceImage resourceImage)
        {
            Path = resourceImage.Path;
            Prompt = resourceImage.Prompt;
            NegativePrompt = resourceImage.NegativePrompt;
            Steps = resourceImage.Steps != null ? (int)resourceImage.Steps : 0;
            Seed = resourceImage.Seed != null ? (long)resourceImage.Seed : -1;
            CfgScale = resourceImage.CfgScale != null ? (float)resourceImage.CfgScale : 0;
            Width = resourceImage.Width != null ? (int)resourceImage.Width : 0;
            Height = resourceImage.Height != null ? (int)resourceImage.Height : 0;
            DenoisingStrength = resourceImage.DenoisingStrength != null ? double.Parse(resourceImage.DenoisingStrength) : null;
        }

        public Image(DanbooruPost post)
        {
            Id = -1;
            Width = post.Width;
            Height = post.Height;
            Prompt = string.Join(", ", post.Tags);
            Path = post.Url;
        }
    }
}
