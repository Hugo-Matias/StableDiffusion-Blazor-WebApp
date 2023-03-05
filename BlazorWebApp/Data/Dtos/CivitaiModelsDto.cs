using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiModelsDto
    {
        [JsonPropertyName("items")]
        public List<CivitaiModelsModelDto> Models { get; set; }
        public CivitaiMetadataDto Metadata { get; set; }
    }
}
