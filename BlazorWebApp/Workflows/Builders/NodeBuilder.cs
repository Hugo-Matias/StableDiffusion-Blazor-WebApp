using System.Text.Json.Serialization;

namespace BlazorWebApp.Workflows.Builders;

/// <summary>
/// Fluent builder for constructing ComfyUI nodes with type-safe inputs and outputs.
/// </summary>
public class NodeBuilder
{
    private readonly string _nodeId;
    private string _classType = "";
    private readonly Dictionary<string, object> _inputs = new();
    private string _title;

    public NodeBuilder(string nodeId)
    {
        _nodeId = nodeId;
        _title = nodeId; // Default title is the node ID
    }

    /// <summary>
    /// Sets the ComfyUI node class type (e.g., "KSampler", "VAEDecode").
    /// </summary>
    public NodeBuilder Type(string classType)
    {
        _classType = classType;
        return this;
    }

    /// <summary>
    /// Sets the display title for this node (shown in ComfyUI UI).
    /// </summary>
    public NodeBuilder Title(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Adds a string input to the node.
    /// </summary>
    public NodeBuilder Input(string name, string value)
    {
        _inputs[name] = value;
        return this;
    }

    /// <summary>
    /// Adds an integer input to the node.
    /// </summary>
    public NodeBuilder Input(string name, int value)
    {
        _inputs[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a double input to the node.
    /// </summary>
    public NodeBuilder Input(string name, double value)
    {
        _inputs[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a long input to the node.
    /// </summary>
    public NodeBuilder Input(string name, long value)
    {
        _inputs[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a boolean input to the node.
    /// </summary>
    public NodeBuilder Input(string name, bool value)
    {
        _inputs[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a list input to the node (for array values).
    /// </summary>
    public NodeBuilder Input(string name, IEnumerable<object> value)
    {
        _inputs[name] = value.ToList();
        return this;
    }

    /// <summary>
    /// Adds a reference to another node's output.
    /// </summary>
    /// <param name="name">The input name on this node.</param>
    /// <param name="reference">Tuple of (nodeId, outputIndex) to reference.</param>
    public NodeBuilder InputRef(string name, (string nodeId, int index) reference)
    {
        _inputs[name] = new object[] { reference.nodeId, reference.index };
        return this;
    }

    /// <summary>
    /// Builds the node as a ComfyNode object.
    /// </summary>
    public ComfyNode Build()
    {
        if (string.IsNullOrEmpty(_classType))
            throw new InvalidOperationException($"Node '{_nodeId}' is missing class_type. Call Type() before Build().");

        return new ComfyNode
        {
            Inputs = _inputs,
            ClassType = _classType,
            Meta = new NodeMeta { Title = _title }
        };
    }
}

/// <summary>
/// Represents a ComfyUI node for JSON serialization.
/// </summary>
public class ComfyNode
{
    [JsonPropertyName("inputs")]
    public Dictionary<string, object> Inputs { get; init; } = new();

    [JsonPropertyName("class_type")]
    public string ClassType { get; init; } = "";

    [JsonPropertyName("_meta")]
    public NodeMeta Meta { get; init; } = new();
}

/// <summary>
/// Metadata for a ComfyUI node.
/// </summary>
public class NodeMeta
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";
}
