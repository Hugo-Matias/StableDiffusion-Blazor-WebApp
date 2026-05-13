using BlazorWebApp.Models;

namespace BlazorWebApp.Workflows.Models;

public record CharacterReferenceWorkflowBuildResult(
    ComfyWorkflow Workflow,
    IReadOnlyList<CharacterReferenceOutputNode> Outputs);

public record CharacterReferenceOutputNode(
    string SlotId,
    string Label,
    string NodeId,
    string FilenamePrefix);

public static class CharacterReferenceWorkflowIds
{
    public const string SourceImageNodeId = "character_source_image";
    public const string SourceImageOutputKey = "source_image";
    public const string FrontViewSlotId = "front-view";
    public const string NeutralSlotId = "neutral";

    public static string SlotImageOutputKey(string slotId) => $"slot_{Sanitize(slotId)}_image_output";

    public static string SlotNodePrefix(string slotId) => $"character_slot_{Sanitize(slotId)}_";

    public static string SlotFilenamePrefix(CharacterReferenceSlotState slot)
    {
        return $"Character/{Sanitize(slot.Label)}";
    }

    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "slot";
        }

        var chars = value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();

        var sanitized = new string(chars);
        while (sanitized.Contains("__", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("__", "_", StringComparison.Ordinal);
        }

        return sanitized.Trim('_');
    }
}
