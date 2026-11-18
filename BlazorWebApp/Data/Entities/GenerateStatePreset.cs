using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    public class GenerateStatePreset
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid WorkflowId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public GenerateStatePresetBody Body { get; set; } = new();
    }
}