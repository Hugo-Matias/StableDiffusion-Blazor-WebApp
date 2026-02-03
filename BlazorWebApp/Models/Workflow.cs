using BlazorWebApp.Data.Entities;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Workflow reference for UI state management and Scriban template loading.
    /// Note: The new fluent API uses IWorkflowBuilder and WorkflowMetadata from BlazorWebApp.Workflows namespace.
    /// </summary>
    public class Workflow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public ModelBase Base { get; set; }
        public ModeType Mode { get; set; }
        
        /// <summary>
        /// List of assets (models/resources) required by this workflow.
        /// These are dynamically loaded from ComfyUI and displayed in the TopToolbar.
        /// </summary>
        public List<WorkflowAsset>? Assets { get; set; }
        
        /// <summary>
        /// List of input sources (images/videos) required by this workflow.
        /// Used for Img2Img, Img2Vid, ControlNet inputs, etc.
        /// </summary>
        public List<WorkflowSource>? Sources { get; set; }
        
        public List<WorkflowStep> Pipeline { get; set; } = new();
        public string RawJson { get; set; } = "";
    }

    /// <summary>
    /// Defines an input source required by a workflow.
    /// </summary>
    public class WorkflowSource
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Type { get; set; } = "image";
        public bool Required { get; set; } = true;
        public string Parameter { get; set; } = "";
    }

    /// <summary>
    /// Pipeline step in a Scriban workflow template.
    /// Note: For new fluent API, fragments are composed directly in IWorkflowBuilder.Build().
    /// </summary>
    public class WorkflowStep
    {
        public string Id { get; set; } = "";
        public string Fragment { get; set; } = "";
        public string RawParameters { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, OutputMapping> Outputs { get; set; } = new();
    }

    /// <summary>
    /// Maps fragment outputs to named references.
    /// Note: For new fluent API, use BlazorWebApp.Workflows.Builders.NodeRegistry.
    /// </summary>
    public class OutputMapping
    {
        public string Node { get; set; } = "";
        public int Index { get; set; }
    }

    /// <summary>
    /// Context for Scriban subgraph rendering.
    /// Note: For new fluent API, context is managed by NodeRegistry and ComfyWorkflowBuilder.
    /// </summary>
    public class SubgraphContext
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public NodeRegistry Outputs { get; set; } = new();
    }

    /// <summary>
    /// Registry for node output references in Scriban templates.
    /// Note: For new fluent API, use BlazorWebApp.Workflows.Builders.NodeRegistry instead.
    /// </summary>
    public class NodeRegistry
    {
        private readonly Dictionary<string, (string nodeId, int outputIndex)> _outputs = new();

        public void Register(string key, string nodeId, int outputIndex = 0)
        {
            _outputs[key] = (nodeId, outputIndex);
        }

        public string GetReference(string key)
        {
            if (!_outputs.TryGetValue(key, out var refData))
            {
                var availableKeys = string.Join(", ", _outputs.Keys.OrderBy(k => k).Select(k => $"'{k}'"));
                throw new KeyNotFoundException(
                    $"No node registered for key '{key}'. Available keys: {availableKeys}");
            }

            return $"[\"{refData.nodeId}\", {refData.outputIndex}]";
        }

        public void Merge(NodeRegistry other)
        {
            foreach (var kvp in other._outputs)
                _outputs[kvp.Key] = kvp.Value;
        }
    }
}
