namespace BlazorWebApp.Services
{
    public class ComfyUIEventBus
    {
        public event Action<Guid>? ExecutionSucceeded;
        public event Action<Guid, string>? ExecutionFailed;

        public void PublishExecutionSucceeded(Guid promptId) => ExecutionSucceeded?.Invoke(promptId);
        public void PublishExecutionFailed(Guid promptId, string error) => ExecutionFailed?.Invoke(promptId, error);
    }
}
