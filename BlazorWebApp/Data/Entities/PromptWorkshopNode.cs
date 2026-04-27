using System.ComponentModel.DataAnnotations;

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

        /// <summary>
        /// Single preview image id for this node. Null when no preview has been generated.
        /// The referenced <see cref="Image"/> row carries <c>IsHidden = true</c> so it stays
        /// out of the gallery views.
        /// </summary>
        public int? PreviewImageId { get; set; }

        public DateTime CreatedAt { get; set; }

        public PromptWorkshopSession Session { get; set; } = null!;
        public PromptWorkshopNode? Parent { get; set; }
        public ICollection<PromptWorkshopNode> Children { get; set; } = new List<PromptWorkshopNode>();
    }
}
