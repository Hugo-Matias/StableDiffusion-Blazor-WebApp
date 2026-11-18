using System.Text.Json;
using BlazorWebApp.Data.Dtos.ComfyUI;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

public static class ComfyUIHistoryImageOutputCollector
{
    public static Dictionary<string, List<ComfyImageOutputFile>> Collect(
        JsonElement historyRoot,
        Guid promptId,
        IReadOnlyCollection<string>? outputNodeIds,
        string comfyOutputsPath)
    {
        var expectedNodeIds = outputNodeIds?
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var outputsByNodeId = expectedNodeIds?.ToDictionary(
            nodeId => nodeId,
            _ => new List<ComfyImageOutputFile>(),
            StringComparer.Ordinal)
            ?? new Dictionary<string, List<ComfyImageOutputFile>>(StringComparer.Ordinal);

        if (!historyRoot.TryGetProperty(promptId.ToString(), out var promptElement) ||
            !promptElement.TryGetProperty("outputs", out var outputsElement))
        {
            return outputsByNodeId;
        }

        var expectedSet = expectedNodeIds is null
            ? null
            : new HashSet<string>(expectedNodeIds, StringComparer.Ordinal);

        foreach (var outputNode in outputsElement.EnumerateObject())
        {
            if (expectedSet is not null && !expectedSet.Contains(outputNode.Name))
            {
                continue;
            }

            if (!outputNode.Value.TryGetProperty("images", out var imagesElement) ||
                imagesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var files = JsonSerializer.Deserialize<List<ComfyUIHistoryImageResponse>>(imagesElement.GetRawText()) ?? [];
            if (!outputsByNodeId.TryGetValue(outputNode.Name, out var nodeFiles))
            {
                nodeFiles = [];
                outputsByNodeId[outputNode.Name] = nodeFiles;
            }

            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file.Filename))
                {
                    continue;
                }

                var relativePath = string.IsNullOrWhiteSpace(file.Subfolder)
                    ? file.Filename
                    : Path.Combine(file.Subfolder, file.Filename);

                nodeFiles.Add(new ComfyImageOutputFile
                {
                    Filename = file.Filename,
                    Subfolder = file.Subfolder ?? string.Empty,
                    Type = file.Type ?? string.Empty,
                    RelativePath = relativePath,
                    FullPath = string.IsNullOrWhiteSpace(comfyOutputsPath)
                        ? relativePath
                        : Path.Combine(comfyOutputsPath, relativePath)
                });
            }
        }

        return outputsByNodeId;
    }
}