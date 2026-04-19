using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.MockBuilders;

/// <summary>
/// Builder for creating WorkflowService instances with mocked dependencies.
/// Simplified for C# workflow builders only.
/// </summary>
public class MockWorkflowServiceBuilder
{
    private Mock<ILogger<WorkflowService>> _mockLogger;

    public MockWorkflowServiceBuilder()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
    }

    public WorkflowService Build()
    {
        // WorkflowService now only uses C# IWorkflowBuilder implementations
        // No more file-based templates or schema service
        return new WorkflowService(_mockLogger.Object);
    }
}
