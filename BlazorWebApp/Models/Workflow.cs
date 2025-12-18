using BlazorWebApp.Data.Entities;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Models
{
    public class Workflow
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
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
        
        public List<WorkflowStep> Pipeline { get; set; }
        public string RawJson { get; set; }
    }

    /// <summary>
    /// Defines an input source required by a workflow.
    /// </summary>
    public class WorkflowSource
    {
        /// <summary>
        /// Unique ID for this source within the workflow.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Display label for the input.
        /// </summary>
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of source: "image" or "video".
        /// </summary>
        public string Type { get; set; } = "image";
        
        /// <summary>
        /// Whether this source is required for generation.
        /// </summary>
        public bool Required { get; set; } = true;
        
        /// <summary>
        /// Parameter name in the workflow template that receives this source.
        /// </summary>
        public string Parameter { get; set; } = string.Empty;
    }

    public class WorkflowStep
    {
        public string Fragment { get; set; }
        public string RawParameters { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public Dictionary<string, OutputMapping> Outputs { get; set; }
    }

    public class OutputMapping
    {
        public string Node { get; set; }
        public int Index { get; set; }
    }

    public class SubgraphContext
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public NodeRegistry Outputs { get; set; } = new();
    }

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
                throw new KeyNotFoundException($"No node registered for key '{key}'");

            return $"[\"{refData.nodeId}\", {refData.outputIndex}]";
        }

        public void Merge(NodeRegistry other)
        {
            foreach (var kvp in other._outputs)
                _outputs[kvp.Key] = kvp.Value;
        }
    }
}
