using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    public interface IWorkshopService
    {
        Task<PromptWorkshopSession> CreateSessionAsync(string name, string rootPrompt);
        Task<List<PromptWorkshopSession>> ListSessionsAsync();
        Task<PromptWorkshopSession?> LoadSessionAsync(int sessionId);
        Task RenameSessionAsync(int sessionId, string newName);
        Task DeleteSessionAsync(int sessionId);

        Task<PromptWorkshopNode> AddChatChildAsync(int sessionId, int parentId, string instruction, string modelName, int ancestorDepth);
        Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, int count, string modelName);
        Task SetCurrentNodeAsync(int sessionId, int nodeId);

        /// <summary>Walks parent pointers up to maxDepth nodes (inclusive of the target), oldest-first order.</summary>
        Task<IReadOnlyList<PromptWorkshopNode>> GetAncestorChainAsync(int nodeId, int maxDepth);
    }
}
