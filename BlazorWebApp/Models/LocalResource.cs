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
        public string? BaseModel { get; set; }
        public List<string>? Tags { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoadedDate { get; set; }

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
            BaseModel = resourceEntity.BaseModel;
            Tags = resourceEntity.Tags;
            Description = resourceEntity.Description;
            CreatedDate = resourceEntity.CreatedDate;
            LastLoadedDate = resourceEntity.LastLoadedDate;
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
        public string? VersionName { get; set; }
        public string? VersionDescription { get; set; }
        public double SizeKb { get; set; }

        public LocalResourceFile() { }
        public LocalResourceFile(Resource resourceEntity)
        {
            ResourceId = resourceEntity.Id;
            CivitaiId = resourceEntity.CivitaiModelVersionId;
            Filename = resourceEntity.Filename;
            TriggerWords = resourceEntity.TriggerWords;
            VersionName = resourceEntity.VersionName;
            VersionDescription = resourceEntity.VersionDescription;
            Description = resourceEntity.VersionDescription ?? resourceEntity.Description;
            SizeKb = resourceEntity.SizeKb;
        }
    }
}
