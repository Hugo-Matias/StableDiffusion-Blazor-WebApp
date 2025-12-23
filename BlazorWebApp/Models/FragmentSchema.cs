namespace BlazorWebApp.Models
{
    /// <summary>
    /// Identifies the purpose/category of a fragment.
    /// Used for programmatic fragment discovery instead of string heuristics.
    /// </summary>
    public enum FragmentType
    {
        /// <summary>
        /// Unknown or unspecified fragment type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Model loading fragment (checkpoint, CLIP, VAE loaders).
        /// Typically has no direct UI - parameters come from Assets.
        /// </summary>
        Loader,

        /// <summary>
        /// Prompt encoding fragment (positive/negative text).
        /// </summary>
        Prompts,

        /// <summary>
        /// Resolution and latent image settings.
        /// Includes empty latent, latent from image, etc.
        /// </summary>
        Latent,

        /// <summary>
        /// KSampler and sampling-related settings.
        /// </summary>
        Sampler,

        /// <summary>
        /// Required workflow settings and configuration.
        /// Examples: video settings, animation parameters, mode-specific configuration.
        /// Always visible (non-collapsible by default).
        /// </summary>
        Settings,

        /// <summary>
        /// CLIP text encoding and conditioning nodes.
        /// </summary>
        Conditioning,

        /// <summary>
        /// Enhancement features like upscale, detailer, refinement.
        /// Typically optional (defaultCollapsed = true).
        /// </summary>
        Enhancement,

        /// <summary>
        /// Output nodes like save, preview, display.
        /// </summary>
        Output,

        /// <summary>
        /// Utility/helper fragments with no UI.
        /// </summary>
        Utility
    }

    /// <summary>
    /// Represents the parsed UI schema from a fragment's #meta block.
    /// Used to determine how a fragment should be rendered in the UI.
    /// </summary>
    public class FragmentSchema
    {
        /// <summary>
        /// The type/purpose of this fragment.
        /// Used for programmatic fragment discovery (e.g., finding the sampler fragment).
        /// Parsed from #meta.ui.type field.
        /// </summary>
        public FragmentType Type { get; set; } = FragmentType.Unknown;

        /// <summary>
        /// Blazor component name to render this fragment.
        /// If null, dynamic field rendering is used based on Fields array.
        /// Example: "SamplerForm", "DetailerForm"
        /// </summary>
        public string? Component { get; set; }

        /// <summary>
        /// Display title shown in the UI.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// FontAwesome icon class for the header.
        /// Example: "fa-solid fa-dice"
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Whether the form section can be collapsed.
        /// </summary>
        public bool Collapsible { get; set; } = true;

        /// <summary>
        /// Initial collapsed state.
        /// </summary>
        public bool DefaultCollapsed { get; set; } = false;

        /// <summary>
        /// Whether multiple instances of this fragment can be added.
        /// Used for samplers, detailers, ControlNets, etc.
        /// </summary>
        public bool Chainable { get; set; } = false;

        /// <summary>
        /// Display order in the parameters panel.
        /// Lower values appear first.
        /// Suggested ranges: 0-49 core, 50-99 primary, 100-149 enhancement, 150+ advanced
        /// </summary>
        public int Order { get; set; } = 100;

        /// <summary>
        /// Parameter constraints for designed components.
        /// Keys are parameter names, values contain min/max/step/source.
        /// </summary>
        public Dictionary<string, ParameterConstraints> Parameters { get; set; } = new();

        /// <summary>
        /// Field definitions for dynamic rendering.
        /// Only used when Component is null.
        /// </summary>
        public List<FieldSchema>? Fields { get; set; }

        /// <summary>
        /// Gets whether this schema uses dynamic field rendering.
        /// </summary>
        public bool UsesDynamicFields => string.IsNullOrEmpty(Component) && Fields?.Count > 0;

        /// <summary>
        /// Gets whether this schema has a designed component.
        /// </summary>
        public bool HasDesignedComponent => !string.IsNullOrEmpty(Component);

        /// <summary>
        /// Gets whether this fragment has any UI definition.
        /// True if it has a designed component OR dynamic fields.
        /// </summary>
        public bool HasUI => HasDesignedComponent || UsesDynamicFields;

        /// <summary>
        /// Gets constraints for a specific parameter.
        /// Returns empty constraints if not defined.
        /// </summary>
        public ParameterConstraints GetConstraints(string parameterName)
        {
            return Parameters.GetValueOrDefault(parameterName) ?? new ParameterConstraints();
        }

        /// <summary>
        /// Validates this fragment schema for correctness.
        /// Returns a list of validation errors, empty if valid.
        /// </summary>
        /// <param name="fragmentFile">The fragment file path for error context.</param>
        /// <returns>List of validation error messages.</returns>
        public List<string> Validate(string? fragmentFile = null)
        {
            var errors = new List<string>();
            var context = string.IsNullOrEmpty(fragmentFile) ? "" : $" in '{fragmentFile}'";

            // Validate parameter constraints
            foreach (var (paramName, constraints) in Parameters)
            {
                var constraintErrors = ValidateConstraints(paramName, constraints);
                errors.AddRange(constraintErrors.Select(e => $"{e}{context}"));
            }

            // Validate fields if present
            if (Fields != null)
            {
                for (int i = 0; i < Fields.Count; i++)
                {
                    var field = Fields[i];
                    var fieldErrors = field.Validate();
                    errors.AddRange(fieldErrors.Select(e => $"Field #{i + 1}: {e}{context}"));

                    // Recursively validate nested fields in groups
                    if (field.Type == "group" && field.Fields != null)
                    {
                        for (int j = 0; j < field.Fields.Count; j++)
                        {
                            var nestedField = field.Fields[j];
                            var nestedErrors = nestedField.Validate();
                            errors.AddRange(nestedErrors.Select(e => $"Field #{i + 1}.{j + 1}: {e}{context}"));
                        }
                    }
                }
            }

            // Validate UI consistency
            if (HasDesignedComponent && UsesDynamicFields)
            {
                errors.Add($"Schema has both 'component' and 'fields' defined - only one should be used{context}");
            }

            return errors;
        }

        /// <summary>
        /// Validates a single parameter's constraints.
        /// </summary>
        private static List<string> ValidateConstraints(string paramName, ParameterConstraints constraints)
        {
            var errors = new List<string>();

            // Validate min < max
            if (constraints.Min.HasValue && constraints.Max.HasValue)
            {
                if (constraints.Min.Value >= constraints.Max.Value)
                {
                    errors.Add($"Parameter '{paramName}' has invalid constraints: min ({constraints.Min}) must be less than max ({constraints.Max})");
                }
            }

            // Validate step > 0
            if (constraints.Step.HasValue && constraints.Step.Value <= 0)
            {
                errors.Add($"Parameter '{paramName}' has invalid step value: {constraints.Step} (must be greater than 0)");
            }

            // Validate dynamic source has input_name (only for ComfyUI node sources, not Backend.* sources)
            // Backend.* sources are resolved from IBackendService and don't need input_name
            if (!string.IsNullOrEmpty(constraints.Source) && 
                !constraints.Source.StartsWith("Backend.", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(constraints.InputName))
            {
                errors.Add($"Parameter '{paramName}' has 'source' but no 'input_name' for dynamic resolution");
            }

            return errors;
        }
    }

    /// <summary>
    /// Constraints for a parameter (min, max, step, data source).
    /// Used by both designed components and dynamic fields.
    /// </summary>
    public class ParameterConstraints
    {
        /// <summary>
        /// Minimum allowed value for numeric fields.
        /// </summary>
        public double? Min { get; set; }

        /// <summary>
        /// Maximum allowed value for numeric fields.
        /// </summary>
        public double? Max { get; set; }

        /// <summary>
        /// Step increment for sliders and numeric fields.
        /// </summary>
        public double? Step { get; set; }

        /// <summary>
        /// Default value for the parameter.
        /// Parsed from schema "default" property or extracted from fragment template.
        /// </summary>
        public object? Default { get; set; }

        /// <summary>
        /// Dynamic data source for select fields.
        /// Contains the ComfyUI node class_type to query for available options.
        /// Example: "SeedVR2LoadDiTModel", "KSampler"
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// The input name to query from the node's object_info.
        /// Used with Source to specify which input field to get options for.
        /// Example: "model", "sampler_name"
        /// </summary>
        public string? InputName { get; set; }

        /// <summary>
        /// Static options for select fields.
        /// Used when Source is not specified.
        /// </summary>
        public List<string>? Options { get; set; }

        /// <summary>
        /// Returns true if this constraint has a dynamic source.
        /// For ComfyUI node sources, both Source and InputName are required.
        /// For Backend.* sources (e.g., Backend.Samplers), only Source is required.
        /// </summary>
        public bool HasDynamicSource => !string.IsNullOrEmpty(Source) && 
            (Source.StartsWith("Backend.", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(InputName));

        /// <summary>
        /// Gets the min value as the specified type.
        /// </summary>
        public T GetMin<T>(T defaultValue) where T : struct
        {
            if (Min == null) return defaultValue;
            return (T)Convert.ChangeType(Min.Value, typeof(T));
        }

        /// <summary>
        /// Gets the max value as the specified type.
        /// </summary>
        public T GetMax<T>(T defaultValue) where T : struct
        {
            if (Max == null) return defaultValue;
            return (T)Convert.ChangeType(Max.Value, typeof(T));
        }

        /// <summary>
        /// Gets the step value as the specified type.
        /// </summary>
        public T GetStep<T>(T defaultValue) where T : struct
        {
            if (Step == null) return defaultValue;
            return (T)Convert.ChangeType(Step.Value, typeof(T));
        }

        /// <summary>
        /// Gets the default value as the specified type.
        /// </summary>
        public T? GetDefault<T>()
        {
            if (Default == null) return default;
            try
            {
                if (Default is T typedValue) return typedValue;
                return (T)Convert.ChangeType(Default, typeof(T));
            }
            catch
            {
                return default;
            }
        }
    }

    /// <summary>
    /// Schema for a single field in dynamic rendering.
    /// </summary>
    public class FieldSchema
    {
        /// <summary>
        /// Fragment parameter name this field binds to.
        /// </summary>
        public string Parameter { get; set; } = string.Empty;

        /// <summary>
        /// Display label for the field.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Field type: slider, numeric, seed, select, text, textarea, checkbox, switch, color, file, resolution, group
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Grid column width (1-12). Default is 6.
        /// </summary>
        public int Column { get; set; } = 6;

        /// <summary>
        /// Help text shown on hover.
        /// </summary>
        public string? Tooltip { get; set; }

        /// <summary>
        /// Condition expression for field visibility.
        /// </summary>
        public string? Visible { get; set; }

        /// <summary>
        /// Minimum value (for slider, numeric).
        /// </summary>
        public double? Min { get; set; }

        /// <summary>
        /// Maximum value (for slider, numeric).
        /// </summary>
        public double? Max { get; set; }

        /// <summary>
        /// Step increment (for slider, numeric).
        /// </summary>
        public double? Step { get; set; }

        /// <summary>
        /// Dynamic data source for select/file fields.
        /// Contains the ComfyUI node class_type to query for available options.
        /// The Parameter property is used as the input name when querying.
        /// Example: "SeedVR2LoadDiTModel", "KSampler"
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// The input name to query from the node's object_info.
        /// Used with Source to specify which input field to get options for.
        /// Example: "model", "sampler_name"
        /// </summary>
        public string? InputName { get; set; }

        /// <summary>
        /// Static options (for select).
        /// </summary>
        public List<string>? Options { get; set; }

        /// <summary>
        /// Number of visible rows (for textarea).
        /// </summary>
        public int? Rows { get; set; }

        /// <summary>
        /// Whether a group is collapsible (for group type).
        /// </summary>
        public bool? Collapsible { get; set; }

        /// <summary>
        /// Nested fields (for group type).
        /// </summary>
        public List<FieldSchema>? Fields { get; set; }

        /// <summary>
        /// Returns true if this field has a dynamic source.
        /// For ComfyUI node sources, both Source and InputName are required.
        /// For Backend.* sources (e.g., Backend.Samplers), only Source is required.
        /// </summary>
        public bool HasDynamicSource => !string.IsNullOrEmpty(Source) && 
            (Source.StartsWith("Backend.", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(InputName));

        /// <summary>
        /// Converts this FieldSchema to ParameterConstraints for source resolution.
        /// </summary>
        public ParameterConstraints ToConstraints()
        {
            return new ParameterConstraints
            {
                Min = Min,
                Max = Max,
                Step = Step,
                Source = Source,
                InputName = InputName,
                Options = Options
            };
        }

        /// <summary>
        /// Validates this field schema.
        /// Returns list of validation errors, empty if valid.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Type != "group" && string.IsNullOrEmpty(Parameter))
                errors.Add($"Field missing required 'parameter' property");

            if (string.IsNullOrEmpty(Type))
                errors.Add($"Field '{Parameter}' missing required 'type' property");

            // Backend.* sources don't need input_name - they're resolved from IBackendService
            var isBackendSource = !string.IsNullOrEmpty(Source) && 
                                  Source.StartsWith("Backend.", StringComparison.OrdinalIgnoreCase);

            switch (Type?.ToLowerInvariant())
            {
                case "slider":
                    if (Min == null) errors.Add($"Slider field '{Parameter}' requires 'min'");
                    if (Max == null) errors.Add($"Slider field '{Parameter}' requires 'max'");
                    break;

                case "select":
                    if (string.IsNullOrEmpty(Source) && (Options == null || Options.Count == 0))
                        errors.Add($"Select field '{Parameter}' requires 'source' (node class_type) or 'options'");
                    if (!string.IsNullOrEmpty(Source) && !isBackendSource && string.IsNullOrEmpty(InputName))
                        errors.Add($"Select field '{Parameter}' with 'source' requires 'input_name'");
                    break;

                case "file":
                    if (string.IsNullOrEmpty(Source))
                        errors.Add($"File field '{Parameter}' requires 'source' (node class_type)");
                    if (!string.IsNullOrEmpty(Source) && !isBackendSource && string.IsNullOrEmpty(InputName))
                        errors.Add($"File field '{Parameter}' with 'source' requires 'input_name'");
                    break;

                case "group":
                    if (Fields == null || Fields.Count == 0)
                        errors.Add($"Group '{Label}' requires 'fields' array");
                    break;
            }

            return errors;
        }
    }
}
