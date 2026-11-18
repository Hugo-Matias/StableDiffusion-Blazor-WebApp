namespace BlazorWebApp.Models;

public enum PromptApplicationMode
{
    Replace,
    Prepend,
    Append
}

public class PromptSendToRequest
{
    public string PositivePrompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public PromptApplicationMode Mode { get; set; } = PromptApplicationMode.Replace;
    public bool IncludeNegativePrompt { get; set; }
}