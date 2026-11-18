using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Builders;

/// <summary>
/// Fluent builder for constructing complete ComfyUI workflows.
/// </summary>
public class ComfyWorkflowBuilder
{
    private readonly Dictionary<string, ComfyNode> _nodes = new();

    /// <summary>
    /// Adds a node to the workflow using a fluent configuration callback.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this node in the workflow.</param>
    /// <param name="configure">Callback to configure the node using NodeBuilder.</param>
    public ComfyWorkflowBuilder AddNode(string nodeId, Action<NodeBuilder> configure)
    {
        var nodeBuilder = new NodeBuilder(nodeId);
        configure(nodeBuilder);
        _nodes[nodeId] = nodeBuilder.Build();
        return this;
    }

    /// <summary>
    /// Adds a pre-built node to the workflow.
    /// </summary>
    public ComfyWorkflowBuilder AddNode(string nodeId, ComfyNode node)
    {
        _nodes[nodeId] = node;
        return this;
    }

    /// <summary>
    /// Serializes the workflow to ComfyUI JSON format.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(_nodes, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    /// <summary>
    /// Builds the final ComfyWorkflow result.
    /// </summary>
    public ComfyWorkflow ToComfyWorkflow(NodeRegistry? registry = null)
    {
        return new ComfyWorkflow
        {
            Json = ToJson(),
            Registry = registry
        };
    }

    /// <summary>
    /// Gets the number of nodes currently in the workflow.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Checks if a node with the given ID exists.
    /// </summary>
    public bool HasNode(string nodeId) => _nodes.ContainsKey(nodeId);

    /// <summary>
    /// Gets all node IDs in the workflow.
    /// </summary>
    public IEnumerable<string> GetNodeIds() => _nodes.Keys;
}
