using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Entry returned by the paths-info endpoint
    /// POST /api/models/{author}/{repo}/paths-info/{revision}
    /// </summary>
    public class HuggingFacePathInfoDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("oid")]
        public string Oid { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("lfs")]
        public HuggingFaceLfsPointerDto? Lfs { get; set; }

        /// <summary>SHA256 from LFS pointer when available; falls back to Git OID.</summary>
        [JsonIgnore]
        public string? Sha256 => Lfs?.Oid ?? Oid;
    }

    /// <summary>Request body for paths-info endpoint.</summary>
    public class HuggingFacePathsInfoRequest
    {
        [JsonPropertyName("paths")]
        public List<string> Paths { get; set; } = new();
    }
}
