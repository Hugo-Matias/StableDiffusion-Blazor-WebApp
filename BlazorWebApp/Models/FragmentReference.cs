namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents a reference to a fragment with its associated parameters and schema.
    /// Used to expose discovered fragments from GenerationParameterService without
    /// requiring consumers to iterate over the fragments dictionary.
    /// </summary>
    public class FragmentReference
    {
        /// <summary>
        /// The unique fragment ID (e.g., "main_sampler", "empty_latent").
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// The fragment's parameter values and metadata.
        /// </summary>
        public FragmentParameters Parameters { get; init; } = new();

        /// <summary>
        /// The fragment's UI schema definition.
        /// Null if the fragment has no schema defined.
        /// </summary>
        public FragmentSchema? Schema { get; init; }

        /// <summary>
        /// The user-friendly title of the fragment.
        /// Defaults to fragment ID if schema is not available.
        /// </summary>
        public string Title => Schema?.Title ?? Id;

        /// <summary>
        /// The icon associated with this fragment (e.g., "fa-solid fa-image").
        /// Null if no icon is defined in the schema.
        /// </summary>
        public string? Icon => Schema?.Icon;

        /// <summary>
        /// Whether this fragment has a UI (either designed component or dynamic fields).
        /// </summary>
        public bool HasUI => Schema?.HasUI ?? false;

        /// <summary>
        /// Whether this fragment has a designed Blazor component.
        /// </summary>
        public bool HasDesignedComponent => Schema?.HasDesignedComponent ?? false;
    }
}
