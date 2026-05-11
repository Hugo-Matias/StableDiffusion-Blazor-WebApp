using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

/// <summary>
/// Send-to surface for prompts produced by the LLM Workshop. Mirrors the shape of
/// <see cref="IMediaSendToService"/> but the asset is a plain prompt string, so the
/// only target operation is "apply this text to the Positive prompt of a chosen
/// txt2img / img2img workflow and navigate there".
/// </summary>
public interface IPromptSendToService
{
    /// <summary>Returns Txt2Img + Img2Img workflows for the current base, ordered by mode then title.</summary>
    List<Workflow> GetParameterWorkflows();

    /// <summary>Mode → CSS class for visual grouping (mirrors MediaSendToService).</summary>
    string GetWorkflowModeClass(ModeType mode);

    /// <summary>Mode → font-awesome icon (mirrors MediaSendToService).</summary>
    string GetWorkflowIcon(ModeType mode);

    /// <summary>
    /// Sends <paramref name="prompt"/> as the Positive prompt of <paramref name="workflow"/>
    /// (queued if not currently active) and navigates to the generate page for it.
    /// </summary>
    void SendPromptToWorkflow(string prompt, Workflow workflow);
}
