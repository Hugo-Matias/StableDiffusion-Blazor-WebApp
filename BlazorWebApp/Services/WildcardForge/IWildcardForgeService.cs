using System.Collections.Generic;
using System.Threading.Tasks;
using BlazorWebApp.Models;
using BlazorWebApp.Services.WildcardForge.Models;

namespace BlazorWebApp.Services.WildcardForge
{
    /// <summary>
    /// Phase 10 - orchestrates the LLM-driven Wildcard Forge operations and persists drafts to
    /// the existing <see cref="BlazorWebApp.Data.Entities.WildcardCollection"/> tables.
    /// </summary>
    public interface IWildcardForgeService
    {
        /// <summary>Run a Forge operation end-to-end and return the produced draft.</summary>
        Task<ForgeDraftDto> RunAsync(ForgeRequest request, string modelName);

        /// <summary>
        /// Persist the supplied draft. <paramref name="mode"/> is one of "new", "append", "replace".
        /// For "new", <paramref name="newName"/> / <paramref name="newCategory"/> / <paramref name="newDescription"/>
        /// are required.
        /// </summary>
        Task<int> SaveDraftAsync(
            ForgeDraftDto draft,
            string mode,
            int? targetCollectionId = null,
            string? newName = null,
            string? newCategory = null,
            string? newDescription = null);

        /// <summary>Build a Compose preview without calling the LLM (for diagnostics).</summary>
        IReadOnlyList<string> PreviewMessages(ForgeRequest request);
    }
}
