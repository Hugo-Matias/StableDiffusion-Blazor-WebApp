namespace BlazorWebApp.Models
{
    public class BaseResource
    {
        public FileInfo File { get; set; }
        public string Title { get; set; }
        public string? ImagePath { get; set; }
        public ResourceType Type { get; set; }
    }

    public enum ResourceType { Checkpoint, Embedding, Hypernetwork, Lora }
}
