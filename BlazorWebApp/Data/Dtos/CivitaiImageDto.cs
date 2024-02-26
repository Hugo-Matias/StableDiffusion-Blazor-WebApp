using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiImageDto
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Nsfw { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public CivitaiNsfw NsfwLevel { get; set; }
        public DateTime CreatedAt { get; set; }
        public int PostId { get; set; }
        public CivitaiImageStatsDto Stats { get; set; }
        [JsonIgnore]
        public CivitaiImageMetaDto Meta { get; set; }
        [JsonPropertyName("meta")]
        public JsonElement MetaObject { get; set; }
        public string Username { get; set; }
        public byte[]? ImageType { get; set; }
    }

    public class CivitaiImageStatsDto
    {
        public int CryCount { get; set; }
        public int LaughCount { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public int HeartCount { get; set; }
        public int CommentCount { get; set; }
    }

    public class CivitaiImageMetaDto
    {
        public string ENSD { get; set; }
        public string Size { get; set; }
        public long Seed { get; set; }
        public string Model { get; set; }
        public int Steps { get; set; }
        public string Prompt { get; set; }
        public string Sampler { get; set; }
        public float CfgScale { get; set; }
        public string ClipSkip { get; set; }
        public string ModelHash { get; set; }
        public string NegativePrompt { get; set; }
        public string DenoisingStrength { get; set; }

        public CivitaiImageMetaDto() { }
        public CivitaiImageMetaDto(JsonElement meta)
        {
            if (meta.TryGetProperty("ENSD", out var prop))
                ENSD = prop.GetString();
            if (meta.TryGetProperty("Size", out prop))
                Size = prop.GetString();
            if (meta.TryGetProperty("seed", out prop))
                Seed = prop.GetInt64();
            if (meta.TryGetProperty("Model", out prop))
                Model = prop.GetString();
            if (meta.TryGetProperty("steps", out prop))
                Steps = prop.GetInt32();
            if (meta.TryGetProperty("prompt", out prop))
                Prompt = prop.GetString();
            if (meta.TryGetProperty("sampler", out prop))
                Sampler = prop.GetString();
            if (meta.TryGetProperty("cfgScale", out prop))
                CfgScale = prop.GetSingle();
            if (meta.TryGetProperty("Clip skip", out prop))
                ClipSkip = prop.GetString();
            if (meta.TryGetProperty("Model hash", out prop))
                ModelHash = prop.GetString();
            if (meta.TryGetProperty("negativePrompt", out prop))
                NegativePrompt = prop.GetString();
            if (meta.TryGetProperty("Denoising strength", out prop))
                DenoisingStrength = prop.GetString();
        }
    }
}
