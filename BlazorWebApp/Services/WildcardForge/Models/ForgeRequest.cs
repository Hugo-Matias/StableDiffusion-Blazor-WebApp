using System.Collections.Generic;
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services.WildcardForge.Models
{
    /// <summary>
    /// Bundle of inputs that feed <see cref="PromptComposer"/>. Mode-agnostic - both Simple mode
    /// and Advanced wizard funnel into a single instance of this DTO.
    /// </summary>
    public class ForgeRequest
    {
        public ForgeOperation Operation { get; set; } = ForgeOperation.Generate;

        // Common
        public string Theme { get; set; } = string.Empty;
        public int Count { get; set; } = 20;
        public string Verbosity { get; set; } = "balanced";
        public string? CategoryId { get; set; } // numeric-id-or-name resolved by knowledge layer
        public string? Subcategory { get; set; }
        public string Scope { get; set; } = "focused";
        public bool DiversityMode { get; set; }
        public List<string> SeedExamples { get; set; } = new();

        // Expand / Refine
        public WildcardCollection? TargetCollection { get; set; }
        public List<WildcardEntry>? ExistingEntries { get; set; }

        // Resolved at compose time (knowledge layer)
        public CategoryCard? ResolvedCategory { get; set; }
    }
}
