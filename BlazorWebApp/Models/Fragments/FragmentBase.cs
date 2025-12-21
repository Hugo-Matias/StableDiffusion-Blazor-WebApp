using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Base class for all workflow fragment types.
    /// Provides common properties and utilities for fragment management.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(GenericFragment), "generic")]
    [JsonDerivedType(typeof(SamplerFragment), "sampler")]
    [JsonDerivedType(typeof(PromptsFragment), "prompts")]
    [JsonDerivedType(typeof(LatentFragment), "latent")]
    [JsonDerivedType(typeof(SeedVR2Fragment), "seed_vr2")]
    [JsonDerivedType(typeof(UpscaleFragment), "upscale")]
    [JsonDerivedType(typeof(DetailerFragment), "detailer")]
    [JsonDerivedType(typeof(ConditioningVariationFragment), "conditioning_variation")]
    [JsonDerivedType(typeof(SeedVarianceEnhancerFragment), "seed_variance_enhancer")]
    [JsonDerivedType(typeof(WanSamplerFragment), "wan_sampler")]
    [JsonDerivedType(typeof(WanLoadModelFragment), "wan_load_model")]
    [JsonDerivedType(typeof(FrameInterpolationFragment), "frame_interpolation")]
    [JsonDerivedType(typeof(WanPromptsFragment), "wan_prompts")]
    public abstract class FragmentBase
    {
        /// <summary>
        /// Unique identifier for this fragment instance in the pipeline.
        /// Examples: "main_sampler", "refiner_sampler", "prompts"
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The fragment template file path relative to Workflows/Fragments/.
        /// Example: "sampler.sbn", "wan/sampler-wan.sbn"
        /// </summary>
        [JsonIgnore]
        public abstract string FragmentFile { get; }

        /// <summary>
        /// Whether this fragment is active and should be rendered in the workflow.
        /// Inactive fragments are skipped during composition.
        /// </summary>
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Order in the pipeline for chainable fragments.
        /// Lower values render first.
        /// </summary>
        [JsonPropertyName("order")]
        public int Order { get; set; }

        /// <summary>
        /// Flattens all properties to a dictionary for Scriban template rendering.
        /// Uses JsonPropertyName attributes for key names to match template expectations.
        /// </summary>
        public virtual Dictionary<string, object?> ToDictionary()
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                // Skip JsonIgnore properties
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                // Use JsonPropertyName if present, otherwise use property name
                var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
                result[jsonName] = prop.GetValue(this);
            }

            // Ensure IsActive is always available for condition checking
            result["IsActive"] = IsActive;

            return result;
        }

        /// <summary>
        /// Sets a property value from a dictionary key.
        /// Matches the key against JsonPropertyName attributes.
        /// </summary>
        /// <param name="key">The JSON property name</param>
        /// <param name="value">The value to set</param>
        /// <returns>True if property was found and set</returns>
        public virtual bool SetPropertyFromDictionary(string key, object? value)
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                // Skip read-only properties
                if (!prop.CanWrite)
                    continue;

                // Match by JsonPropertyName or property name
                var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
                if (!string.Equals(jsonName, key, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Found matching property, try to convert and set
                try
                {
                    var convertedValue = ConvertValue(value, prop.PropertyType);
                    prop.SetValue(this, convertedValue);
                    return true;
                }
                catch
                {
                    // Conversion failed, skip
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a value to the target type, handling common conversions.
        /// </summary>
        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Direct type match
            if (underlyingType.IsInstanceOfType(value))
                return value;

            // Handle JsonElement (from deserialization)
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElement(jsonElement, underlyingType);
            }

            // Numeric conversions
            if (IsNumericType(underlyingType) && IsNumericType(value.GetType()))
            {
                return Convert.ChangeType(value, underlyingType);
            }

            // String to other types
            if (value is string strValue)
            {
                if (underlyingType == typeof(bool))
                    return bool.Parse(strValue);
                if (IsNumericType(underlyingType))
                    return Convert.ChangeType(double.Parse(strValue), underlyingType);
            }

            // Default conversion attempt
            return Convert.ChangeType(value, underlyingType);
        }

        private static object? ConvertJsonElement(JsonElement element, Type targetType)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String when targetType == typeof(string) => element.GetString(),
                JsonValueKind.Number when targetType == typeof(int) => element.GetInt32(),
                JsonValueKind.Number when targetType == typeof(long) => element.GetInt64(),
                JsonValueKind.Number when targetType == typeof(float) => element.GetSingle(),
                JsonValueKind.Number when targetType == typeof(double) => element.GetDouble(),
                JsonValueKind.Number when targetType == typeof(decimal) => element.GetDecimal(),
                JsonValueKind.True or JsonValueKind.False when targetType == typeof(bool) => element.GetBoolean(),
                _ => JsonSerializer.Deserialize(element.GetRawText(), targetType)
            };
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) ||
                   type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }

        /// <summary>
        /// Creates a deep clone of this fragment with a new ID.
        /// Used for chainable fragments (e.g., multiple samplers).
        /// </summary>
        /// <param name="newId">The ID for the cloned fragment</param>
        public abstract FragmentBase Clone(string newId);

        /// <summary>
        /// Gets constraint metadata for a property using reflection.
        /// Reads [Range] and [Step] attributes.
        /// </summary>
        public static (double? Min, double? Max, double? Step) GetConstraints<TFragment, TProperty>(
            Expression<Func<TFragment, TProperty>> propertyExpression)
            where TFragment : FragmentBase
        {
            var memberExpression = propertyExpression.Body as MemberExpression
                ?? (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;

            if (memberExpression?.Member is not PropertyInfo propertyInfo)
                return (null, null, null);

            var rangeAttr = propertyInfo.GetCustomAttribute<RangeAttribute>();
            var stepAttr = propertyInfo.GetCustomAttribute<StepAttribute>();

            double? min = rangeAttr?.Minimum switch
            {
                double d => d,
                int i => (double)i,
                float f => (double)f,
                _ => null
            };

            double? max = rangeAttr?.Maximum switch
            {
                double d => d,
                int i => (double)i,
                float f => (double)f,
                _ => null
            };

            double? step = stepAttr?.Value;

            return (min, max, step);
        }

        /// <summary>
        /// Gets the DynamicSource attribute for a property if present.
        /// </summary>
        public static DynamicSourceAttribute? GetDynamicSource<TFragment, TProperty>(
            Expression<Func<TFragment, TProperty>> propertyExpression)
            where TFragment : FragmentBase
        {
            var memberExpression = propertyExpression.Body as MemberExpression
                ?? (propertyExpression.Body as UnaryExpression)?.Operand as MemberExpression;

            if (memberExpression?.Member is not PropertyInfo propertyInfo)
                return null;

            return propertyInfo.GetCustomAttribute<DynamicSourceAttribute>();
        }

        /// <summary>
        /// Gets all properties with DynamicSource attributes.
        /// </summary>
        public IEnumerable<(PropertyInfo Property, DynamicSourceAttribute Attribute)> GetDynamicSourceProperties()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<DynamicSourceAttribute>();
                if (attr != null)
                    yield return (prop, attr);
            }
        }

        /// <summary>
        /// Gets a value from this fragment's properties by key name.
        /// Returns default(T) if not found or conversion fails.
        /// </summary>
        public virtual T? GetValue<T>(string key)
        {
            var dict = ToDictionary();
            if (!dict.TryGetValue(key, out var value) || value == null)
                return default;

            if (value is T typedValue)
                return typedValue;

            try
            {
                return (T)ConvertValue(value, typeof(T))!;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Gets a value from this fragment's properties by key name.
        /// Returns defaultValue if not found or conversion fails.
        /// </summary>
        public virtual T GetValueOrDefault<T>(string key, T defaultValue)
        {
            var dict = ToDictionary();
            if (!dict.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is T typedValue)
                return typedValue;

            try
            {
                var converted = ConvertValue(value, typeof(T));
                return converted != null ? (T)converted : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a property value by key name.
        /// This is an alias for SetPropertyFromDictionary for API compatibility.
        /// </summary>
        public virtual void SetValue<T>(string key, T value)
        {
            SetPropertyFromDictionary(key, value);
        }

        /// <summary>
        /// Checks if a property has a non-null value.
        /// </summary>
        public virtual bool HasValue(string key)
        {
            var dict = ToDictionary();
            return dict.TryGetValue(key, out var value) && value != null;
        }
    }

    /// <summary>
    /// Extension methods for nullable conversions.
    /// </summary>
    internal static class NullableExtensions
    {
        public static double ToDouble(this int value) => value;
    }
}
