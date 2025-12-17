namespace BlazorWebApp.Data.Dtos
{
    public class WildcardEntryDto
    {
        public int Id { get; set; }
        public int CollectionId { get; set; }
        public string Value { get; set; } = string.Empty;
        public float Weight { get; set; } = 1.0f;
        public int SortOrder { get; set; }
    }
}
