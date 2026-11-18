using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    /// <summary>
    /// Single entry from the repository file-tree endpoint
    /// GET /api/models/{author}/{repo}/tree/{revision}/{path}
    /// </summary>
    public class HuggingFaceTreeEntryDto
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

        /// <summary>True when this entry is a directory (tree node).</summary>
        [JsonIgnore]
        public bool IsDirectory => string.Equals(Type, "directory", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when this entry is a file (blob node).</summary>
        [JsonIgnore]
        public bool IsFile => string.Equals(Type, "file", StringComparison.OrdinalIgnoreCase);

        /// <summary>Inferred filename from the last path segment.</summary>
        [JsonIgnore]
        public string Filename => System.IO.Path.GetFileName(Path);

        /// <summary>Inferred extension, lower-cased, including the dot.</summary>
        [JsonIgnore]
        public string Extension => System.IO.Path.GetExtension(Path).ToLowerInvariant();

        /// <summary>Size in KB (rounded).</summary>
        [JsonIgnore]
        public double SizeKb => Size / 1024.0;

        /// <summary>Size in MB (rounded to 2 decimal places).</summary>
        [JsonIgnore]
        public double SizeMb => Math.Round(Size / 1048576.0, 2);
    }

    public class HuggingFaceLfsPointerDto
    {
        [JsonPropertyName("oid")]
        public string Oid { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("pointerSize")]
        public int PointerSize { get; set; }
    }
}
