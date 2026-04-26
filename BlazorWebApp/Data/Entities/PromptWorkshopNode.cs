using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BlazorWebApp.Data.Entities
{
    public class PromptWorkshopNode
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public int? ParentId { get; set; }

        /// <summary>0 for root, 1 for first gen, increments with each turn or spawn cycle.</summary>
        public int GenerationNumber { get; set; }

        [Required]
        public string PromptText { get; set; } = string.Empty;

        /// <summary>"root" | "chat" | "evolve"</summary>
        [Required, MaxLength(16)]
        public string Mode { get; set; } = "root";

        /// <summary>Chat-mode only. The user instruction that produced this node.</summary>
        public string? Instruction { get; set; }

        public string? ModelUsed { get; set; }

        /// <summary>JSON array of Image entity IDs. May be stale (images may have been deleted).</summary>
        public string? ImageIdsJson { get; set; }

        public DateTime CreatedAt { get; set; }

        public PromptWorkshopSession Session { get; set; } = null!;
        public PromptWorkshopNode? Parent { get; set; }
        public ICollection<PromptWorkshopNode> Children { get; set; } = new List<PromptWorkshopNode>();

        [NotMapped]
        public List<int> ImageIds
        {
            get => string.IsNullOrWhiteSpace(ImageIdsJson)
                ? new()
                : JsonSerializer.Deserialize<List<int>>(ImageIdsJson) ?? new();
            set => ImageIdsJson = JsonSerializer.Serialize(value);
        }
    }
}
