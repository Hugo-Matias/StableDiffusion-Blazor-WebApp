using BlazorWebApp.Data.Dtos.WebUI;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing models and assets (checkpoints, VAEs, samplers, etc.).
    /// Handles model loading, selection, and asset resolution.
    /// </summary>
    public interface IModelService
    {
        // Model lists
        List<SDModel> CheckpointModels { get; }
        List<SDModel> DiffusionModels { get; }
        List<string> SDVAEs { get; }
        List<string> ClipModels { get; }
        List<string> ClipVisionModels { get; }
        List<string> SDADetailerModels { get; }
        List<Models.Sampler> Samplers { get; }
        List<Scheduler> Schedulers { get; }
        List<Upscaler> Upscalers { get; }

        // Model operations
        Task GetWorkflowModels(bool refresh = false);
        Task SetCurrentModel(string modelTitle, ModeType? mode = null);
        Task SetCurrentVae(string vae, ModeType? mode = null);
        string GetCurrentModel(ModeType? mode = null);
        string? GetCurrentVae(ModeType? mode = null);

        // Asset operations
        List<SDModel> GetModelsForAssetType(AssetType assetType);
        Task<List<string>> GetAssetOptions(AssetType assetType);
    }
}
