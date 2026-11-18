using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Services;

public static class CharacterReferenceSlotPlanner
{
    public static IReadOnlyList<CharacterReferenceSlotState> ResolveRunSlots(
        AppStateCharacter characterState,
        IReadOnlyCollection<string>? requestedSlotIds,
        bool includeDependencies)
    {
        var slotsById = characterState.Slots.ToDictionary(slot => slot.Id, StringComparer.Ordinal);
        var selectedIds = requestedSlotIds is { Count: > 0 }
            ? requestedSlotIds.Where(slotsById.ContainsKey).Distinct(StringComparer.Ordinal).ToList()
            : characterState.Slots.Where(slot => slot.IsEnabled).Select(slot => slot.Id).ToList();

        var resolvedIds = new List<string>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slotId in selectedIds)
        {
            Visit(slotId, includeDependencies, slotsById, resolvedIds, visiting, visited);
        }

        return resolvedIds.Select(id => slotsById[id]).ToList();
    }

    private static void Visit(
        string slotId,
        bool includeDependencies,
        IReadOnlyDictionary<string, CharacterReferenceSlotState> slotsById,
        List<string> resolvedIds,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (!slotsById.TryGetValue(slotId, out var slot) || visited.Contains(slotId))
        {
            return;
        }

        if (!visiting.Add(slotId))
        {
            return;
        }

        if (includeDependencies)
        {
            var dependencySlotId = ResolveDependencySlotId(slot);
            if (!string.IsNullOrWhiteSpace(dependencySlotId)
                && ShouldRunDependency(dependencySlotId, slotsById))
            {
                Visit(dependencySlotId, includeDependencies, slotsById, resolvedIds, visiting, visited);
            }
        }

        visiting.Remove(slotId);
        visited.Add(slotId);
        resolvedIds.Add(slotId);
    }

    public static string? ResolveDependencySlotId(CharacterReferenceSlotState slot)
    {
        return slot.DependencyPolicy switch
        {
            CharacterReferenceDependencyPolicy.FrontViewOutput => CharacterReferenceWorkflowIds.FrontViewSlotId,
            CharacterReferenceDependencyPolicy.NeutralOutput => CharacterReferenceWorkflowIds.NeutralSlotId,
            CharacterReferenceDependencyPolicy.PreviousSlot => slot.DependencySlotId,
            _ => null
        };
    }

    public static bool HasReusableOutput(CharacterReferenceSlotState slot)
    {
        return !string.IsNullOrWhiteSpace(slot.LastOutputPath)
            && File.Exists(slot.LastOutputPath);
    }

    private static bool ShouldRunDependency(
        string dependencySlotId,
        IReadOnlyDictionary<string, CharacterReferenceSlotState> slotsById)
    {
        return !slotsById.TryGetValue(dependencySlotId, out var dependencySlot)
            || !HasReusableOutput(dependencySlot);
    }
}