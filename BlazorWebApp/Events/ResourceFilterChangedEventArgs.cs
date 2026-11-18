namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event published when the resource filter state changes (base model toggles, untracked toggle, allow all toggle).
    /// Consumers (WorkflowAssetSelector, LoraForm) should refresh their filtered lists.
    /// </summary>
    public class ResourceFilterChangedEventArgs : EventArgs
    {
    }
}
