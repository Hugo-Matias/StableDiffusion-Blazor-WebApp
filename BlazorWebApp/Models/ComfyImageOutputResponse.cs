namespace BlazorWebApp.Models;

public sealed class ComfyImageOutputResponse
{
    public string PromptId { get; init; } = string.Empty;
    public Dictionary<string, List<ComfyImageOutputFile>> ImagesByNodeId { get; init; } = new();
}

public sealed class ComfyImageOutputFile
{
    public string Filename { get; init; } = string.Empty;
    public string Subfolder { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
}