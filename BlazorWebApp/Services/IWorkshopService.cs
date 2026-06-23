using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public enum PreviewOrientation { Portrait, Landscape }

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
        /// Generates a single preview image for the node using the live generation parameters
        /// from <see cref="IStateService"/>, with prompt / batch size / resolution / seed overridden.
        /// The image is persisted as <see cref="Image.IsHidden"/> = true and assigned to
        /// <see cref="PromptWorkshopNode.PreviewImageId"/>. Returns the image id, or null on failure.
        /// When <paramref name="useEnhancements"/> is false (default for previews), enhancement
        /// fragments (Detailer, Upscale, Refiner, SeedVR2, SeedVarianceEnhancer, etc.) are
        /// deactivated on the cloned snapshot so the preview workflow stays lean.
        /// </summary>
        Task<int?> GeneratePreviewAsync(int nodeId, Guid workflowId, PreviewOrientation orientation, long seed, bool useEnhancements = false);
    }
}
