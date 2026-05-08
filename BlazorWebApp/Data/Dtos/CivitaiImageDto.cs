using System.Globalization;
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
        public string Type { get; set; }
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
        public List<int> ModelVersionIds { get; set; } = new();
        public int BrowsingLevel { get; set; }
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
        public int? MetaId { get; set; }
        public string ENSD { get; set; }
        public string Size { get; set; }
        public long Seed { get; set; }
        public string Model { get; set; }
        public int Steps { get; set; }
        public string Prompt { get; set; }
        public string Sampler { get; set; }
        public string Scheduler { get; set; }
        public float CfgScale { get; set; }
        public string ClipSkip { get; set; }
        public string ModelHash { get; set; }
        public string NegativePrompt { get; set; }
        public string DenoisingStrength { get; set; }
        public string HiresUpscale { get; set; }
        public string HiresUpscaler { get; set; }
        public string HiresSteps { get; set; }
        public string FaceRestoration { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Comfy { get; set; }
        public List<string> Models { get; set; }
        public List<string> Vaes { get; set; }
        [JsonPropertyName("meta/resources")]
        public List<CivitaiImageMetaResourceDto> Resources { get; set; }

        public CivitaiImageMetaDto() { }
        public CivitaiImageMetaDto(JsonElement meta)
        {
            if (meta.ValueKind == JsonValueKind.Undefined || meta.ValueKind == JsonValueKind.Null || meta.ValueKind != JsonValueKind.Object)
                return;

            if (TryGetProperty(meta, out var prop, "id"))
                MetaId = GetInt32(prop);

            if (TryGetProperty(meta, out var nestedMeta, "meta"))
            {
                if (nestedMeta.ValueKind == JsonValueKind.Object)
                {
                    ReadGenerationMetadata(nestedMeta);
                    return;
                }

                if (nestedMeta.ValueKind == JsonValueKind.Null)
                    return;
            }

            ReadGenerationMetadata(meta);
        }

        private void ReadGenerationMetadata(JsonElement meta)
        {
            ENSD = GetString(meta, "ENSD") ?? ENSD;
            Size = GetString(meta, "Size") ?? Size;
            Seed = GetInt64(meta, "seed") ?? Seed;
            Model = GetString(meta, "Model", "model") ?? Model;
            Steps = GetInt32(meta, "steps") ?? Steps;
            Prompt = GetString(meta, "prompt") ?? Prompt;
            Sampler = GetString(meta, "sampler") ?? Sampler;
            Scheduler = GetString(meta, "scheduler", "Schedule type", "scheduleType") ?? Scheduler;
            CfgScale = GetSingle(meta, "cfgScale", "cfg_scale", "CFG scale") ?? CfgScale;
            ClipSkip = GetString(meta, "Clip skip", "clipSkip") ?? ClipSkip;
            ModelHash = GetString(meta, "Model hash", "modelHash") ?? ModelHash;
            NegativePrompt = GetString(meta, "negativePrompt", "Negative prompt") ?? NegativePrompt;
            DenoisingStrength = GetString(meta, "Denoising strength", "denoise", "denoisingStrength") ?? DenoisingStrength;
            HiresUpscale = GetString(meta, "Hires upscale") ?? HiresUpscale;
            HiresUpscaler = GetString(meta, "Hires upscaler") ?? HiresUpscaler;
            HiresSteps = GetString(meta, "Hires steps") ?? HiresSteps;
            FaceRestoration = GetString(meta, "Face restoration") ?? FaceRestoration;
            Width = GetInt32(meta, "width") ?? Width;
            Height = GetInt32(meta, "height") ?? Height;
            Comfy = GetString(meta, "comfy") ?? Comfy;
            Models = GetStringList(meta, "models") ?? Models;
            Vaes = GetStringList(meta, "vaes") ?? Vaes;

            if (string.IsNullOrWhiteSpace(Model) && Models?.Count > 0)
                Model = Models[0];

            if (TryGetProperty(meta, out var prop, "resources") && prop.ValueKind == JsonValueKind.Array)
            {
                Resources = new();
                foreach (var r in prop.EnumerateArray())
                {
                    if (r.ValueKind != JsonValueKind.Object)
                        continue;

                    var resource = new CivitaiImageMetaResourceDto();
                    foreach (var e in r.EnumerateObject())
                    {
                        if (e.NameEquals("name"))
                            resource.Name = GetStringValue(e.Value);
                        if (e.NameEquals("type"))
                            resource.Type = GetStringValue(e.Value);
                        if (e.NameEquals("weight"))
                            resource.Weight = GetSingle(e.Value) ?? resource.Weight;
                        if (e.NameEquals("hash"))
                            resource.Hash = GetStringValue(e.Value);
                    }
                    Resources.Add(resource);
                }
            }
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            return TryGetProperty(element, out var property, names) ? GetStringValue(property) : null;
        }

        private static string? GetStringValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
                _ => null
            };
        }

        private static int? GetInt32(JsonElement element, params string[] names)
        {
            return TryGetProperty(element, out var property, names) ? GetInt32(property) : null;
        }

        private static int? GetInt32(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
                return value;

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return null;
        }

        private static long? GetInt64(JsonElement element, params string[] names)
        {
            if (!TryGetProperty(element, out var property, names))
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
                return value;

            if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;

            return null;
        }

        private static float? GetSingle(JsonElement element, params string[] names)
        {
            return TryGetProperty(element, out var property, names) ? GetSingle(property) : null;
        }

        private static float? GetSingle(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out var value))
                return value;

            if (element.ValueKind == JsonValueKind.String && float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            return null;
        }

        private static List<string>? GetStringList(JsonElement element, params string[] names)
        {
            if (!TryGetProperty(element, out var property, names) || property.ValueKind != JsonValueKind.Array)
                return null;

            var values = new List<string>();
            foreach (var item in property.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.Object
                    ? GetString(item, "name", "modelName", "vaeName")
                    : GetStringValue(item);

                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }

            return values;
        }

        private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
        {
            property = default;
            if (element.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var jsonProperty in element.EnumerateObject())
            {
                foreach (var name in names)
                {
                    if (string.Equals(jsonProperty.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        property = jsonProperty.Value;
                        return true;
                    }
                }
            }

            return false;
        }
    }
    public class CivitaiImageMetaResourceDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public float Weight { get; set; }
        public string Hash { get; set; }
    }
}
