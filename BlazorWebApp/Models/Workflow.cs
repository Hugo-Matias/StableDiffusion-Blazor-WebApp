using BlazorWebApp.Data.Entities;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Workflow reference for UI state management.
    /// Used by services to represent discovered workflows from C# IWorkflowBuilder implementations.
    /// </summary>
    public class Workflow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";

        /// <summary>
        /// Short, user-facing description of what the workflow does and how to use it.
        /// Optional. Surfaced on the Generate page banner and Info drawer.
        /// </summary>
        public string? Description { get; set; }

        public ModelBase Base { get; set; }
        public ModeType Mode { get; set; }

        /// <summary>
        /// List of assets (models/resources) required by this workflow.
        /// These are dynamically loaded from ComfyUI and displayed in the TopToolbar.
        /// </summary>
        public List<WorkflowAsset>? Assets { get; set; }

        /// <summary>
        /// List of input sources (images/videos) required by this workflow.
        /// Used for Img2Img, Img2Vid, ControlNet inputs, etc.
        /// </summary>
        public List<WorkflowSource>? Sources { get; set; }

        /// <summary>
        /// CivitAI base model strings that this workflow is compatible with.
        /// Used to filter asset selectors and LoRA lists to only show compatible resources.
        /// </summary>
        public List<string>? CompatibleResourceBaseModels { get; set; }
    }

    /// <summary>
    /// Defines an input source required by a workflow.
    /// </summary>
    public class WorkflowSource
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "image";
        public bool Required { get; set; } = true;
        public bool AllowMultiple { get; set; }
        public string Parameter { get; set; } = "";
        public VideoSourceOptions? DefaultVideoOptions { get; set; }
    }
}
