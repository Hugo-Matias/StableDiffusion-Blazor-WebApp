using BlazorWebApp.Data.Dtos;

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
        public string? Description { get; set; }
        public bool IsEnabled { get; set; }
        public double SizeKb { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoadedDate { get; set; }

        public Resource() { CreatedDate = DateTime.Now; }
        public Resource(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file)
        {
            Title = model.Name;
            Filename = file.Name;
            Author = model.Creator.Username;
            CivitaiModelId = model.Id;
            CivitaiModelVersionId = version.Id;
            Type = new() { Name = model.Type };
            Tags = model.Tags.Select(t => t.Name).ToList();
            if (!string.IsNullOrWhiteSpace(version.Description)) Description = version.Description;
            else Description = model.Description;
            IsEnabled = false;
            if (version.TrainedWords != null) TriggerWords = version.TrainedWords;
            SizeKb = file.SizeKb;
            CreatedDate = DateTime.Now;
        }
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
