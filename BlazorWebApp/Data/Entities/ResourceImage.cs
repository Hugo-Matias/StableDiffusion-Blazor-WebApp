using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Data.Entities
{
    public class ResourceImage
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public int CivitaiModelId { get; set; }
        public int CivitaiModelVersionID { get; set; }
        public string Hash { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Url { get; set; }
        public bool? Nsfw { get; set; }
        public List<string>? Tags { get; set; }
        public string? GenerationProcess { get; set; }
        public string? Model { get; set; }
        public string? ModelHash { get; set; }
        public string? Prompt { get; set; }
        public string? NegativePrompt { get; set; }
        public long? Seed { get; set; }
        public string? Sampler { get; set; }
        public int? Steps { get; set; }
        public float? CfgScale { get; set; }
        public string? HiresUpscale { get; set; }
        public string? HiresUpscaler { get; set; }
        public string? HiresSteps { get; set; }
        public string? DenoisingStrength { get; set; }
        public string? FaceRestoration { get; set; }
        public string? ClipSkip { get; set; }
        public string? ENSD { get; set; }

        public ResourceImage() { }
        public ResourceImage(CivitaiImageDto image)
        {
            Width = image.Width;
            Height = image.Height;
            Url = image.Url;
            Nsfw = image.Nsfw;
            Hash = image.Hash;
            if (image.Meta != null)
            {
                NegativePrompt = image.Meta.NegativePrompt;
                Model = image.Meta.Model;
                ModelHash = image.Meta.ModelHash;
                Seed = image.Meta.Seed;
                Sampler = image.Meta.Sampler;
                Steps = image.Meta.Steps;
                CfgScale = image.Meta.CfgScale;
                HiresUpscale = image.Meta.HiresUpscale;
                HiresUpscaler = image.Meta.HiresUpscaler;
                HiresSteps = image.Meta.HiresSteps;
                DenoisingStrength = image.Meta.DenoisingStrength;
                FaceRestoration = image.Meta.FaceRestoration;
                if (image.Meta.ClipSkip != null) ClipSkip = image.Meta.ClipSkip.ToString();
                ENSD = image.Meta.ENSD;
                // Override resolution with Size property of ImageMeta,
                // Width and Height properties represent the final image size after Highres.
                if (image.Meta.Size != null)
                {
                    var res = image.Meta.Size.Split("x");
                    if (res.Length == 2)
                    {
                        Width = int.Parse(res[0]);
                        Height = int.Parse(res[1]);
                    }
                }
            }
            Tags = new();
        }
    }
}
