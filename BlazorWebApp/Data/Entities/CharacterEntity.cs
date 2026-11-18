using BlazorWebApp.Models.CharacterCreator;

namespace BlazorWebApp.Data.Entities;

public class CharacterEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ThumbnailImageId { get; set; }
    public CharacterBody Body { get; set; } = CharacterBody.CreateDefault();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}