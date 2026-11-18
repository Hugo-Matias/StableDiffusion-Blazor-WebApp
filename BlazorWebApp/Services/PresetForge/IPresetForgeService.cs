using BlazorWebApp.Services.PresetForge.Models;

namespace BlazorWebApp.Services.PresetForge;

public interface IPresetForgeService
{
    Task<PresetForgeDraft> RunAsync(PresetForgeRequest request, string modelName);
    Task<IReadOnlyList<int>> SaveDraftAsync(PresetForgeDraft draft);
}