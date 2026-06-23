using System.ComponentModel.DataAnnotations;

namespace BlazorWebApp.Data.Entities
{
    public class PromptWorkshopSession
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        /// <summary>Id of the currently active node for this session; null if empty session.</summary>
        public int? CurrentNodeId { get; set; }

        public ICollection<PromptWorkshopNode> Nodes { get; set; } = new List<PromptWorkshopNode>();
    }
}
