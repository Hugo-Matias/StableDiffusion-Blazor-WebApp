using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    public class LocalResource : BaseResource
    {
        public int ResourceId { get; set; }
        public List<LocalResourceFile> Files { get; set; }
        public string? Author { get; set; }
        public int? CivitaiId { get; set; }
        public ResourceType Type { get; set; }
        public ResourceSubType? SubType { get; set; }
        public List<string>? Tags { get; set; }

        public LocalResource() { }
        public LocalResource(Resource resourceEntity)
        {
            ResourceId = resourceEntity.Id;
            Files = new();
            Title = resourceEntity.Title;
            Author = resourceEntity.Author;
            CivitaiId = resourceEntity.CivitaiModelId;
            Type = resourceEntity.Type;
            SubType = resourceEntity.SubType;
            Tags = resourceEntity.Tags;
        }
    }

    public class LocalResourceFile : BaseResource
    {
        public int ResourceId { get; set; }
        public int? CivitaiId { get; set; }
        public string Filename { get; set; }
        public FileInfo File { get; set; }
        public List<string>? TriggerWords { get; set; }
        public bool IsEnabled { get; set; }
        public string? Description { get; set; }
        public double SizeKb { get; set; }

        public LocalResourceFile() { }
        public LocalResourceFile(Resource resourceEntity)
        {
            ResourceId = resourceEntity.Id;
            CivitaiId = resourceEntity.CivitaiModelVersionId;
            Filename = resourceEntity.Filename;
            TriggerWords = resourceEntity.TriggerWords;
            Description = resourceEntity.Description;
            SizeKb = resourceEntity.SizeKb;
        }
    }
}
