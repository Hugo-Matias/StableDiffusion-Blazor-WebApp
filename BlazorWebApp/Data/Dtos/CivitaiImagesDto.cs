using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiImagesDto
    {
        [JsonPropertyName("items")]
        public List<CivitaiImageDto> Images { get; set; }
        public CivitaiImagesMetadataDto Metadata { get; set; }
    }

    public class CivitaiImagesMetadataDto
    {
        //public string NextCursor { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public string NextPage { get; set; }
    }
}
