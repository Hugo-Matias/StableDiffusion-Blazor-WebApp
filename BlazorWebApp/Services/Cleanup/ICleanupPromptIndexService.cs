namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupPromptIndexService
    {
        CleanupPromptIndex BuildPromptIndex(string? prompt);
    }
}