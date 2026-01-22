using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Builders;

/// <summary>
/// Registry for tracking node outputs and their references throughout workflow construction.
/// Supports both string-based (legacy) and type-safe generic registration.
/// </summary>
public class NodeRegistry
{
    private readonly Dictionary<string, (string nodeId, int outputIndex)> _outputs = new();
    private readonly Dictionary<Type, List<NodeOutput>> _typedOutputs = new();

    #region String-based methods (backward compatibility)

    /// <summary>
    /// Registers an output from a node for later reference (string-based).
    /// </summary>
    /// <param name="outputName">Logical name for this output (e.g., "model_output", "latent_output").</param>
    /// <param name="nodeId">The node ID that produces this output.</param>
    /// <param name="outputIndex">The output slot index on the node (usually 0).</param>
    public void Register(string outputName, string nodeId, int outputIndex)
    {
        _outputs[outputName] = (nodeId, outputIndex);
    }

    /// <summary>
    /// Gets a reference to a previously registered output (string-based).
    /// </summary>
    /// <param name="outputName">The logical name of the output to reference.</param>
    /// <returns>Tuple of (nodeId, outputIndex) suitable for InputRef().</returns>
    /// <exception cref="InvalidOperationException">Thrown if the output was not registered.</exception>
    public (string nodeId, int outputIndex) GetRef(string outputName)
    {
        if (_outputs.TryGetValue(outputName, out var reference))
        {
            return reference;
        }

        throw new InvalidOperationException($"Output '{outputName}' has not been registered. Available outputs: {string.Join(", ", _outputs.Keys)}");
    }

    /// <summary>
    /// Checks if an output has been registered (string-based).
    /// </summary>
    public bool HasOutput(string outputName) => _outputs.ContainsKey(outputName);

    /// <summary>
    /// Gets all registered output names (string-based).
    /// </summary>
    public IEnumerable<string> GetAllOutputNames() => _outputs.Keys;

    #endregion

    #region Type-safe generic methods

    /// <summary>
    /// Registers a typed output from a node for compile-time type safety.
    /// </summary>
    /// <typeparam name="TOutput">The output type (ModelOutput, ClipOutput, etc.).</typeparam>
    /// <param name="output">The output instance to register.</param>
    public void Register<TOutput>(TOutput output) where TOutput : NodeOutput
    {
        var type = typeof(TOutput);
        if (!_typedOutputs.ContainsKey(type))
            _typedOutputs[type] = new List<NodeOutput>();
        
        _typedOutputs[type].Add(output);
    }

    /// <summary>
    /// Gets a reference to the single registered output of the specified type.
    /// </summary>
    /// <typeparam name="TOutput">The output type to retrieve.</typeparam>
    /// <returns>Tuple of (nodeId, outputIndex) suitable for InputRef().</returns>
    /// <exception cref="InvalidOperationException">Thrown if no output or multiple outputs of the type exist.</exception>
    public (string nodeId, int index) GetRef<TOutput>() where TOutput : NodeOutput
    {
        var outputs = GetAll<TOutput>();
        if (outputs.Count == 0)
            throw new InvalidOperationException($"No output of type {typeof(TOutput).Name} has been registered.");
        if (outputs.Count > 1)
            throw new InvalidOperationException($"Multiple outputs of type {typeof(TOutput).Name} found ({outputs.Count}). Use GetRef<TOutput>(string scopePrefix) to specify which one.");
        
        var output = outputs[0];
        return (output.NodeId, output.OutputIndex);
    }

    /// <summary>
    /// Gets a reference to a scoped output of the specified type.
    /// Useful when multiple outputs of the same type exist (e.g., detailer, upscale).
    /// </summary>
    /// <typeparam name="TOutput">The output type to retrieve.</typeparam>
    /// <param name="scopePrefix">Node ID prefix to filter by (e.g., "detailer_", "upscale_").</param>
    /// <returns>Tuple of (nodeId, outputIndex) suitable for InputRef().</returns>
    /// <exception cref="InvalidOperationException">Thrown if no matching output exists.</exception>
    public (string nodeId, int index) GetRef<TOutput>(string scopePrefix) where TOutput : NodeOutput
    {
        var outputs = GetAll<TOutput>();
        var scoped = outputs.FirstOrDefault(o => o.NodeId.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase));
        if (scoped == null)
            throw new InvalidOperationException($"No output of type {typeof(TOutput).Name} with scope prefix '{scopePrefix}' has been registered.");
        
        return (scoped.NodeId, scoped.OutputIndex);
    }

    /// <summary>
    /// Gets all registered outputs of the specified type.
    /// </summary>
    /// <typeparam name="TOutput">The output type to retrieve.</typeparam>
    /// <returns>List of outputs of the specified type.</returns>
    public List<TOutput> GetAll<TOutput>() where TOutput : NodeOutput
    {
        var type = typeof(TOutput);
        if (_typedOutputs.TryGetValue(type, out var outputs))
            return outputs.Cast<TOutput>().ToList();
        return new List<TOutput>();
    }

    /// <summary>
    /// Checks if any output of the specified type has been registered.
    /// </summary>
    public bool HasOutput<TOutput>() where TOutput : NodeOutput
    {
        return GetAll<TOutput>().Count > 0;
    }

    #endregion

    #region Common methods

    /// <summary>
    /// Gets the total number of registered outputs (both string-based and typed).
    /// </summary>
    public int Count => _outputs.Count + _typedOutputs.Values.Sum(list => list.Count);

    /// <summary>
    /// Clears all registered outputs (both string-based and typed).
    /// </summary>
    public void Clear()
    {
        _outputs.Clear();
        _typedOutputs.Clear();
    }

    #endregion
}
