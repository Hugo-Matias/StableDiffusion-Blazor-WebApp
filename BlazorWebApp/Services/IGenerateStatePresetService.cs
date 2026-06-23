using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public interface IGenerateStatePresetService
    {
        Task<List<GenerateStatePreset>> GetPresetsAsync(Guid workflowId);
        Task<GenerateStatePreset?> GetPresetAsync(int id);
        Task<GenerateStatePreset> CreatePresetAsync(string name, GenerationParameters parameters);
        Task<GenerateStatePreset?> RenamePresetAsync(int id, string name);
        Task<GenerateStatePreset?> UpdatePresetAsync(int id, string name, GenerationParameters parameters);
        Task DeletePresetAsync(int id);
    }
}