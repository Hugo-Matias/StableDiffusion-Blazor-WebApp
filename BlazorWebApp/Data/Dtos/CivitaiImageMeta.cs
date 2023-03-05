using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiImageMeta
    {
        [JsonPropertyName("ENSD")] public string ENSD { get; set; }
        [JsonPropertyName("Size")] public string Size { get; set; }
        [JsonPropertyName("seed")] public long Seed { get; set; }
        [JsonPropertyName("Model")] public string Model { get; set; }
        [JsonPropertyName("steps")] public int Steps { get; set; }
        [JsonPropertyName("prompt")] public string Prompt { get; set; }
        [JsonPropertyName("sampler")] public string Sampler { get; set; }
        [JsonPropertyName("cfgScale")] public float CfgScale { get; set; }
        [JsonPropertyName("Clip skip")] public string ClipSkip { get; set; }
        [JsonPropertyName("resources")] public List<CivitaiImageMetaResource> Resources { get; set; }
        [JsonPropertyName("Model hash")] public string ModelHash { get; set; }
        [JsonPropertyName("Hires steps")] public string HighResSteps { get; set; }
        [JsonPropertyName("Hires upscale")] public string HighResUpscale { get; set; }
        [JsonPropertyName("Hires upscaler")] public string HighResUpscaler { get; set; }
        [JsonPropertyName("negativePrompt")] public string NegativePrompt { get; set; }
        [JsonPropertyName("AddNet Enabled")] public string AddNetEnabled { get; set; }
        [JsonPropertyName("Denoising strength")] public string DenoisingStrength { get; set; }
    }

    public class CivitaiImageMetaResource
    {
        [JsonPropertyName("hash")] public string Hash { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("type")] public string Type { get; set; }
        [JsonPropertyName("weight")] public float? Weight { get; set; }
    }
}
