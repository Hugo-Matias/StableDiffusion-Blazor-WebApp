namespace BlazorWebApp.Models
{
    public class LocalResource : BaseResource
    {
        public FileInfo File { get; set; }
        public ResourceType Type { get; set; }
        public FileInfo? TriggerWordsFile { get; set; }
    }

    public enum ResourceType { Checkpoint, Embedding, Hypernetwork, Lora }
    public enum ResourceSubType { None, Anatomy, Character, Clothing, Design, Expression, Landscape, Object, Person, Pose, Style, Photography, Anime, Painting, NSFW }
}
