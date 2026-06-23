using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Events;

/// <summary>
/// Published when a workflow is enabled or disabled for Generate-page visibility.
/// </summary>
public class WorkflowAvailabilityChangedEventArgs : EventArgs
{
    public Guid WorkflowId { get; init; }
    public ModelBase WorkflowBase { get; init; }
    public bool IsEnabled { get; init; }
}