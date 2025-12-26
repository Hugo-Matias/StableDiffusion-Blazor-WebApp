using BlazorWebApp.Services;
using BlazorWebApp.Services.Templating;
using BlazorWebApp.Services.Templating.Pipeline;
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
    private Mock<ILogger<WorkflowTemplateParser>> _mockParserLogger;
    private Mock<ILogger<FragmentSchemaService>> _mockSchemaLogger;
    private Mock<ILogger<FluidTemplateService>> _mockFluidLogger;
    private Mock<ILogger<PipelineExpander>> _mockExpanderLogger;
    private Mock<ILogger<ComputeRegistry>> _mockComputeLogger;
    private Mock<ILogger<ForeachProcessor>> _mockForeachLogger;
    private Mock<ILogger<ConditionalProcessor>> _mockConditionalLogger;
    private IOService _ioService;
    private string _tempPath;

    public MockWorkflowServiceBuilder()
    {
        _mockLogger = new Mock<ILogger<WorkflowService>>();
        _mockParserLogger = new Mock<ILogger<WorkflowTemplateParser>>();
        _mockSchemaLogger = new Mock<ILogger<FragmentSchemaService>>();
        _mockFluidLogger = new Mock<ILogger<FluidTemplateService>>();
        _mockExpanderLogger = new Mock<ILogger<PipelineExpander>>();
        _mockComputeLogger = new Mock<ILogger<ComputeRegistry>>();
        _mockForeachLogger = new Mock<ILogger<ForeachProcessor>>();
        _mockConditionalLogger = new Mock<ILogger<ConditionalProcessor>>();
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

        // Create the parser, schema service, and Fluid template service
        var fluidTemplateService = new FluidTemplateService(_mockFluidLogger.Object);
        var templateParser = new WorkflowTemplateParser(_mockParserLogger.Object, fluidTemplateService);
        var fragmentSchemaService = new FragmentSchemaService(_mockSchemaLogger.Object);

        // Create pipeline processors
        var computeRegistry = new ComputeRegistry(_mockComputeLogger.Object);
        var processors = new List<IPipelineProcessor>
        {
            new ForeachProcessor(_mockForeachLogger.Object),
            new ConditionalProcessor(_mockConditionalLogger.Object)
        };
        var pipelineExpander = new PipelineExpander(_mockExpanderLogger.Object, processors, computeRegistry);

        return new WorkflowService(
            _ioService, 
            _mockLogger.Object, 
            templateParser, 
            fragmentSchemaService, 
            fluidTemplateService,
            new FragmentConditionValidator(new Mock<ILogger<FragmentConditionValidator>>().Object),
            pipelineExpander);
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
