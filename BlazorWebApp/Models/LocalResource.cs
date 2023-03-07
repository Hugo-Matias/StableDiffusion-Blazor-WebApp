namespace BlazorWebApp.Models
{
    public class LocalResource : BaseResource
    {
        public FileInfo File { get; set; }
        public ResourceType Type { get; set; }
        public FileInfo? TriggerWordsFile { get; set; }
    }

    public enum ResourceType { Checkpoint, Embedding, Hypernetwork, Lora }
    public enum ResourceSubType { None, Style, Character, Person, Pose, Clothing, Anatomy, Expression, Landscape, Location, Animal, Object, Car, Design, Photography, Anime, Painting, NSFW }
}
