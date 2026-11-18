using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

public interface ICharacterReferenceRunService
{
    Task<CharacterReferenceRunResult> RunAsync(CharacterReferenceRunRequest? request = null);
}

public sealed record CharacterReferenceRunRequest(
    IReadOnlyCollection<string>? SlotIds = null,
    bool IncludeDependencies = true);

public sealed record CharacterReferenceRunResult(
    bool Success,
    string? PromptId,
    IReadOnlyList<CharacterReferenceSlotRunResult> Slots,
    string? ErrorMessage = null);

public sealed record CharacterReferenceSlotRunResult(
    string SlotId,
    CharacterReferenceSlotStatus Status,
    Image? Image,
    string? ErrorMessage = null);