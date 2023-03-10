using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    public class LocalResource : BaseResource
    {
        public string Filename { get; set; }
        public FileInfo File { get; set; }
        public string? Author { get; set; }
        public int? CivitaiModelId { get; set; }
        public int? CivitaiModelVersionId { get; set; }
        public ResourceType Type { get; set; }
        public ResourceSubType? SubType { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? TriggerWords { get; set; }

        public LocalResource() { }
        public LocalResource(Resource resourceEntity)
        {
            Title = resourceEntity.Title;
            Author = resourceEntity.Author;
            Filename = resourceEntity.Filename;
            CivitaiModelId = resourceEntity.CivitaiModelId;
            CivitaiModelVersionId = resourceEntity.CivitaiModelVersionId;
            Type = resourceEntity.Type;
            SubType = resourceEntity.SubType;
            Tags = resourceEntity.Tags;
            TriggerWords = resourceEntity.TriggerWords;
        }
    }
}
