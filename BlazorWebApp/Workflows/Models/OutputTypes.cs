namespace BlazorWebApp.Workflows.Models;

/// <summary>
/// Base record for all node output types.
/// Provides compile-time type safety for node output references.
/// </summary>
public abstract record NodeOutput(string NodeId, int OutputIndex);

/// <summary>
/// Represents a MODEL output from a node (e.g., UNETLoader, CheckpointLoaderSimple).
/// </summary>
public sealed record ModelOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);

/// <summary>
/// Represents a CLIP output from a node (e.g., DualCLIPLoader, CLIPLoader).
/// </summary>
public sealed record ClipOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);

/// <summary>
/// Represents a VAE output from a node (e.g., VAELoader, CheckpointLoaderSimple).
/// </summary>
public sealed record VaeOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);

/// <summary>
/// Represents a LATENT output from a node (e.g., KSampler, EmptyLatentImage).
/// </summary>
public sealed record LatentOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);

/// <summary>
/// Represents an IMAGE output from a node (e.g., VAEDecode, LoadImage).
/// </summary>
public sealed record ImageOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);

/// <summary>
/// Represents a CONDITIONING output from a node (e.g., CLIPTextEncode).
/// </summary>
public sealed record ConditioningOutput(string NodeId, int OutputIndex) : NodeOutput(NodeId, OutputIndex);
