using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.MockBuilders;

/// <summary>
/// Builder for creating WorkflowService instances with mocked dependencies.
/// Now simplified for C# workflow builders only.
/// </summary>
public class MockWorkflowServiceBuilder
{
    private Mock<ILogger<WorkflowService>> _mockLogger;
    private Mock<ILogger<FragmentSchemaService>> _mockSchemaLogger;

    public MockWorkflowServiceBuilder()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
        _mockSchemaLogger = new Mock<ILogger<FragmentSchemaService>>();
    }

    public WorkflowService Build()
    {
        // Create the schema service
        var fragmentSchemaService = new FragmentSchemaService(_mockSchemaLogger.Object);

        // WorkflowService now only uses C# IWorkflowBuilder implementations
        // No more file-based templates
        return new WorkflowService(_mockLogger.Object, fragmentSchemaService);
    }
}
