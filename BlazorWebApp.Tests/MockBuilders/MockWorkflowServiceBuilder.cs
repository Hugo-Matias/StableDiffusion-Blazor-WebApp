using BlazorWebApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.MockBuilders;

/// <summary>
/// Builder for creating WorkflowService instances with mocked dependencies.
/// </summary>
public class MockWorkflowServiceBuilder
{
    private Mock<ILogger<WorkflowService>> _mockLogger;
    private IOService _ioService;
    private string _tempPath;

    public MockWorkflowServiceBuilder()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
        _tempPath = Path.Combine(Path.GetTempPath(), $"WorkflowTests_{Guid.NewGuid()}");
    }

    public MockWorkflowServiceBuilder WithTempWorkflowPath(out string tempPath)
    {
        tempPath = _tempPath;
        return this;
    }

    public MockWorkflowServiceBuilder WithTemplateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempPath, "Workflows", "Templates", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    public MockWorkflowServiceBuilder WithFragmentFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_tempPath, "Workflows", "Fragments", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    public WorkflowService Build()
    {
        // Create IOService with real implementation for file operations
        var mockConfig = new Mock<IConfiguration>();
        _ioService = new IOService(mockConfig.Object);

        // Note: WorkflowService uses hardcoded path from AppContext.BaseDirectory
        // For testing, we'd need to either:
        // 1. Make the path injectable
        // 2. Create files in the expected location
        // 3. Use reflection to override
        return new WorkflowService(_ioService, _mockLogger.Object);
    }

    public void Cleanup()
    {
        if (Directory.Exists(_tempPath))
        {
            try
            {
                Directory.Delete(_tempPath, true);
            }
            catch { }
        }
    }
}
