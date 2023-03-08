using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    public class Prompt
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string? Positive { get; set; }
        public string? Negative { get; set; }
        public string? ImagePath { get; set; }
        public bool IsFavorite { get; set; }

        public Prompt() { }
        public Prompt(PromptResource prompt)
        {
            Id = prompt.Id;
            Title = prompt.Title;
            Positive = prompt.Positive;
            Negative = prompt.Negative;
            ImagePath = prompt.ImageSrc;
            IsFavorite = prompt.IsFavorite;
        }
    }
}
