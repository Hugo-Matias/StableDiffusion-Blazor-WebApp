using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public enum PreviewOrientation { Portrait, Landscape }

    /// <summary>
    /// Controls how <see cref="IWorkshopService.ResolvePreviewResolution"/> picks the preview's
    /// width/height. <c>Fast</c> uses a small, base-model-appropriate resolution for quick
    /// iteration. <c>Workflow</c> uses the selected workflow's own configured latent default.
    /// </summary>
    public enum WorkshopResolutionMode { Fast, Workflow }

    public interface IWorkshopService
    {
        Task<PromptWorkshopSession> CreateSessionAsync(string name, string rootPrompt);
        Task<List<PromptWorkshopSession>> ListSessionsAsync();
        Task<PromptWorkshopSession?> LoadSessionAsync(int sessionId);
        Task RenameSessionAsync(int sessionId, string newName);
        Task DeleteSessionAsync(int sessionId);

        Task<PromptWorkshopNode> AddChatChildAsync(int sessionId, int parentId, string instruction, string modelName, int ancestorDepth, ChatVerbosity verbosity = ChatVerbosity.Match, float temperature = 0.7f);
        Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, EvolveControls controls);
        Task SetCurrentNodeAsync(int sessionId, int nodeId);

        /// <summary>Walks parent pointers up to maxDepth nodes (inclusive of the target), oldest-first order.</summary>
        Task<IReadOnlyList<PromptWorkshopNode>> GetAncestorChainAsync(int nodeId, int maxDepth);

        /// <summary>Updates the prompt text on a node (used by the editable right-panel field).</summary>
        Task UpdateNodePromptAsync(int nodeId, string promptText);

        /// <summary>
        /// Computes the final preview width/height for a workflow + orientation + resolution
        /// mode, without generating anything. Used to drive the read-only resolution display
        /// in the right panel and by <see cref="GeneratePreviewAsync"/> internally.
        /// <c>Workflow</c> mode reads the workflow's currently configured resolution (live
        /// in-memory parameters if it's the active Generate-page workflow, otherwise its last
        /// saved <see cref="Data.Entities.WorkflowState"/>), falling back to the fragment's
        /// static schema default when nothing has been saved yet.
        /// </summary>
        Task<(int Width, int Height)> ResolvePreviewResolutionAsync(Workflow workflow, PreviewOrientation orientation, WorkshopResolutionMode mode);

        /// <summary>
        /// Generates a single preview image for the node using the live generation parameters
        /// from <see cref="IStateService"/>, with prompt / batch size / resolution / seed overridden.
        /// The image is persisted as <see cref="Image.IsHidden"/> = true and assigned to
        /// <see cref="PromptWorkshopNode.PreviewImageId"/>. Returns the image id, or null on failure.
        /// When <paramref name="useEnhancements"/> is false (default for previews), enhancement
        /// fragments (Detailer, Upscale, Refiner, SeedVR2, SeedVarianceEnhancer, etc.) are
        /// deactivated on the cloned snapshot so the preview workflow stays lean.
        /// </summary>
        Task<int?> GeneratePreviewAsync(int nodeId, Guid workflowId, PreviewOrientation orientation, long seed, bool useEnhancements = false, WorkshopResolutionMode resolutionMode = WorkshopResolutionMode.Fast);
    }
}
