using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using ImageEntity = BlazorWebApp.Data.Entities.Image;

namespace BlazorWebApp.Services;

/// <summary>
/// Centralized, context-aware Send To logic for workflow source media and asset parameters.
/// </summary>
public interface IMediaSendToService
{
    bool IsLocal(string? path);
    IReadOnlyList<MediaSendToTarget> GetSourceTargets(SendToMediaType mediaType);
    string GetWorkflowIcon(ModeType mode);
    string GetWorkflowModeClass(ModeType mode);
    string GetMediaIcon(SendToMediaType mediaType);
    void SendSourceToTarget(ImageEntity asset, MediaSendToTarget target);
    List<Workflow> GetParameterWorkflows();
    Task SendParametersToWorkflow(ImageEntity asset, Workflow workflow, IReadOnlyCollection<string> selectedParams);
}

public enum SendToMediaType
{
    Image,
    Video
}

public record MediaSendToTarget(
    Workflow Workflow,
    string SourceKey,
    string SourceLabel,
    string SourceType,
    bool IsNewSlot)
{
    public string WorkflowTitle => Workflow.Title;
    public ModeType Mode => Workflow.Mode;
    public string DisplayLabel => IsNewSlot ? $"{Workflow.Title} / Add {SourceLabel}" : $"{Workflow.Title} / {SourceLabel}";
}