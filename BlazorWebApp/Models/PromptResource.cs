using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    public class PromptResource : BaseResource
    {
        public int Id { get; set; }
        public string? Positive { get; set; }
        public string? Negative { get; set; }
        public bool IsFavorite { get; set; }

        public PromptResource() { }
        public PromptResource(Prompt entity)
        {
            Id = entity.Id;
            Title = entity.Title;
            ImageSrc = entity.ImagePath;
            Positive = entity.Positive;
            Negative = entity.Negative;
            IsFavorite = entity.IsFavorite;
        }
    }
}
