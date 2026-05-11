namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupMaintenanceService
    {
        int PendingCount { get; }
    }
}