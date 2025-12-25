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
        
        /// <summary>
        /// Indicates whether this workflow uses Fluid/Liquid template syntax (.liquid extension).
        /// When true, composition uses PipelineExpander for $foreach, $if, $compute markers.
        /// When false (legacy .sbn), composition uses Scriban rendering.
        /// </summary>
        public bool IsFluidTemplate { get; set; }
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
        /// <summary>
        /// Unique identifier for this step in the pipeline.
        /// Used as the key in GenerationParameters.Fragments.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
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

        /// <summary>
        /// Returns all registered outputs for iteration.
        /// </summary>
        public IReadOnlyDictionary<string, (string nodeId, int index)> All => _outputs;

        /// <summary>
        /// Returns the keys of all registered outputs.
        /// </summary>
        public IEnumerable<string> Keys => _outputs.Keys;

        /// <summary>
        /// Returns the count of registered outputs.
        /// </summary>
        public int Count => _outputs.Count;
    }
}
