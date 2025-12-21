namespace BlazorWebApp.Models.Fragments.Attributes
{
    /// <summary>
    /// Indicates this property's options should be populated dynamically from a ComfyUI node or backend collection.
    /// Used for dropdowns that need runtime-resolved options (e.g., sampler names, model lists).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DynamicSourceAttribute : Attribute
    {
        /// <summary>
        /// The ComfyUI node class_type to query for options.
        /// Example: "KSampler", "CheckpointLoaderSimple"
        /// </summary>
        public string? NodeType { get; }

        /// <summary>
        /// The input name on the node to get options from.
        /// Example: "sampler_name", "ckpt_name"
        /// </summary>
        public string? InputName { get; }

        /// <summary>
        /// Alternative: Use a well-known backend collection.
        /// Example: "Backend.Samplers", "Backend.Schedulers", "Backend.Upscalers"
        /// </summary>
        public string? BackendCollection { get; }

        /// <summary>
        /// Creates a DynamicSource that queries a ComfyUI node for options.
        /// </summary>
        /// <param name="nodeType">The ComfyUI node class_type (e.g., "KSampler")</param>
        /// <param name="inputName">The input name on the node (e.g., "sampler_name")</param>
        public DynamicSourceAttribute(string nodeType, string inputName)
        {
            NodeType = nodeType;
            InputName = inputName;
        }

        /// <summary>
        /// Creates a DynamicSource that uses a well-known backend collection.
        /// </summary>
        /// <param name="backendCollection">The backend collection path (e.g., "Backend.Samplers")</param>
        public DynamicSourceAttribute(string backendCollection)
        {
            BackendCollection = backendCollection;
        }

        /// <summary>
        /// Whether this source uses a backend collection (vs. a ComfyUI node query).
        /// </summary>
        public bool IsBackendCollection => !string.IsNullOrEmpty(BackendCollection);
    }
}
