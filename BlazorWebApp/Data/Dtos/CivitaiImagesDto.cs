using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiImagesDto
    {
        [JsonPropertyName("items")]
        public List<CivitaiImageDto> Images { get; set; } = new();
        public CivitaiImagesMetadataDto Metadata { get; set; } = new();
    }

    public class CivitaiImagesMetadataDto
    {
        public string NextCursor { get; set; } = string.Empty;
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string NextPage { get; set; } = string.Empty;
    }
}
