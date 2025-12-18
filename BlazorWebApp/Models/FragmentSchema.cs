namespace BlazorWebApp.Models
{
    /// <summary>
    /// Represents the parsed UI schema from a fragment's #meta block.
    /// Used to determine how a fragment should be rendered in the UI.
    /// </summary>
    public class FragmentSchema
    {
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
        /// Data source reference for select fields.
        /// Format: "ServiceName.PropertyName" (e.g., "Backend.Samplers")
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Static options for select fields.
        /// Used when Source is not specified.
        /// </summary>
        public List<string>? Options { get; set; }

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
        /// Data source reference (for select, file).
        /// </summary>
        public string? Source { get; set; }

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

            switch (Type?.ToLowerInvariant())
            {
                case "slider":
                    if (Min == null) errors.Add($"Slider field '{Parameter}' requires 'min'");
                    if (Max == null) errors.Add($"Slider field '{Parameter}' requires 'max'");
                    break;

                case "select":
                    if (string.IsNullOrEmpty(Source) && (Options == null || Options.Count == 0))
                        errors.Add($"Select field '{Parameter}' requires 'source' or 'options'");
                    break;

                case "file":
                    if (string.IsNullOrEmpty(Source))
                        errors.Add($"File field '{Parameter}' requires 'source'");
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
