using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiCreatorsDto
    {
        [JsonPropertyName("items")]
        public List<CivitaiCreatorsCreatorDto> Creators { get; set; }
        public CivitaiMetadataDto Metadata { get; set; }
    }

    public class CivitaiCreatorsCreatorDto
    {
        public string Username { get; set; }
        public int ModelCount { get; set; }
        public string Link { get; set; }
    }
}
