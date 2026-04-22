using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using ImageEntity = BlazorWebApp.Data.Entities.Image;

namespace BlazorWebApp.Services;

/// <summary>
/// Centralized, context-aware "Send To" logic that targets workflows
/// compatible with the currently selected workflow base.
/// </summary>
public interface IImageSendToService
{
    /// <summary>Workflows that accept images as input (Img2Img, Img2Vid) for the current base.</summary>
    List<Workflow> GetImageWorkflows();

    /// <summary>Workflows that accept generation parameters (Txt2Img, Img2Img) for the current base.</summary>
    List<Workflow> GetParameterWorkflows();

    /// <summary>Returns the source definition that allows multiple images, if any.</summary>
    WorkflowSource? GetMultiSourceDefinition(Workflow workflow);

    /// <summary>Enumerates the source slots currently in use for a multi-source workflow.</summary>
    List<SourceSlotInfo> GetSourceSlots(Workflow workflow, WorkflowSource multiSource);

    /// <summary>Icon class for the workflow mode.</summary>
    string GetWorkflowIcon(ModeType mode);

    /// <summary>CSS modifier class for the workflow mode.</summary>
    string GetWorkflowModeClass(ModeType mode);

    /// <summary>Sends an image to a single-source workflow (Img2Img / Img2Vid).</summary>
    void SendImageToWorkflow(ImageEntity asset, Workflow workflow);

    /// <summary>Sends an image to a specific existing slot on a multi-source workflow.</summary>
    void SendImageToSourceSlot(ImageEntity asset, Workflow workflow, string sourceKey);

    /// <summary>Sends an image to a new (additional) slot on a multi-source workflow.</summary>
    void SendImageToNewSlot(ImageEntity asset, Workflow workflow, WorkflowSource multiSource);

    /// <summary>Queues the selected parameters of the asset into the target workflow and navigates to it.</summary>
    Task SendParametersToWorkflow(ImageEntity asset, Workflow workflow, IReadOnlyCollection<string> selectedParams);
}

public record SourceSlotInfo(string Key, string Label);
