namespace BlazorWebApp.Data.Entities
{
    public class Resource
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Filename { get; set; }
        public string? Author { get; set; }
        public int? CivitaiModelId { get; set; }
        public int? CivitaiModelVersionId { get; set; }
        public ResourceType Type { get; set; }
        public ResourceSubType? SubType { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? TriggerWords { get; set; }
    }

    public class ResourceType
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ResourceSubType
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
