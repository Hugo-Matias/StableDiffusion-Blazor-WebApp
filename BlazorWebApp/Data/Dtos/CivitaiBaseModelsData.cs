using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiBaseModelsData
    {
        [JsonPropertyName("families")]
        public List<CivitaiBaseModelFamily> Families { get; set; } = new();

        [JsonPropertyName("groups")]
        public List<CivitaiBaseModelGroup> Groups { get; set; } = new();

        [JsonPropertyName("models")]
        public List<CivitaiBaseModelEntry> Models { get; set; } = new();
    }

    public class CivitaiBaseModelFamily
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("disabled")]
        public bool? Disabled { get; set; }
    }

    public class CivitaiBaseModelGroup
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("selector")]
        public string? Selector { get; set; }
    }

    public class CivitaiBaseModelEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "image";

        [JsonPropertyName("group")]
        public string Group { get; set; } = string.Empty;

        [JsonPropertyName("groupDisplayName")]
        public string? GroupDisplayName { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("familyDisplayName")]
        public string? FamilyDisplayName { get; set; }

        [JsonPropertyName("hidden")]
        public bool? Hidden { get; set; }

        [JsonPropertyName("ecosystem")]
        public string? Ecosystem { get; set; }

        [JsonPropertyName("engine")]
        public string? Engine { get; set; }
    }
}
